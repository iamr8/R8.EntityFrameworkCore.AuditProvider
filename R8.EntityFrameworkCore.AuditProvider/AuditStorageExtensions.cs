using System.Text.Json;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Extension methods for reading the stored audit collection from an audited entity.
    /// </summary>
    public static class AuditStorageExtensions
    {
        /// <summary>
        /// Deserializes the audit collection.
        /// </summary>
        /// <param name="entity">An entity that has been audited.</param>
        /// <returns>An instance of <see cref="AuditCollection"/> that contains all audits.</returns>
        public static AuditCollection? GetAuditCollection(this IAuditJsonStorage entity)
        {
            var audits = entity.Audits?.Deserialize<Audit[]>(AuditProviderConfiguration.JsonOptions);
            return audits == null ? null : new AuditCollection(audits);
        }

        /// <summary>
        /// Deserializes the audit collection.
        /// </summary>
        /// <param name="entity">An entity that has been audited.</param>
        /// <returns>An instance of <see cref="AuditCollection"/> that contains all audits.</returns>
        public static AuditCollection? GetAuditCollection(this IAuditStorage entity)
        {
            var audits = entity.Audits;
            return audits == null ? null : new AuditCollection(audits);
        }
    }
}