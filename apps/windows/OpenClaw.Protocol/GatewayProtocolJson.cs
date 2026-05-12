using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenClaw.Protocol;

/// <summary>
/// Provides the JSON contract shared by generated gateway protocol models.
/// </summary>
public static class GatewayProtocolJson
{
    /// <summary>
    /// Serializer settings used for gateway frames so enum values stay wire-compatible with TypeScript names.
    /// </summary>
    public static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
