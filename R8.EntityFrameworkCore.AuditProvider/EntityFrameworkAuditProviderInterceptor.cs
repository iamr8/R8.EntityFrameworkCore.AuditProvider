using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
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
        private readonly AuditProviderOptions _options;
        private readonly ILogger<EntityFrameworkAuditProviderInterceptor> _logger;

        public EntityFrameworkAuditProviderInterceptor(AuditProviderOptions options, ILogger<EntityFrameworkAuditProviderInterceptor> logger)
        {
            _options = options;
            _logger = logger;
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
                    var audit = await StoringAuditAsync(auditEntry, eventData.Context, cancellationToken).ConfigureAwait(false);
                }
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
                    AdditionalData = user.AdditionalData,
                };
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

                        if (audit.Flag == AuditFlag.Deleted && !_options.IncludedFlags.Contains(AuditFlag.Deleted))
                            return false;
                        
                        if (audit.Flag == AuditFlag.UnDeleted && !_options.IncludedFlags.Contains(AuditFlag.UnDeleted))
                            return false;
                        
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.EntityType.Name, entry.State);
                    }
                    else
                    {
                        if (changes.Length == 0)
                        {
                            _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.EntityType.Name, entry.State);
                            return false;
                        }

                        if (!_options.IncludedFlags.Contains(AuditFlag.Changed))
                            return false;
                        
                        audit.Flag = AuditFlag.Changed;
                        audit.Changes = changes.ToArray();
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is changed", entry.EntityType.Name, entry.State);
                    }

                    break;
                }

                case EntityState.Added:
                {
                    if (!_options.IncludedFlags.Contains(AuditFlag.Created))
                        return false;
                    
                    audit.Flag = AuditFlag.Created;
                    _logger.LogDebug("Entity {EntityName} with state {EntityState} is created", entry.EntityType.Name, entry.State);
                    break;
                }

                default:
                    throw new NotSupportedException($"State {entry.State} is not supported.");
            }

            var audits = GetAudits(entityAuditable, audit);
            entityAuditable.Audits = JsonSerializer.SerializeToElement(audits, _options.JsonOptions);

            _logger.LogDebug("Entity {EntityName} with state {EntityState} is audited", entry.EntityType.Name, entry.State);
            return true;
        }

        internal Audit[] GetAudits(IAuditable entityAuditable, Audit audit)
        {
            Memory<Audit> newAudits;
            if (entityAuditable.Audits != null)
            {
                Memory<Audit> existingAudits = entityAuditable.Audits.Value.Deserialize<Audit[]>(_options.JsonOptions);
                if (_options.MaxStoredAudits is > 0 && existingAudits.Length >= _options.MaxStoredAudits.Value)
                {
                    newAudits = new Audit[_options.MaxStoredAudits.Value];
                    var startIndex = 0;
                    if (existingAudits.Span[0].Flag == AuditFlag.Created)
                    {
                        startIndex = 1;
                        newAudits.Span[0] = existingAudits.Span[0];
                    }
                    
                    startIndex = existingAudits.Length - _options.MaxStoredAudits.Value + startIndex;
                    var adjustment = existingAudits.Span[0].Flag == AuditFlag.Created ? 0 : 1;
                    for (var i = startIndex + 1; i < existingAudits.Length; i++)
                    {
                        var index = i - startIndex;
                        newAudits.Span[index - adjustment] = existingAudits.Span[i];
                    }
                    newAudits.Span[^1] = audit;
                }
                else
                {
                    newAudits = new Audit[existingAudits.Length + 1];
                    existingAudits.CopyTo(newAudits);
                    newAudits.Span[^1] = audit;
                }
            }
            else
            {
                newAudits = new Audit[1];
                newAudits.Span[0] = audit;
            }
            
            return newAudits.ToArray();
        }

        private (bool? Deleted, Memory<AuditChange> Changed) GetChangedPropertyEntries(ICollection<PropertyEntry> propertyEntries)
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

                    if (currentEnumerator is IDisposable ced) ced.Dispose();
                    if (originalEnumerator is IDisposable oed) oed.Dispose();
                }

                if (string.Equals(propertyName, nameof(IAuditableDelete.IsDeleted), StringComparison.Ordinal))
                {
                    var oldValue = !originalNull && (bool)propertyEntry.OriginalValue!;
                    var newValue = !currentNull && (bool)propertyEntry.CurrentValue!;
                    deleted = oldValue switch
                    {
                        false when newValue => true,
                        true when !newValue => false,
                        _ => null
                    };
                    continue;
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