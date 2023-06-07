using System.Reflection;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace R8.EventSourcing.PostgreSQL
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
            var entries = eventData.Context?.ChangeTracker.Entries().ToArray();
            if (entries == null || !entries.Any())
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var changedEntries = entries.Where(x => x.State != EntityState.Detached && x.State != EntityState.Unchanged).ToArray();
            if (!changedEntries.Any())
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            foreach (var entry in changedEntries)
            {
                var propertyEntries = GetPropertyEntries(entry);
                if (propertyEntries.Length == 0 || entry.State is not (EntityState.Added or EntityState.Deleted or EntityState.Modified))
                {
                    _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.Entity.GetType().Name, entry.State);
                    continue;
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

                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.Entity.GetType().Name, entry.State);
                    }
                }
                else
                {
                    if (entry.State == EntityState.Deleted)
                    {
                        // Delete permanently
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is deleted permanently", entry.Entity.GetType().Name, entry.State);
                        continue;
                    }
                }

                if (entity is not IAuditable entityAuditable)
                {
                    _logger.LogDebug("Entity {EntityName} with state {EntityState} is not auditable", entry.Entity.GetType().Name, entry.State);
                    continue;
                }

                var audit = new Audit { DateTime = DateTime.UtcNow };

                switch (entry.State)
                {
                    case EntityState.Modified:
                    {
                        var changes = GetChangedPropertyEntries(entry);
                        if (!changes.Any())
                        {
                            _logger.LogDebug("No changes found for {EntityName} with state {EntityState}", entry.Entity.GetType().Name, entry.State);
                            continue;
                        }

                        var deleteInChanges = changes.FirstOrDefault(x => x.Key == nameof(IAuditableDelete.IsDeleted));
                        if (deleteInChanges.HasValue)
                        {
                            var old = deleteInChanges.OldValue != null && bool.Parse(deleteInChanges.OldValue);
                            var @new = deleteInChanges.NewValue != null && bool.Parse(deleteInChanges.NewValue);
                            audit.Flag = old switch
                            {
                                false when @new == true => AuditFlags.Deleted,
                                true when @new == false => AuditFlags.UnDeleted,
                            };

                            changes = changes.Where(x => x.Key != nameof(IAuditableDelete.IsDeleted)).ToArray();
                            if (changes.Any())
                            {
                                _logger.LogWarning("Cannot delete/undelete and update at the same time");
                                throw new NotSupportedException("Cannot delete/undelete and update at the same time.");
                            }

                            _logger.LogDebug("Entity {EntityName} with state {EntityState} is marked as deleted", entry.Entity.GetType().Name, entry.State);
                        }
                        else
                        {
                            audit.Flag = AuditFlags.Changed;
                            audit.Changes = changes;
                            _logger.LogDebug("Entity {EntityName} with state {EntityState} is changed", entry.Entity.GetType().Name, entry.State);
                        }

                        break;
                    }

                    case EntityState.Added:
                    {
                        audit.Flag = AuditFlags.Created;
                        _logger.LogDebug("Entity {EntityName} with state {EntityState} is created", entry.Entity.GetType().Name, entry.State);
                        break;
                    }

                    default:
                    {
                        _logger.LogDebug("State {EntityState} is not supported", entry.State);
                        continue;
                    }
                }

                var audits = entityAuditable.Audits != null
                    ? entityAuditable.Audits.Deserialize<List<Audit>>()
                    : new List<Audit>();
                audits.Add(audit);

                entityAuditable.Audits = JsonSerializer.SerializeToDocument(audits, AuditJsonSettings.Settings);
                _logger.LogDebug("Entity {EntityName} with state {EntityState} is audited", entry.Entity.GetType().Name, entry.State);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
        }

        private PropertyEntry[] GetPropertyEntries(EntityEntry entityEntry)
        {
            var exclusions = new List<string> { nameof(IAuditable.Audits) };

            if (_options.IgnoredColumns != null && _options.IgnoredColumns.Any())
                exclusions.AddRange(_options.IgnoredColumns);

            var propertyEntries = entityEntry.Members
                .Where(x => !exclusions.Contains(x.Metadata.Name) && x is PropertyEntry)
                .Cast<PropertyEntry>()
                .ToArray();
            return propertyEntries;
        }

        private AuditChange[] GetChangedPropertyEntries(EntityEntry entityEntry)
        {
            var propertyEntries = GetPropertyEntries(entityEntry);
            var changes = Enumerable.Empty<AuditChange>();
            var changeHandlers = _options.ChangeHandlers;

            foreach (var propertyEntry in propertyEntries)
            {
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

                if (changeHandlers.Any(x => x.CanHandle(propertyEntry.Metadata.ClrType)))
                {
                    var changeHandler = changeHandlers.First(x => x.CanHandle(propertyEntry.Metadata.ClrType));
                    if (!changeHandler.Handle(propertyEntry.CurrentValue, propertyEntry.OriginalValue, AuditJsonSettings.Settings))
                        continue;
                    
                    newString = changeHandler.NewValue;
                    oldString = changeHandler.OldValue;
                }
                else
                {
                    newString = propertyEntry.CurrentValue?.ToString();
                    oldString = propertyEntry.OriginalValue?.ToString();
                }

                var auditChange = new AuditChange(propertyName, oldString, newString);
                changes = changes.Concat(new[] { auditChange });
            }

            return changes.ToArray();
        }
    }
}