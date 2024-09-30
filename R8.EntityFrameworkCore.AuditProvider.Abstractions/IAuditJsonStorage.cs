using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditStorageBase"/> interface that represents a column to store audits.
    /// </summary>
    public interface IAuditStorageBase
    {
    }

    /// <summary>
    /// An <see cref="IAuditJsonStorage"/> interface that represents an a column to store audits.
    /// </summary>
    public interface IAuditJsonStorage : IAuditStorageBase
    {
        /// <summary>
        /// Gets an <see cref="JsonElement"/> that represents audits for this record.
        /// </summary>
        JsonElement? Audits { get; set; }
    }

    /// <summary>
    /// An <see cref="IAuditJsonStorage"/> interface that represents an a column to store audits.
    /// </summary>
    public interface IAuditStorage : IAuditStorageBase
    {
        /// <summary>
        /// Gets an array of <see cref="Audit"/> that represents audits for this record.
        /// </summary>
        /// <remarks>Don't forget to Serialize/Deserialize this using <see cref="AuditProviderConfiguration.JsonOptions"/>.</remarks>
        Audit[]? Audits { get; set; }
    }
}