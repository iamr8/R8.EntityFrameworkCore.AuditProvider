using System.Text.Json;

namespace R8.EventSourcing.PostgreSQL
{
    /// <summary>
    /// An <see cref="IAuditable"/> interface that represents an entity that is auditable.
    /// </summary>
    public interface IAuditable : IDisposable
    {
        /// <summary>
        /// Gets or sets the Audit `JSON` data for this record.
        /// </summary>
        JsonDocument? Audits { get; set; }
    }
}