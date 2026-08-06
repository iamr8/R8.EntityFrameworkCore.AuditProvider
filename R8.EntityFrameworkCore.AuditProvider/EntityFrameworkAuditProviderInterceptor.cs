using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
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

        // Framework-owned columns that the provider manages itself; a change to them is never audited as
        // a property change. ("Audits" covers both IAuditStorage and IAuditJsonStorage — same name.)
        private static readonly string[] IgnoredChangedProperties =
        {
            nameof(IAuditStorage.Audits),
            nameof(IAuditCreateDate.CreateDate),
            nameof(IAuditUpdateDate.UpdateDate),
            nameof(IAuditDeleteDate.DeleteDate)
        };

        // Starting capacity for the per-save change buffer, rented from ArrayPool. It grows (never throws)
        // if an entity has more audited property changes than this, so there is no hard cap.
        private const int InitialChangeBufferCapacity = 16;

        /// <summary>
        /// Initializes a new instance of the <see cref="EntityFrameworkAuditProviderInterceptor"/> class.
        /// </summary>
        /// <param name="options">The audit provider options.</param>
        /// <param name="serviceProvider">The service provider used to resolve the date-time and user providers.</param>
        /// <param name="logger">The logger.</param>
        public EntityFrameworkAuditProviderInterceptor(AuditProviderOptions options, IServiceProvider serviceProvider, ILogger<EntityFrameworkAuditProviderInterceptor> logger)
        {
            _options = options;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context == null)
                return base.SavingChangesAsync(eventData, result, cancellationToken);

            // Enumerate only auditable entities (typed) instead of materializing every tracked entry.
            // One wrapper instance is reused across all entries in this SaveChanges — the loop is
            // synchronous and single-threaded, so the (stateless) interceptor stays thread-safe.
            AuditEntityEntry? pooledEntry = null;
            foreach (var entry in context.ChangeTracker.Entries<IAuditActivator>())
            {
                if (entry.State is not (EntityState.Added or EntityState.Deleted or EntityState.Modified))
                    continue;

                pooledEntry ??= new AuditEntityEntry();
                pooledEntry.SetEntry(entry);
                AckAudits(pooledEntry, context);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        [DebuggerStepThrough]
        internal void AckAudits(IEntityEntry entry, DbContext? dbContext)
        {
            if (entry.State is not (EntityState.Added or EntityState.Deleted or EntityState.Modified))
                return;

            using var logScope = _logger.BeginScope("Storing audit for {EntityName} with state {EntityState}", entry.EntityType.Name, entry.State);
            var entity = entry.Entity;
            if (entity is not IAuditActivator auditActivator)
                return;

            var currentDateTime = _options.DateTimeProvider?.Invoke(_serviceProvider) ?? DateTime.UtcNow;

            var hasStorage = auditActivator is IAuditStorageBase;
            AuditUser? auditUser = null;
            AuditFlag? auditFlag = null;
            AuditChange[]? finalChanges = null;
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
                        foreach (var memberEntry in entry.Members)
                        {
                            if (memberEntry is not PropertyEntry propertyEntry)
                                continue;
                            if (propertyEntry.OriginalValue is not bool isDeleted)
                                continue;

                            // Already soft-deleted: don't record another Deleted audit.
                            if (propertyEntry.Metadata.Name.Equals(nameof(IAuditSoftDelete.IsDeleted), StringComparison.Ordinal) && isDeleted)
                                return;
                        }

                        // Set deleted flag
                        entitySoftDelete.IsDeleted = true;
                        entry.DetectChanges();
                        entry.State = EntityState.Modified;

                        PerformDeleteUndelete(entry, auditActivator, deleted: true, ref auditFlag, ref canStore, hasStorage, currentDateTime);
                    }

                    break;
                }
                case EntityState.Modified:
                {
                    // Rent a change buffer from the pool; it is grown (never a fixed cap) as changes are
                    // found and returned once the changes have been copied into the audit.
                    var pool = ArrayPool<AuditChange>.Shared;
                    var buffer = pool.Rent(InitialChangeBufferCapacity);
                    var count = 0;
                    try
                    {
                        var deleted = GetChangedPropertyEntries(entry.Members, hasStorage, ref buffer, ref count);
                        if (deleted.HasValue)
                        {
                            if (count > 0)
                                throw new NotSupportedException("Cannot delete/undelete and update at the same time.");

                            if (auditActivator is IAuditSoftDelete softDelete)
                                softDelete.IsDeleted = deleted.Value;

                            PerformDeleteUndelete(entry, auditActivator, deleted.Value, ref auditFlag, ref canStore, hasStorage, currentDateTime);
                        }
                        else
                        {
                            PerformChanged(entry, auditActivator, new ArraySegment<AuditChange>(buffer, 0, count), ref auditFlag, ref canStore, ref finalChanges, hasStorage, currentDateTime);
                        }
                    }
                    finally
                    {
                        pool.Return(buffer, clearArray: true);
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
                    var auditStorage = (IAuditStorageBase)auditActivator;
                    var audit = new Audit
                    {
                        DateTime = currentDateTime,
                        Flag = auditFlag.Value,
                        User = auditUser,
                        Changes = finalChanges,
                    };
                    var audits = AppendAudit(auditStorage, audit);

                    switch (auditStorage)
                    {
                        case IAuditJsonStorage jsonStorage:
                        {
                            jsonStorage.Audits = JsonSerializer.SerializeToElement(audits, _options.JsonOptions);
                            break;
                        }
                        case IAuditStorage arrayStorage:
                        {
                            arrayStorage.Audits = audits;
                            break;
                        }
                    }

                    return;
                }

                if (!hasStorage)
                {
                    _logger.NotAuditable(entry.EntityType.Name, entry.State, nameof(IAuditJsonStorage));
                }
            }
        }

        internal Audit[] AppendAudit(IAuditStorageBase entityAuditable, Audit audit)
        {
            Span<Audit> newAudits;
            Span<Audit> existingAudits;
            switch (entityAuditable)
            {
                case IAuditJsonStorage { Audits: not null } jsonStorage:
                {
                    // The full audit array is deserialized on each append because audits are persisted as a
                    // single JSON column: appending requires reading the existing value first. The cost is
                    // bounded by AuditProviderOptions.MaxStoredAudits, which caps how many audits are kept.
                    existingAudits = jsonStorage.Audits.Value.Deserialize<Audit[]>(_options.JsonOptions);
                    break;
                }
                case IAuditJsonStorage:
                {
                    newAudits = new Audit[1];
                    newAudits[0] = audit;
                    return newAudits.ToArray();
                }
                case IAuditStorage { Audits.Length: > 0 } arrayStorage:
                {
                    existingAudits = arrayStorage.Audits;
                    break;
                }
                case IAuditStorage:
                {
                    newAudits = new Audit[1];
                    newAudits[0] = audit;
                    return newAudits.ToArray();
                }
                default:
                {
                    throw new NotSupportedException("Entity does not implemented by IAuditJsonStorage or IAuditStorage.");
                }
            }

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
            _logger.Created(entry.EntityType.Name, auditFlag);
            canStore = isStorage && _options.AuditFlagSupport.Created.HasFlag(AuditFlagState.Storage);
        }

        private void PerformChanged(IEntityEntry entry, IAuditActivator auditActivator, ArraySegment<AuditChange> changes, ref AuditFlag? auditFlag, ref bool canStore, ref AuditChange[]? finalChanges, bool isStorage, DateTime currentDateTime)
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

                if (changes.Count == 0)
                {
                    _logger.NoChangesFound(entry.EntityType.Name, entry.State);
                    return;
                }

                auditFlag = AuditFlag.Changed;
                // Copy out of the pooled buffer into a right-sized array kept on the audit; the pooled
                // buffer is returned by the caller once this method returns.
                finalChanges = changes.ToArray();
                _logger.Changed(entry.EntityType.Name, auditFlag);
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

            if (auditFlag == AuditFlag.Deleted)
                _logger.Deleted(entry.EntityType.Name, auditFlag);
            else
                _logger.UnDeleted(entry.EntityType.Name, auditFlag);
        }

        // Writes detected changes into the rented `buffer` (growing it via the pool when full — never a
        // fixed cap), advancing `count`. Returns the soft-delete transition, if any.
        private bool? GetChangedPropertyEntries(IEnumerable<MemberEntry> memberEntries, bool hasAuditStorage, ref AuditChange[] buffer, ref int count)
        {
            bool? deleted = null;
            foreach (var memberEntry in memberEntries)
            {
                if (memberEntry is not PropertyEntry propertyEntry)
                    continue;
                if (!propertyEntry.IsModified)
                    continue;

                var metadata = propertyEntry.Metadata;
                var propertyName = metadata.Name;
                var originalValue = propertyEntry.OriginalValue;
                var currentValue = propertyEntry.CurrentValue;

                if (string.Equals(propertyName, nameof(IAuditSoftDelete.IsDeleted), StringComparison.Ordinal))
                {
                    deleted = (originalValue, currentValue) switch
                    {
                        (false, true) => true,  // Not deleted -> Deleted
                        (true, false) => false, // Deleted -> Undeleted
                        _ => null
                    };

                    continue;
                }

                // No change per EF's provider-aware value comparer (handles value objects, converted
                // types, byte arrays, etc.), with an element-wise fallback for collection properties.
                if (metadata.GetValueComparer().Equals(originalValue, currentValue))
                    continue;
                if (originalValue is IEnumerable ov && currentValue is IEnumerable cv && ov.Cast<object>().SequenceEqual(cv.Cast<object>()))
                    continue;

                if (metadata.PropertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() != null || IgnoredChangedProperties.Contains(propertyName, StringComparer.Ordinal))
                    continue;

                if (!hasAuditStorage)
                    continue;

                var newString = GetValue(metadata, currentValue);
                var oldString = GetValue(metadata, originalValue);

                // Skip when the values serialize identically (e.g. different JsonDocument instances with
                // the same content) — the comparer above is CLR-level and would not catch that.
                if (newString.HasValue && oldString.HasValue && IsEqual(newString.Value, oldString.Value))
                    continue;

                if (count == buffer.Length)
                {
                    // Grow: rent a larger buffer, copy, and return the old one to the pool.
                    var pool = ArrayPool<AuditChange>.Shared;
                    var larger = pool.Rent(buffer.Length * 2);
                    Array.Copy(buffer, larger, count);
                    pool.Return(buffer, clearArray: true);
                    buffer = larger;
                }

                buffer[count++] = new AuditChange(propertyName, oldString, newString);
            }

            return deleted;
        }

        internal static bool IsEqual(JsonElement first, JsonElement second)
        {
#if NET8_0_OR_GREATER
            // System.Text.Json 9+ (referenced on net8.0 and net10.0) ships a canonical deep comparison.
            // It is order-insensitive for objects, order-sensitive for arrays, normalizes numbers, and —
            // unlike the manual walk below — handles numbers outside the decimal range without throwing.
            return JsonElement.DeepEquals(first, second);
#else
            switch (first.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    if (second.ValueKind != JsonValueKind.Object)
                        return false;

                    var firstObjs = first.EnumerateObject();
                    var secondObjs = second.EnumerateObject();

                    int firstCount = 0, secondCount = 0;

                    while (firstObjs.MoveNext()) firstCount++;
                    while (secondObjs.MoveNext()) secondCount++;

                    if (firstCount != secondCount)
                        return false;

                    firstObjs = first.EnumerateObject(); // Re-enumerate
                    secondObjs = second.EnumerateObject(); // Re-enumerate

                    while (firstObjs.MoveNext())
                    {
                        var firstProp = firstObjs.Current;
                        if (!second.TryGetProperty(firstProp.Name, out var secondProp) ||
                            !IsEqual(firstProp.Value, secondProp))
                        {
                            return false;
                        }
                    }

                    if (firstObjs is IDisposable firstObjsDisposable) firstObjsDisposable.Dispose();
                    if (secondObjs is IDisposable secondObjsDisposable) secondObjsDisposable.Dispose();

                    return true;
                }
                case JsonValueKind.Array:
                {
                    if (second.ValueKind != JsonValueKind.Array)
                        return false;

                    var array1 = first.EnumerateArray();
                    var array2 = second.EnumerateArray();

                    int firstArrayCount = 0, secondArrayCount = 0;

                    while (array1.MoveNext()) firstArrayCount++;
                    while (array2.MoveNext()) secondArrayCount++;

                    if (firstArrayCount != secondArrayCount)
                        return false;

                    array1 = first.EnumerateArray(); // Re-enumerate
                    array2 = second.EnumerateArray(); // Re-enumerate

                    while (array1.MoveNext() && array2.MoveNext())
                    {
                        if (!IsEqual(array1.Current, array2.Current))
                            return false;
                    }

                    if (array1 is IDisposable array1Disposable) array1Disposable.Dispose();
                    if (array2 is IDisposable array2Disposable) array2Disposable.Dispose();

                    return true;
                }
                case JsonValueKind.String:
                {
                    if (second.ValueKind != JsonValueKind.String)
                        return false;

                    return first.ValueEquals(second.GetString());
                }
                case JsonValueKind.Number:
                {
                    if (second.ValueKind != JsonValueKind.Number)
                        return false;

                    return first.GetDecimal() == second.GetDecimal();
                }
                case JsonValueKind.True:
                case JsonValueKind.False:
                {
                    return first.GetBoolean() == second.GetBoolean();
                }
                case JsonValueKind.Null:
                {
                    return second.ValueKind == JsonValueKind.Null;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
#endif
        }

        private JsonElement? GetValue(IProperty metadata, object? value)
        {
            if (value is null)
                return null;

            // Respect EF value converters so the audit records what the provider actually stores
            // (e.g. an enum mapped to a string), not the raw CLR value.
            var converter = metadata.GetValueConverter();
            if (converter != null)
            {
                var converted = converter.ConvertToProvider(value);
                if (converted is null)
                    return null;

                return JsonSerializer.SerializeToElement(converted, converter.ProviderClrType, _options.JsonOptions);
            }

            if (value is JsonDocument jsonDoc)
                return jsonDoc.RootElement.Clone();

            return JsonSerializer.SerializeToElement(value, metadata.ClrType, _options.JsonOptions);
        }
    }
}