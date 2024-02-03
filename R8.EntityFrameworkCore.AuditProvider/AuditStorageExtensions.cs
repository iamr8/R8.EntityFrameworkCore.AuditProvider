using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    public static class AuditStorageExtensions
    {
        /// <summary>
        /// Deserializes the audit collection.
        /// </summary>
        /// <param name="entity">An entity that has been audited.</param>
        /// <typeparam name="TEntity">A type of entity that has been audited.</typeparam>
        /// <returns>An instance of <see cref="AuditCollection"/> that contains all audits.</returns>
        public static AuditCollection? GetAuditCollection<TEntity>(this TEntity entity) where TEntity : class, IAuditStorage
        {
            var audits = entity.Audits?.Deserialize<Audit[]>(AuditProviderConfiguration.JsonOptions);
            return audits == null ? null : new AuditCollection(audits);
        }
    }
}