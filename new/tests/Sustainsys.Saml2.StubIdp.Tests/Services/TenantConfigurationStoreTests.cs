using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Services;

namespace Sustainsys.Saml2.StubIdp.Tests.Services;

/// <summary>
/// Tests for <see cref="TenantConfigurationStore"/>: file resolution, caching, ETag
/// stability, null-on-unknown lookup and save semantics. Each test gets an isolated
/// temporary data directory.
/// </summary>
public class TenantConfigurationStoreTests : IDisposable
{
    private static readonly Guid DefaultTenantGuid = Guid.Parse("e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9");
    private static readonly Guid SampleTenantGuid = Guid.Parse("2273dd5a-8e86-4abc-9078-236215f06c0e");

    private readonly string contentRootPath = Path.Combine(Path.GetTempPath(), "stubidp-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string dataDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantConfigurationStoreTests"/> class and
    /// creates the isolated data directory with the two bundled tenant files.
    /// </summary>
    public TenantConfigurationStoreTests()
    {
        dataDirectory = Path.Combine(contentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, "default.json"), """{ "IdpDescription": "default tenant", "HideDetails": false }""");
        File.WriteAllText(Path.Combine(dataDirectory, $"{SampleTenantGuid:D}.json"), """{ "IdpDescription": "sample tenant", "HideDetails": true }""");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(contentRootPath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup of temporary test data.
        }

        GC.SuppressFinalize(this);
    }

