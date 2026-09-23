using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoWebNav;

/// <summary>
/// The serializer configuration for everything that crosses into the page scripts. The camelCase
/// policy is load-bearing: fingerprint.js and resolver.js read <see cref="ElementFingerprint"/>
/// keys by their camelCase names (XPath → xPath), so a caller's own JSON settings must never leak
/// in here.
/// </summary>
public static class WebNavJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
