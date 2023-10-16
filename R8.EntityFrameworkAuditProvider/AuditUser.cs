using System.Text.Json.Serialization;

namespace R8.EntityFrameworkAuditProvider
{
    public record struct AuditUser
    {
        [JsonPropertyName(JsonNames.AuditUser.UserId)]
        public string UserId { get; init; }

        [JsonPropertyName(JsonNames.AuditUser.AdditionalData)]
        public Dictionary<string, string> AdditionalData { get; init; }
    }
}