    private IHostEnvironment CreateEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(contentRootPath);
        return environment.Object;
    }

    private TenantConfigurationStore CreateStore() =>
        new(
            Options.Create(new StubIdpOptions { DataPath = "App_Data" }),
            CreateEnvironment(),
            NullLogger<TenantConfigurationStore>.Instance);

    private static string ExpectedETag(string json) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(json)));

    /// <summary>
    /// Verifies the well-known default tenant GUID of the legacy implementation.
    /// </summary>
    [Fact]
    public void Default_tenant_id_is_the_legacy_default_guid()
    {
        Assert.Equal(DefaultTenantGuid, CreateStore().DefaultTenantId);
    }

    /// <summary>
    /// Verifies that the default tenant GUID resolves to <c>default.json</c> and that the
    /// model carries the exact file content and its uppercase MD5 ETag (the assertion from
    /// the task specification).
    /// </summary>
    [Fact]
    public async Task Default_tenant_reads_default_json()
    {
        var store = CreateStore();

        var model = await store.GetConfigurationAsync(DefaultTenantGuid);

        Assert.NotNull(model);
        var json = await File.ReadAllTextAsync(Path.Combine(dataDirectory, "default.json"));
        Assert.Equal(json, model.JsonData);
        Assert.Equal(ExpectedETag(json), model.ETag);
        Assert.Equal("default tenant", model.IdpDescription);
    }

    /// <summary>
    /// Verifies that any other GUID resolves to <c>{guid}.json</c> in canonical lowercase
    /// format.
    /// </summary>
    [Fact]
    public async Task Named_tenant_reads_its_own_file()
    {
        var store = CreateStore();

        var model = await store.GetConfigurationAsync(SampleTenantGuid);

        Assert.NotNull(model);
        Assert.Equal("sample tenant", model.IdpDescription);
        Assert.True(model.HideDetails);
    }

    /// <summary>
    /// Verifies that an unknown GUID yields null.
    /// </summary>
    [Fact]
    public async Task Unknown_tenant_returns_null()
    {
        var store = CreateStore();

        var model = await store.GetConfigurationAsync(Guid.NewGuid());

        Assert.Null(model);
    }

    /// <summary>
    /// Verifies the legacy <c>GetOrAdd</c> semantics: a null result is cached, so a file
    /// appearing on disk after the first lookup is not picked up until a save.
    /// </summary>
    [Fact]
    public async Task Unknown_tenant_null_result_is_cached()
    {
        var store = CreateStore();
        var idpId = Guid.NewGuid();

        Assert.Null(await store.GetConfigurationAsync(idpId));

        await File.WriteAllTextAsync(Path.Combine(dataDirectory, $"{idpId:D}.json"), """{ "IdpDescription": "late file" }""");

        Assert.Null(await store.GetConfigurationAsync(idpId));
    }

    /// <summary>
    /// Verifies that a cached configuration is reused: repeated lookups return the same
    /// instance instead of re-reading the file.
    /// </summary>
    [Fact]
    public async Task Cached_instance_is_reused()
    {
        var store = CreateStore();

        var first = await store.GetConfigurationAsync(DefaultTenantGuid);
        var second = await store.GetConfigurationAsync(DefaultTenantGuid);

        Assert.Same(first, second);
    }

    /// <summary>
    /// Verifies the <c>GetOrAdd</c> race semantics: concurrent first lookups all observe the
    /// same loaded instance.
    /// </summary>
    [Fact]
    public async Task Concurrent_lookups_observe_the_same_instance()
    {
        var store = CreateStore();

        var models = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => store.GetConfigurationAsync(DefaultTenantGuid)));

        Assert.All(models, model => Assert.Same(models[0], model));
    }

    /// <summary>
    /// Verifies that a configuration file with invalid JSON still yields a model with null
    /// values and default <c>HideDetails</c>, like the legacy implementation.
    /// </summary>
    [Fact]
    public async Task Invalid_json_file_yields_model_with_null_values()
    {
        var idpId = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(dataDirectory, $"{idpId:D}.json"), "{ not json");

        var store = CreateStore();

        var model = await store.GetConfigurationAsync(idpId);

        Assert.NotNull(model);
        Assert.Null(model.DefaultAudience);
        Assert.True(model.HideDetails);
    }

    /// <summary>
    /// Verifies that saving writes the normalized, indented JSON to the tenant file and
    /// replaces the cached value.
    /// </summary>
    [Fact]
    public async Task Save_writes_indented_json_and_updates_the_cache()
    {
        var store = CreateStore();
        var idpId = Guid.NewGuid();
        const string json = """{"IdpDescription":"saved tenant","HideDetails":false}""";

        var saved = await store.SaveAsync(idpId, json);

        var fileContent = await File.ReadAllTextAsync(Path.Combine(dataDirectory, $"{idpId:D}.json"));
        Assert.Equal(saved.JsonData, fileContent);
        Assert.Contains("\"IdpDescription\": \"saved tenant\"", fileContent); // indented
        Assert.Equal(ExpectedETag(fileContent), saved.ETag);

        var cached = await store.GetConfigurationAsync(idpId);
        Assert.Same(saved, cached);
    }

    /// <summary>
    /// Verifies that saving an already-cached tenant replaces the cached instance (including
    /// a previously cached null), so the new ETag becomes observable immediately.
    /// </summary>
    [Fact]
    public async Task Save_replaces_a_previously_cached_lookup()
    {
        var store = CreateStore();
        var idpId = Guid.NewGuid();

        Assert.Null(await store.GetConfigurationAsync(idpId));

        var saved = await store.SaveAsync(idpId, """{ "IdpDescription": "now configured" }""");

        var cached = await store.GetConfigurationAsync(idpId);
        Assert.Same(saved, cached);
        Assert.Equal("now configured", cached!.IdpDescription);
    }

    /// <summary>
    /// Verifies that saving rejects invalid JSON and leaves the cache untouched.
    /// </summary>
    [Fact]
    public async Task Save_throws_for_invalid_json()
    {
        var store = CreateStore();
        var idpId = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<JsonException>(() => store.SaveAsync(idpId, "{ not json"));

        Assert.Null(await store.GetConfigurationAsync(idpId));
    }

    /// <summary>
    /// Verifies the null-argument guard on <see cref="TenantConfigurationStore.SaveAsync"/>.
    /// </summary>
    [Fact]
    public async Task Save_throws_for_null_json()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() => store.SaveAsync(Guid.NewGuid(), null!));
    }

    /// <summary>
    /// Verifies that the write honors the cancellation token.
    /// </summary>
    [Fact]
    public async Task Save_honors_cancellation()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.SaveAsync(Guid.NewGuid(), """{ "IdpDescription": "x" }""", cancellation.Token));
    }

    /// <summary>
    /// Verifies the documented cancellation semantics of the one-time read: a cancelled
    /// caller observes <see cref="OperationCanceledException"/> and the cache stays clean for
    /// the next caller.
    /// </summary>
    [Fact]
    public async Task Cancelled_lookup_throws_and_does_not_poison_the_cache()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => store.GetConfigurationAsync(DefaultTenantGuid, cancellation.Token));

        Assert.NotNull(await store.GetConfigurationAsync(DefaultTenantGuid));
    }

    /// <summary>
    /// Verifies the null-argument guards on the constructor.
    /// </summary>
    [Fact]
    public void Constructor_throws_for_null_arguments()
    {
        var options = Options.Create(new StubIdpOptions { DataPath = "App_Data" });
        var environment = CreateEnvironment();
        var logger = NullLogger<TenantConfigurationStore>.Instance;

        Assert.Throws<ArgumentNullException>(() => new TenantConfigurationStore(null!, environment, logger));
        Assert.Throws<ArgumentNullException>(() => new TenantConfigurationStore(options, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new TenantConfigurationStore(options, environment, null!));
    }
}
