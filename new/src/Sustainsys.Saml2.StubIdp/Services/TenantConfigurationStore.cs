using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Sustainsys.Saml2.StubIdp.Configuration;
using Sustainsys.Saml2.StubIdp.Models;

namespace Sustainsys.Saml2.StubIdp.Services;

/// <summary>
/// Default <see cref="ITenantConfigurationStore"/> implementation backed by JSON files below
/// the configured data directory, with a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// cache. Registered as a singleton.
/// </summary>
public class TenantConfigurationStore(
    IOptions<StubIdpOptions> options,
    IHostEnvironment environment,
    ILogger<TenantConfigurationStore> logger) : ITenantConfigurationStore
{
    private static readonly Guid DefaultTenantGuid = Guid.Parse("e73d98ff-0f1c-4cc2-8808-6d1bf028a8a9");

    // Legacy parity: the manage page re-serialized the JSON with Newtonsoft Formatting.Indented
    // before writing. UnsafeRelaxedJsonEscaping keeps non-ASCII characters literal, like
    // Newtonsoft's default escaping, so saved files stay close to the legacy byte layout.
    private static readonly JsonSerializerOptions IndentedJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ConcurrentDictionary<Guid, Lazy<Task<IdpConfigurationModel?>>> cache = new();
    private readonly string dataDirectory = ResolveDataDirectory(options, environment);
    private readonly ILogger<TenantConfigurationStore> logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public Guid DefaultTenantId => DefaultTenantGuid;

    /// <inheritdoc/>
    /// <remarks>
    /// The one-time file read itself is not cancellable: it completes and populates the cache
    /// even when the caller that triggered it is cancelled mid-wait, so a cancelled first
    /// request does not poison the cache for later callers. A failed read is evicted so the
    /// next caller retries instead of observing a permanently faulted task.
    /// </remarks>
    public async Task<IdpConfigurationModel?> GetConfigurationAsync(Guid idpId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lazy = cache.GetOrAdd(
            idpId,
            static (id, self) => new Lazy<Task<IdpConfigurationModel?>>(
                () => self.LoadAsync(id),
                LazyThreadSafetyMode.ExecutionAndPublication),
            this);

        try
        {
            return await lazy.Value.WaitAsync(cancellationToken);
        }
        catch (Exception) when (lazy.Value.IsFaulted)
        {
            // Evict only when the load itself failed (not when this caller was cancelled), and
            // only when the dictionary still holds the same Lazy instance.
            cache.TryRemove(new KeyValuePair<Guid, Lazy<Task<IdpConfigurationModel?>>>(idpId, lazy));
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IdpConfigurationModel> SaveAsync(Guid idpId, string jsonData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jsonData);
        cancellationToken.ThrowIfCancellationRequested();

        // Legacy parity: the on-disk content is the normalized, indented representation (the
        // legacy ManageController re-serialized with Formatting.Indented before writing).
        var normalized = JsonSerializer.Serialize(JsonNode.Parse(jsonData), IndentedJsonOptions);
        var filePath = GetFilePath(idpId);

        // Write to a temporary file first and move it over the target, so a concurrent reader
        // never observes a partially written file.
        var tempPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, normalized, cancellationToken);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        var model = new IdpConfigurationModel(normalized);
        cache[idpId] = new Lazy<Task<IdpConfigurationModel?>>(Task.FromResult<IdpConfigurationModel?>(model));

        logger.LogInformation("Saved configuration for tenant {TenantId} to {FilePath}.", idpId, filePath);

        return model;
    }

    private static string ResolveDataDirectory(IOptions<StubIdpOptions> options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        var dataPath = options.Value.DataPath;
        return Path.IsPathRooted(dataPath)
            ? dataPath
            : Path.Combine(environment.ContentRootPath, dataPath);
    }

    private string GetFilePath(Guid idpId) =>
        Path.Combine(dataDirectory, idpId == DefaultTenantGuid ? "default.json" : $"{idpId:D}.json");

    private async Task<IdpConfigurationModel?> LoadAsync(Guid idpId)
    {
        var filePath = GetFilePath(idpId);
        if (!File.Exists(filePath))
        {
            logger.LogDebug("No configuration file for tenant {TenantId} at {FilePath}.", idpId, filePath);
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath);

        logger.LogDebug("Loaded configuration for tenant {TenantId} from {FilePath}.", idpId, filePath);

        return new IdpConfigurationModel(json);
    }
}
