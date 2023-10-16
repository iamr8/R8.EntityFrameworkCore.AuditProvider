using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace R8.EntityFrameworkAuditProvider
{
    /// <summary>
    /// An Interceptor to audit changes in <see cref="DbContext"/>.
    /// </summary>
    public class EntityFrameworkAuditProviderInterceptor : SaveChangesInterceptor
    {
        private readonly EntityFrameworkAuditProviderOptions _options;
        private readonly ILogger<EntityFrameworkAuditProviderInterceptor> _logger;
        private readonly string[] _excludedColumns;
        private readonly bool _logDebugEnabled;
        private readonly bool _logWarningEnabled;

        public EntityFrameworkAuditProviderInterceptor(EntityFrameworkAuditProviderOptions options, ILogger<EntityFrameworkAuditProviderInterceptor> logger)
        {
            _options = options;
            _logger = logger;

            _excludedColumns = _options.ExcludedColumns.Distinct().ToArray();

            _logDebugEnabled = _logger.IsEnabled(LogLevel.Debug);
            _logWarningEnabled = _logger.IsEnabled(LogLevel.Warning);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context?.ChangeTracker.Entries();
            var entriesCount = entries.TryGetNonEnumeratedCount(out var count) ? count : entries.Count();
            for (var i = 0; i < entriesCount; i++)
            {
                var entry = new AuditEntityEntry(entries.ElementAt(i));
                await StoringAuditAsync(entry, eventData.Context, cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        internal async ValueTask<bool> StoringAuditAsync<TDbContext>(IEntityEntry entry, TDbContext dbContext, CancellationToken cancellationToken = default) where TDbContext : IInfrastructure<IServiceProvider>?
        {
            if (entry.State is not (EntityState.Added or EntityState.Deleted or EntityState.Modified))
                return false;

            var propertyEntries = GetPropertyEntries(entry);
            if (propertyEntries.Length == 0)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.Entity.GetType().Name, entry.State);
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

                    if (_logDebugEnabled)
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.Entity.GetType().Name, entry.State);
                }
            }
            else
            {
                if (entry.State == EntityState.Deleted)
                {
                    // Delete permanently
                    if (_logDebugEnabled)
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is deleted permanently", entry.Entity.GetType().Name, entry.State);
                    return false;
                }
            }

            if (entity is not IAuditable entityAuditable)
            {
                if (_logDebugEnabled)
                    _logger.LogDebug("Entity {EntityName} with state {EntityState} is not auditable", entry.Entity.GetType().Name, entry.State);
                return false;
            }

            var audit = new Audit { DateTime = DateTime.UtcNow };
            var user = _options.UserProvider?.Invoke(dbContext.GetService<IServiceProvider>());
            if (user != null)
            {
                audit.User = new AuditUser
                {
                    UserId = user.UserId,
                    AdditionalData = user.AdditionalData
                };
            }

            if (_options.IncludeStackTrace)
            {
                var type = this.GetType();
                audit.StackTrace = this.GetStackTrace(type);
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
                            if (_logWarningEnabled)
                                _logger.LogWarning("Cannot delete/undelete and update at the same time");
                            throw new NotSupportedException("Cannot delete/undelete and update at the same time.");
                        }

                        audit.Flag = deleted.Value switch
                        {
                            false => AuditFlag.UnDeleted,
                            true => AuditFlag.Deleted,
                        };

                        if (_logDebugEnabled)
                            _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.Entity.GetType().Name, entry.State);
                    }
                    else
                    {
                        if (changes.Length == 0)
                        {
                            if (_logDebugEnabled)
                                _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.Entity.GetType().Name, entry.State);
                            return false;
                        }

                        audit.Flag = AuditFlag.Changed;
                        audit.Changes = changes.ToArray();
                        if (_logDebugEnabled)
                            _logger.LogDebug("Entity {EntityName} with state {EntityState} is changed", entry.Entity.GetType().Name, entry.State);
                    }

                    break;
                }

                case EntityState.Added:
                {
                    audit.Flag = AuditFlag.Created;
                    if (_logDebugEnabled)
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is created", entry.Entity.GetType().Name, entry.State);
                    break;
                }

                default:
                    throw new NotSupportedException($"State {entry.State} is not supported.");
            }

            var audits = entityAuditable.Audits != null
                ? entityAuditable.Audits.Deserialize<List<Audit>>(_options.JsonOptions)
                : new List<Audit>();
            audits.Add(audit);

            entityAuditable.Audits = JsonSerializer.SerializeToDocument(audits, _options.JsonOptions);

            if (_logDebugEnabled)
                _logger.LogDebug("Entity {EntityName} with state {EntityState} is audited", entry.Entity.GetType().Name, entry.State);
            return true;
        }

        private string[] GetStackTrace(Type interceptorType)
        {
            var stackTrace = new StackTrace();
            var stackFrames = stackTrace.GetFrames();
            if (stackFrames.Length == 0)
                return Array.Empty<string>();

            using var memoryOwner = MemoryPool<string>.Shared.Rent(stackFrames.Length);
            var found = 0;
            foreach (var frame in stackFrames)
            {
                if (TryGetMethodFromStackTrace(frame, interceptorType, out var frameStr))
                    memoryOwner.Memory.Span[found++] = frameStr!;
            }

            return memoryOwner.Memory[..found].ToArray();
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

        private Memory<PropertyEntry> GetPropertyEntries(IEntityEntry entityEntry)
        {
            var count = entityEntry.Members.TryGetNonEnumeratedCount(out var c) ? c : entityEntry.Members.Count();
            using var memoryOwner = MemoryPool<PropertyEntry>.Shared.Rent(count);
            var found = 0;
            for (var i = 0; i < count; i++)
            {
                var member = entityEntry.Members.ElementAt(i);
                if (_excludedColumns.Contains(member.Metadata.Name))
                    continue;

                memoryOwner.Memory.Span[found++] = member;
            }

            var mem = memoryOwner.Memory[..found];
            return mem;
        }

        private (bool? Deleted, Memory<AuditChange> Changed) GetChangedPropertyEntries(Memory<PropertyEntry> propertyEntries)
        {
            using var memoryOwner = MemoryPool<AuditChange>.Shared.Rent(propertyEntries.Length);
            var found = 0;
            bool? deleted = null;
            for (var i = 0; i < propertyEntries.Length; i++)
            {
                var propertyEntry = propertyEntries.Span[i];
                if (propertyEntry.Metadata.PropertyInfo?.GetCustomAttribute<IgnoreAuditAttribute>() != null)
                    continue;

                var propertyName = propertyEntry.Metadata.Name;
                if ((propertyEntry.CurrentValue is null && propertyEntry.OriginalValue is null) ||
                    propertyEntry.CurrentValue?.Equals(propertyEntry.OriginalValue) == true)
                {
                    continue;
                }

                string? newString;
                string? oldString;

                var converters = _options.TypeHandlers.SingleOrDefault(x => x.CanHandle(propertyEntry.Metadata.ClrType));
                if (converters != null)
                {
                    if (!converters.Handle(propertyEntry.CurrentValue, propertyEntry.OriginalValue, _options.JsonOptions))
                        continue;

                    newString = converters.NewValue;
                    oldString = converters.OldValue;
                }
                else
                {
                    if (propertyName == nameof(IAuditableDelete.IsDeleted))
                    {
                        var oldValue = propertyEntry.OriginalValue != null && bool.Parse(propertyEntry.OriginalValue.ToString());
                        var newValue = propertyEntry.CurrentValue != null && bool.Parse(propertyEntry.CurrentValue.ToString());
                        deleted = oldValue switch
                        {
                            false when newValue == true => true,
                            true when newValue == false => false,
                            _ => null
                        };
                        continue;
                    }

                    newString = propertyEntry.CurrentValue?.ToString();
                    oldString = propertyEntry.OriginalValue?.ToString();
                }

                var auditChange = new AuditChange(propertyName, oldString, newString);
                memoryOwner.Memory.Span[found++] = auditChange;
            }

            var mem = memoryOwner.Memory[..found];
            return (deleted, mem);
        }
    }
}