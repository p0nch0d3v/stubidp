using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sustainsys.Saml2.StubIdp.Models;

/// <summary>
/// The parsed configuration of a single tenant, together with the raw JSON and its ETag.
/// Port of <c>legacy/Sustainsys.Saml2.StubIdp/Models/IdpConfigurationModel.cs</c> from
/// Newtonsoft.Json to <see cref="System.Text.Json"/>.
/// </summary>
/// <remarks>
/// The class deliberately preserves legacy leniency: when the JSON cannot be parsed or a key
/// is missing, the corresponding value is <see langword="null"/> instead of an exception, and
/// <see cref="HideDetails"/> then defaults to <see langword="true"/>.
/// </remarks>
public class IdpConfigurationModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdpConfigurationModel"/> class.
    /// </summary>
    /// <param name="jsonData">The exact JSON of the tenant configuration file. The
    /// <see cref="ETag"/> is computed over this exact string.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jsonData"/> is
    /// <see langword="null"/>.</exception>
    public IdpConfigurationModel(string jsonData)
    {
        ArgumentNullException.ThrowIfNull(jsonData);

        JsonData = jsonData;
        ETag = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(jsonData)));
        DefaultAssertionConsumerServiceUrl = GetJsonValue(jsonData, nameof(DefaultAssertionConsumerServiceUrl));
        DefaultAudience = GetJsonValue(jsonData, nameof(DefaultAudience));
        IdpDescription = GetJsonValue(jsonData, nameof(IdpDescription));
        HideDetails = (GetJsonValue(jsonData, nameof(HideDetails)) ?? "true").ToLowerInvariant() == "true";
    }

    /// <summary>
    /// Gets the exact JSON string the model was created from.
    /// </summary>
    public string JsonData { get; }

    /// <summary>
    /// Gets the ETag of <see cref="JsonData"/>: the uppercase hexadecimal MD5 hash of the
    /// UTF-8 encoded JSON, identical to the legacy implementation.
    /// </summary>
    public string ETag { get; }

    /// <summary>
    /// Gets the default assertion consumer service URL of the tenant, or
    /// <see langword="null"/> when the key is missing or not a scalar value.
    /// </summary>
    public string? DefaultAssertionConsumerServiceUrl { get; }

    /// <summary>
    /// Gets the default audience of the tenant, or <see langword="null"/> when the key is
    /// missing or not a scalar value.
    /// </summary>
    public string? DefaultAudience { get; }

    /// <summary>
    /// Gets the tenant description shown on the home page, or <see langword="null"/> when the
    /// key is missing or not a scalar value.
    /// </summary>
    public string? IdpDescription { get; }

    /// <summary>
    /// Gets a value indicating whether the details section of the home page is hidden.
    /// Defaults to <see langword="true"/> when the key is missing or cannot be read.
    /// </summary>
    public bool HideDetails { get; }

    private static string? GetJsonValue(string jsonData, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonData);
            if (!document.RootElement.TryGetProperty(key, out var value))
            {
                return null;
            }

            // Legacy parity with (string)JObject.Parse(jsonData)[key]: scalar JSON values are
            // converted to text, while a missing key, an object/array value or invalid JSON
            // yields null (the legacy catch-all swallowed every such error).
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
