using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditable"/> interface that represents an entity that is auditable.
    /// </summary>
    public interface IAuditable
    {
        /// <summary>
        /// Gets or sets the Audit `JSON` data for this record.
        /// </summary>
        JsonElement? Audits { get; set; }
    }
}