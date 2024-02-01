using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// An Interceptor to audit changes in <see cref="DbContext"/>.
    /// </summary>
    public class EntityFrameworkAuditProviderInterceptor : SaveChangesInterceptor
    {
        private readonly AuditProviderOptions _options;
        private readonly IServiceProvider _serviceProvider;

        private readonly ILogger<EntityFrameworkAuditProviderInterceptor> _logger;

        public EntityFrameworkAuditProviderInterceptor(AuditProviderOptions options, IServiceProvider serviceProvider, ILogger<EntityFrameworkAuditProviderInterceptor> logger)
        {
            _options = options;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var entries = eventData.Context?.ChangeTracker.Entries();
            if (entries != null)
            {
                using var enumerator = entries.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    var entry = enumerator.Current;
                    var auditEntry = new AuditEntityEntry(entry);
                    await AckAuditsAsync(auditEntry, eventData.Context, cancellationToken).ConfigureAwait(false);
                }
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        [DebuggerStepThrough]
        internal async ValueTask AckAuditsAsync(IEntityEntry entry, DbContext? dbContext, CancellationToken cancellationToken = default)
        {
            if (entry.State is not (EntityState.Added or EntityState.Deleted or EntityState.Modified))
                return;

            using var logScope = _logger.BeginScope("Storing audit for {EntityName} with state {EntityState}", entry.EntityType.Name, entry.State);
            var propertyEntries = entry.Members.OfType<PropertyEntry>().ToArray();
            var entity = entry.Entity;
            if (entity is not IAuditActivator auditActivator)
                return;

            var currentDateTime = _options.DateTimeProvider?.Invoke(_serviceProvider) ?? DateTime.UtcNow;

            var hasStorage = auditActivator is IAuditStorage;
            AuditUser? auditUser = null;
            AuditFlag? auditFlag = null;
            var finalChanges = Memory<AuditChange>.Empty;
            var canStore = hasStorage;

            if (dbContext != null && _options.UserProvider != null && hasStorage)
            {
                var user = _options.UserProvider.Invoke(_serviceProvider);
                if (user != null)
                {
                    auditUser = new AuditUser
                    {
                        UserId = user.UserId,
                        AdditionalData = user.AdditionalData,
                    };
                }
            }

            switch (entry.State)
            {
                case EntityState.Deleted:
                {
                    if (auditActivator is IAuditSoftDelete entitySoftDelete)
                    {
                        if (entry.State == EntityState.Deleted)
                        {
                            if (propertyEntries.Any(c => c.Metadata.Name.Equals(nameof(IAuditSoftDelete.IsDeleted), StringComparison.Ordinal) && ((bool)c.OriginalValue) == true))
                                return;

                            // Set deleted flag
                            await entry.ReloadAsync(cancellationToken).ConfigureAwait(false);
                            entitySoftDelete.IsDeleted = true;
                            entry.DetectChanges();
                            entry.State = EntityState.Modified;

                            PerformDeleteUndelete(entry, auditActivator, deleted: true, ref auditFlag, ref canStore, hasStorage, currentDateTime);
                        }
                    }

                    break;
                }
                case EntityState.Modified:
                {
                    var (deleted, changes) = GetChangedPropertyEntries(propertyEntries, hasStorage);
                    if (deleted.HasValue)
                    {
                        if (changes.Length > 0)
                            throw new NotSupportedException("Cannot delete/undelete and update at the same time.");

                        if (auditActivator is IAuditSoftDelete softDelete)
                            softDelete.IsDeleted = deleted.Value;

                        PerformDeleteUndelete(entry, auditActivator, deleted.Value, ref auditFlag, ref canStore, hasStorage, currentDateTime);
                    }
                    else
                    {
                        PerformChanged(entry, auditActivator, changes, ref auditFlag, ref canStore, ref finalChanges, hasStorage, currentDateTime);
                    }

                    break;
                }

                case EntityState.Added:
                {
                    PerformCreated(entry, auditActivator, ref auditFlag, ref canStore, hasStorage, currentDateTime);
                    break;
                }
            }

            if (canStore)
            {
                if (hasStorage && auditFlag.HasValue)
                {
                    var auditStorage = (IAuditStorage)auditActivator;
                    var audit = new Audit
                    {
                        DateTime = currentDateTime,
                        Flag = auditFlag.Value,
                        User = auditUser,
                        Changes = finalChanges.ToArray(),
                    };
                    var audits = AppendAudit(auditStorage, audit);
                    auditStorage.Audits = JsonSerializer.SerializeToElement(audits, _options.JsonOptions);

                    return;
                }

                _logger.LogDebug(AuditEventId.NotAuditable, "Entity {EntityName} with state {EntityState} does not implemented by {Auditable}. So it will be ignored while is not auditable", entry.EntityType.Name, entry.State, nameof(IAuditStorage));
            }
        }

        internal Audit[] AppendAudit(IAuditStorage entityAuditable, Audit audit)
        {
            Span<Audit> newAudits;
            if (entityAuditable.Audits != null)
            {
                Span<Audit> existingAudits = entityAuditable.Audits.Value.Deserialize<Audit[]>(_options.JsonOptions);
                if (_options.MaxStoredAudits is > 0 && existingAudits.Length >= _options.MaxStoredAudits.Value)
                {
                    newAudits = new Audit[_options.MaxStoredAudits.Value];
                    var startIndex = 0;
                    if (existingAudits[0].Flag == AuditFlag.Created)
                    {
                        if (_options.MaxStoredAudits is 1)
                        {
                            // In this scenario, we have only one audit and it is created.
                            // So we cannot add new audit to the list.
                            throw new InvalidOperationException("Max stored audits cannot be 1 when the first audit has Created flag.");
                        }

                        startIndex = 1;
                        newAudits[0] = existingAudits[0];
                    }

                    startIndex = existingAudits.Length - _options.MaxStoredAudits.Value + startIndex;
                    var adjustment = existingAudits[0].Flag == AuditFlag.Created ? 0 : 1;
                    for (var i = startIndex + 1; i < existingAudits.Length; i++)
                    {
                        var index = i - startIndex;
                        newAudits[index - adjustment] = existingAudits[i];
                    }
                }
                else
                {
                    newAudits = new Audit[existingAudits.Length + 1];
                    existingAudits.CopyTo(newAudits);
                }

                newAudits[^1] = audit;
            }
            else
            {
                newAudits = new Audit[1];
                newAudits[0] = audit;
            }

            return newAudits.ToArray();
        }

        private void PerformCreated(IEntityEntry entry, IAuditActivator auditActivator, ref AuditFlag? auditFlag, ref bool canStore, bool isStorage, DateTime currentDateTime)
        {
            if (_options.AuditFlagSupport.Created.HasFlag(AuditFlagState.ActionDate))
            {
                if (auditActivator is IAuditCreateDate cd)
                    cd.CreateDate = currentDateTime;
            }

            auditFlag = AuditFlag.Created;
            _logger.LogDebug(AuditEventId.Created, "Entity {EntityName} is marked at {AuditFlag}", entry.EntityType.Name, auditFlag);
            canStore = isStorage && _options.AuditFlagSupport.Created.HasFlag(AuditFlagState.Storage);
        }

        private void PerformChanged(IEntityEntry entry, IAuditActivator auditActivator, Memory<AuditChange> changes, ref AuditFlag? auditFlag, ref bool canStore, ref Memory<AuditChange> finalChanges, bool isStorage, DateTime currentDateTime)
        {
            if (_options.AuditFlagSupport.Changed.HasFlag(AuditFlagState.ActionDate))
            {
                if (auditActivator is IAuditUpdateDate ud)
                    ud.UpdateDate = currentDateTime;
                if (auditActivator is IAuditDeleteDate dd)
                    dd.DeleteDate = null;
            }

            if (_options.AuditFlagSupport.Changed.HasFlag(AuditFlagState.Storage))
            {
                if (!isStorage)
                    return;

                if (changes.Length == 0)
                {
                    _logger.LogDebug(AuditEventId.NoChangesFound, "Entity {EntityName} with state {EntityState} has no changes", entry.EntityType.Name, entry.State);
                    return;
                }

                auditFlag = AuditFlag.Changed;
                finalChanges = changes;
                _logger.LogDebug(AuditEventId.Changed, "Entity {EntityName} is marked as {AuditFlag}", entry.EntityType.Name, auditFlag);
            }
            else
            {
                canStore = false;
            }
        }

        private void PerformDeleteUndelete(IEntityEntry entry, IAuditActivator auditActivator, bool deleted, ref AuditFlag? auditFlag, ref bool canStore, bool isStorage, DateTime currentDateTime)
        {
            if (deleted)
            {
                auditFlag = AuditFlag.Deleted;

                if (_options.AuditFlagSupport.Deleted.HasFlag(AuditFlagState.ActionDate))
                {
                    if (auditActivator is IAuditDeleteDate dd)
                        dd.DeleteDate = currentDateTime;
                }

                canStore = isStorage && _options.AuditFlagSupport.Deleted.HasFlag(AuditFlagState.Storage);
            }
            else
            {
                auditFlag = AuditFlag.UnDeleted;

                if (_options.AuditFlagSupport.UnDeleted.HasFlag(AuditFlagState.ActionDate))
                {
                    if (auditActivator is IAuditDeleteDate dd)
                        dd.DeleteDate = null;
                    if (auditActivator is IAuditUpdateDate ud)
                        ud.UpdateDate = currentDateTime;
                }

                canStore = isStorage && _options.AuditFlagSupport.UnDeleted.HasFlag(AuditFlagState.Storage);
            }

            _logger.LogDebug(auditFlag == AuditFlag.Deleted ? AuditEventId.Deleted : AuditEventId.UnDeleted, "Entity {EntityName} is marked as {AuditFlag}", entry.EntityType.Name, auditFlag);
        }

        private (bool? Deleted, Memory<AuditChange> Changed) GetChangedPropertyEntries(PropertyEntry[] propertyEntries, bool hasAuditStorage)
        {
            Memory<AuditChange> memory = new AuditChange[propertyEntries.Length];
            var lastIndex = -1;
            bool? deleted = null;
            foreach (var propertyEntry in propertyEntries)
            {
                if (!propertyEntry.IsModified)
                    continue;

                var propertyType = propertyEntry.Metadata.ClrType;
                if (propertyEntry.Metadata.PropertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() != null)
                    continue;

                var propertyName = propertyEntry.Metadata.Name;
                if (propertyName.Equals(nameof(IAuditStorage.Audits), StringComparison.Ordinal) ||
                    propertyName.Equals(nameof(IAuditCreateDate.CreateDate), StringComparison.Ordinal) ||
                    propertyName.Equals(nameof(IAuditUpdateDate.UpdateDate), StringComparison.Ordinal) ||
                    propertyName.Equals(nameof(IAuditDeleteDate.DeleteDate), StringComparison.Ordinal))
                    continue;

                var currentNull = propertyEntry.CurrentValue is null;
                var originalNull = propertyEntry.OriginalValue is null;
                if ((currentNull && originalNull) || propertyEntry.CurrentValue?.Equals(propertyEntry.OriginalValue) == true)
                    continue;

                if (string.Equals(propertyName, nameof(IAuditSoftDelete.IsDeleted), StringComparison.Ordinal))
                {
                    var oldValue = !originalNull && (bool)propertyEntry.OriginalValue!;
                    var newValue = !currentNull && (bool)propertyEntry.CurrentValue!;
                    if (oldValue == false && newValue == true)
                        deleted = true;
                    else if (oldValue == true && newValue == false)
                        deleted = false;
                    else
                        deleted = null;

                    continue;
                }

                if (!hasAuditStorage)
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

                    if (currentEnumerator is IDisposable ced) ced.Dispose();
                    if (originalEnumerator is IDisposable oed) oed.Dispose();
                }

                var newString = GetValue(propertyEntry.CurrentValue, propertyType, currentNull);
                var oldString = GetValue(propertyEntry.OriginalValue, propertyType, originalNull);

                var auditChange = new AuditChange(propertyName, oldString, newString);
                memory.Span[++lastIndex] = auditChange;
            }

            var array = memory[..(lastIndex + 1)];
            return (deleted, array);
        }

        private JsonElement? GetValue(object? value, Type propertyType, bool isNull)
        {
            if (isNull)
                return null;

            JsonElement? newString;
            if (value is JsonDocument jsonDoc)
            {
                newString = jsonDoc.RootElement.Clone();
            }
            else
            {
                newString = JsonSerializer.SerializeToElement(value, propertyType, _options.JsonOptions);
            }

            return newString;
        }
    }
}