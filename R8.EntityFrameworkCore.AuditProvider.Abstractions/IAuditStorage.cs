using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditStorage"/> interface that represents an a column to store audits.
    /// </summary>
    public interface IAuditStorage
    {
        /// <summary>
        /// Gets the Audit `JSON` data for this record.
        /// </summary>
        JsonElement? Audits { get; set; }
    }
}