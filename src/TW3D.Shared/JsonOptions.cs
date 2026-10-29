using System.Text.Json;
using System.Text.Json.Serialization;

namespace TW3D.Shared
{
    internal static class JsonOptions
    {
        /// <summary>camelCase names, enums as strings; reading tolerates comments and trailing commas.</summary>
        public static readonly JsonSerializerOptions Default = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }
}
