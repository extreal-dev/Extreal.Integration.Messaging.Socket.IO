using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Extreal.Integration.Messaging.Redis
{
    [SuppressMessage("Usage", "CC0047")]
    public class GroupResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
