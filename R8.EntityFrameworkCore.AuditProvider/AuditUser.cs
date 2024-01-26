using System.Text.Json.Serialization;

namespace R8.EntityFrameworkCore.AuditProvider
{
    public record struct AuditUser
    {
        [JsonPropertyName(JsonNames.AuditUser.UserId)]
        public string UserId { get; init; }

        [JsonPropertyName(JsonNames.AuditUser.AdditionalData)]
        public IDictionary<string, string> AdditionalData { get; init; }
    }
}