using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Source-generated (high-performance, allocation-free) log messages for the audit interceptor.
    /// EventIds match the previous hand-written ones: Created=0, Changed=1, Deleted=2, UnDeleted=3,
    /// NotAuditable=100, NoChangesFound=200.
    /// </summary>
    internal static partial class AuditProviderLog
    {
        [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "Entity {EntityName} with state {EntityState} does not implemented by {Auditable}. So it will be ignored while is not auditable")]
        public static partial void NotAuditable(this ILogger logger, string entityName, EntityState entityState, string auditable);

        [LoggerMessage(EventId = 200, Level = LogLevel.Debug, Message = "Entity {EntityName} with state {EntityState} has no changes")]
        public static partial void NoChangesFound(this ILogger logger, string entityName, EntityState entityState);

        [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Entity {EntityName} is marked at {AuditFlag}")]
        public static partial void Created(this ILogger logger, string entityName, AuditFlag? auditFlag);

        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Entity {EntityName} is marked as {AuditFlag}")]
        public static partial void Changed(this ILogger logger, string entityName, AuditFlag? auditFlag);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Entity {EntityName} is marked as {AuditFlag}")]
        public static partial void Deleted(this ILogger logger, string entityName, AuditFlag? auditFlag);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Entity {EntityName} is marked as {AuditFlag}")]
        public static partial void UnDeleted(this ILogger logger, string entityName, AuditFlag? auditFlag);
    }
}
