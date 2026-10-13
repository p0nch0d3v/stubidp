using System.Security.Cryptography;
using System.Text;
using Sustainsys.Saml2.StubIdp.Models;

namespace Sustainsys.Saml2.StubIdp.Tests.Models;

/// <summary>
/// Tests for <see cref="IdpConfigurationModel"/>, the System.Text.Json port of the legacy
/// Newtonsoft-based model.
/// </summary>
public class IdpConfigurationModelTests
{
    private const string ValidJson = """
        {
          "DefaultAssertionConsumerServiceUrl": "https://sp.example.com/SAML2/Acs",
          "DefaultAudience": "https://sp.example.com/SAML2",
          "IdpDescription": "This is my custom IDP description",
          "HideDetails": false,
          "UserList": []
        }
        """;

    /// <summary>
    /// The ETag must be the uppercase hexadecimal MD5 of the exact JSON string, identical to
    /// the legacy implementation (this is the assertion from the task specification).
    /// </summary>
    [Fact]
    public void ETag_is_uppercase_hex_md5_of_the_exact_json()
    {
        var model = new IdpConfigurationModel(ValidJson);

        Assert.Equal(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(ValidJson))), model.ETag);
    }

    /// <summary>
    /// Verifies that identical JSON yields an identical, stable ETag across instances.
    /// </summary>
    [Fact]
    public void ETag_is_stable_for_identical_json()
    {
        var first = new IdpConfigurationModel(ValidJson);
        var second = new IdpConfigurationModel(ValidJson);

        Assert.Equal(first.ETag, second.ETag);
    }

    /// <summary>
    /// Verifies that the raw JSON is preserved exactly, since it is served verbatim by the
    /// CurrentConfiguration endpoint.
    /// </summary>
    [Fact]
    public void JsonData_is_preserved_exactly()
    {
        var model = new IdpConfigurationModel(ValidJson);

        Assert.Equal(ValidJson, model.JsonData);
    }

    /// <summary>
    /// Verifies that the well-known scalar keys are read from the JSON.
    /// </summary>
    [Fact]
    public void Scalar_values_are_read_from_the_json()
    {
        var model = new IdpConfigurationModel(ValidJson);

        Assert.Equal("https://sp.example.com/SAML2/Acs", model.DefaultAssertionConsumerServiceUrl);
        Assert.Equal("https://sp.example.com/SAML2", model.DefaultAudience);
        Assert.Equal("This is my custom IDP description", model.IdpDescription);
    }

    /// <summary>
    /// Verifies the legacy <c>HideDetails</c> semantics for the supported JSON shapes.
    /// </summary>
    /// <param name="json">The JSON document to parse.</param>
    /// <param name="expected">The expected <see cref="IdpConfigurationModel.HideDetails"/> value.</param>
    [Theory]
    [InlineData("""{ "HideDetails": true }""", true)]
    [InlineData("""{ "HideDetails": false }""", false)]
    [InlineData("""{ "HideDetails": "true" }""", true)]
    [InlineData("""{ "HideDetails": "false" }""", false)]
    [InlineData("""{ "HideDetails": "TrUe" }""", true)]
    [InlineData("""{ "HideDetails": "anything" }""", false)]
    [InlineData("""{ }""", true)]
    [InlineData("""{ "HideDetails": null }""", true)]
    public void HideDetails_follows_legacy_semantics(string json, bool expected)
    {
        var model = new IdpConfigurationModel(json);

        Assert.Equal(expected, model.HideDetails);
    }

    /// <summary>
    /// Verifies that missing keys yield null values, like the legacy implementation.
    /// </summary>
    [Fact]
    public void Missing_keys_yield_null()
    {
        var model = new IdpConfigurationModel("""{ "UserList": [] }""");

        Assert.Null(model.DefaultAssertionConsumerServiceUrl);
        Assert.Null(model.DefaultAudience);
        Assert.Null(model.IdpDescription);
    }

    /// <summary>
    /// Verifies that a scalar number is converted to text, matching the legacy
    /// <c>(string)JToken</c> conversion.
    /// </summary>
    [Fact]
    public void Number_value_is_converted_to_text()
    {
        var model = new IdpConfigurationModel("""{ "IdpDescription": 42 }""");

        Assert.Equal("42", model.IdpDescription);
    }

    /// <summary>
    /// Verifies that object and array values yield null: the legacy explicit string conversion
    /// of a complex token threw and was swallowed by the catch-all.
    /// </summary>
    [Theory]
    [InlineData("""{ "DefaultAudience": { "Nested": true } }""")]
    [InlineData("""{ "DefaultAudience": [ "a", "b" ] }""")]
    public void Complex_values_yield_null(string json)
    {
        var model = new IdpConfigurationModel(json);

        Assert.Null(model.DefaultAudience);
    }

    /// <summary>
    /// Verifies the legacy leniency: invalid JSON yields null values and
    /// <see cref="IdpConfigurationModel.HideDetails"/> defaults to true, but the ETag is still
    /// computed over the exact input.
    /// </summary>
    [Fact]
    public void Invalid_json_yields_nulls_and_default_hide_details()
    {
        const string json = "{ this is not json";

        var model = new IdpConfigurationModel(json);

        Assert.Null(model.DefaultAssertionConsumerServiceUrl);
        Assert.Null(model.DefaultAudience);
        Assert.Null(model.IdpDescription);
        Assert.True(model.HideDetails);
        Assert.Equal(Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(json))), model.ETag);
    }

    /// <summary>
    /// Verifies the null-argument guard.
    /// </summary>
    [Fact]
    public void Constructor_throws_for_null_json()
    {
        Assert.Throws<ArgumentNullException>(() => new IdpConfigurationModel(null!));
    }
}
