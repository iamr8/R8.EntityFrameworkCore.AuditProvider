using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// An Interceptor to audit changes in <see cref="DbContext"/>.
    /// </summary>
    public class EntityFrameworkAuditProviderInterceptor : SaveChangesInterceptor
    {
        private readonly EntityFrameworkAuditProviderOptions _options;
        private readonly ILogger<EntityFrameworkAuditProviderInterceptor> _logger;

        public EntityFrameworkAuditProviderInterceptor(EntityFrameworkAuditProviderOptions options, ILogger<EntityFrameworkAuditProviderInterceptor> logger)
        {
            _options = options;
            _logger = logger;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context?.ChangeTracker.Entries();
            if (entries == null)
                return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
            
            using var enumerator = entries.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var entry = enumerator.Current;
                var runtimeAnnotations = entry.Metadata.FindRuntimeAnnotation(AuditIgnoranceAnnotation);
                if (runtimeAnnotations?.Value is true)
                    continue;

                var auditEntry = new AuditEntityEntry(entry);
                var audit = await StoringAuditAsync(auditEntry, eventData.Context, cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        internal async ValueTask<bool> StoringAuditAsync(IEntityEntry entry, DbContext? dbContext, CancellationToken cancellationToken = default)
        {
            if (entry.State is not (EntityState.Added or EntityState.Deleted or EntityState.Modified))
                return false;

            var propertyEntries = entry.Members.OfType<PropertyEntry>().ToArray();
            if (propertyEntries.Length == 0)
            {
                _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.EntityType.Name, entry.State);
                return false;
            }

            var entity = entry.Entity;
            if (entity is IAuditableDelete entitySoftDelete)
            {
                if (entry.State == EntityState.Deleted)
                {
                    // Set deleted flag
                    await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                    entitySoftDelete.IsDeleted = true;
                    entry.DetectChanges();
                    entry.State = EntityState.Modified;

                    _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.EntityType.Name, entry.State);
                }
            }
            else
            {
                if (entry.State == EntityState.Deleted)
                {
                    // Delete permanently
                    _logger.LogDebug("Entity {EntityName} with state {EntityState} is deleted permanently", entry.EntityType.Name, entry.State);
                    return false;
                }
            }

            if (entity is not IAuditable entityAuditable)
            {
                _logger.LogDebug("Entity {EntityName} with state {EntityState} is not auditable", entry.EntityType.Name, entry.State);
                return false;
            }

            var audit = new Audit { DateTime = DateTime.UtcNow };

            if (dbContext != null && _options.UserProvider != null)
            {
                var user = _options.UserProvider.Invoke(dbContext.GetService<IServiceProvider>());
                audit.User = new AuditUser
                {
                    UserId = user.UserId,
                    AdditionalData = user.AdditionalData
                };
            }

            if (_options.IncludeStackTrace)
            {
                audit.StackTrace = this.GetStackTrace(typeof(EntityFrameworkAuditProviderInterceptor));
            }

            switch (entry.State)
            {
                case EntityState.Modified:
                {
                    var (deleted, changes) = GetChangedPropertyEntries(propertyEntries);
                    if (deleted.HasValue)
                    {
                        if (changes.Length > 0)
                        {
                            _logger.LogWarning("Cannot delete/undelete and update at the same time");
                            throw new NotSupportedException("Cannot delete/undelete and update at the same time.");
                        }

                        audit.Flag = deleted.Value switch
                        {
                            false => AuditFlag.UnDeleted,
                            true => AuditFlag.Deleted,
                        };

                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.EntityType.Name, entry.State);
                    }
                    else
                    {
                        if (changes.Length == 0)
                        {
                            _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.EntityType.Name, entry.State);
                            return false;
                        }

                        audit.Flag = AuditFlag.Changed;
                        audit.Changes = changes;
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is changed", entry.EntityType.Name, entry.State);
                    }

                    break;
                }

                case EntityState.Added:
                {
                    audit.Flag = AuditFlag.Created;
                    _logger.LogDebug("Entity {EntityName} with state {EntityState} is created", entry.EntityType.Name, entry.State);
                    break;
                }

                default:
                    throw new NotSupportedException($"State {entry.State} is not supported.");
            }

            var audits = entityAuditable.Audits != null
                ? entityAuditable.Audits.Deserialize<List<Audit>>(_options.JsonOptions) ?? new List<Audit>()
                : new List<Audit>();
            audits.Add(audit);

            entityAuditable.Audits = JsonSerializer.SerializeToDocument(audits, _options.JsonOptions);

            _logger.LogDebug("Entity {EntityName} with state {EntityState} is audited", entry.EntityType.Name, entry.State);
            return true;
        }

        private string[] GetStackTrace(Type interceptorType)
        {
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames();
            if (stackFrames.Length == 0)
                return Array.Empty<string>();

            Memory<string> memory = new string[stackFrames.Length];
            var lastIndex = -1;
            foreach (var frame in stackFrames)
            {
                if (TryGetMethodFromStackTrace(frame, interceptorType, out var frameStr))
                    memory.Span[++lastIndex] = frameStr!;
            }

            var array = memory[..(lastIndex + 1)].ToArray();
            return array;
        }

        private bool TryGetMethodFromStackTrace(StackFrame frame, Type interceptorType, out string? rawFrame)
        {
            rawFrame = null;
            if (!frame.HasMethod())
                return false;

            var methodInfo = frame.GetMethod() as MethodInfo;
            var methodType = methodInfo!.DeclaringType;
            var containerType = methodType?.DeclaringType;
            if (methodType?.Namespace == null || _options.ExcludedNamespacesInStackTrace.Any(ignoredNamespace => methodType.Namespace.StartsWith(ignoredNamespace)) || methodType == interceptorType || (containerType != null && containerType == interceptorType))
                return false;

            var sb = new StringBuilder();
            var fileName = frame.GetFileName();
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                sb.Append(fileName);
                var lineNumber = frame.GetFileLineNumber();
                if (lineNumber > 0)
                    sb.Append(':').Append(lineNumber);
                sb.Append('+');
            }

            sb.Append(methodInfo);
            if (containerType != null)
            {
                sb.Append(" (").Append(methodType).Append(')');
            }

            rawFrame = sb.ToString();
            return true;
        }

        internal const string AuditIgnoranceAnnotation = "AuditProvider:IgnoreAuditing";

        private (bool? Deleted, AuditChange[] Changed) GetChangedPropertyEntries(ICollection<PropertyEntry> propertyEntries)
        {
            Memory<AuditChange> memory = new AuditChange[propertyEntries.Count];
            var lastIndex = -1;
            bool? deleted = null;
            foreach (var propertyEntry in propertyEntries)
            {
                var propertyType = propertyEntry.Metadata.ClrType;
                if (propertyEntry.Metadata.PropertyInfo?.GetCustomAttribute<IgnoreAuditAttribute>() != null)
                    continue;

                var propertyName = propertyEntry.Metadata.Name;
                var currentNull = propertyEntry.CurrentValue is null;
                var originalNull = propertyEntry.OriginalValue is null;
                if ((currentNull && originalNull) || propertyEntry.CurrentValue?.Equals(propertyEntry.OriginalValue) == true)
                    continue;

                if (propertyEntry is { CurrentValue: IEnumerable ce, OriginalValue: IEnumerable oe })
                {
                    var currentEnumerator = ce.GetEnumerator();
                    var originalEnumerator = oe.GetEnumerator();
                    var currentHasNext = currentEnumerator.MoveNext();
                    var originalHasNext = originalEnumerator.MoveNext();
                    while (currentHasNext && originalHasNext)
                    {
                        if (!currentEnumerator.Current!.Equals(originalEnumerator.Current))
                            break;

                        currentHasNext = currentEnumerator.MoveNext();
                        originalHasNext = originalEnumerator.MoveNext();
                    }

                    if (!currentHasNext && !originalHasNext)
                        continue;
                }

                if (propertyName == nameof(IAuditableDelete.IsDeleted))
                {
                    var oldValue = !originalNull && (bool)propertyEntry.OriginalValue!;
                    var newValue = !currentNull && (bool)propertyEntry.CurrentValue!;
                    deleted = oldValue switch
                    {
                        false when newValue == true => true,
                        true when newValue == false => false,
                        _ => null
                    };
                    continue;
                }

                string? newString = null;
                if (!currentNull)
                {
                    newString = JsonSerializer.Serialize(propertyEntry.CurrentValue, propertyType, _options.JsonOptions);
                    if (newString.StartsWith("\"") && newString.EndsWith("\""))
                        newString = newString[1..^1];
                }

                string? oldString = null;
                if (!originalNull)
                {
                    oldString = JsonSerializer.Serialize(propertyEntry.OriginalValue, propertyType, _options.JsonOptions);
                    if (oldString.StartsWith("\"") && oldString.EndsWith("\""))
                        oldString = oldString[1..^1];
                }

                var auditChange = new AuditChange(propertyName, oldString, newString);
                memory.Span[++lastIndex] = auditChange;
            }

            var array = memory[..(lastIndex + 1)].ToArray();
            return (deleted, array);
        }
    }
}