using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// Represents the user that made a change, as stored in an <see cref="Audit"/>.
    /// </summary>
    public record struct AuditUser
    {
        /// <summary>
        /// Gets or sets the id of the user.
        /// </summary>
        [JsonPropertyName(JsonNames.AuditUser.UserId)]
        public string? UserId { get; set; }

        /// <summary>
        /// Gets or sets additional data stored alongside the user id.
        /// </summary>
        [JsonPropertyName(JsonNames.AuditUser.AdditionalData)]
        public IDictionary<string, string>? AdditionalData { get; set; }
    }
}