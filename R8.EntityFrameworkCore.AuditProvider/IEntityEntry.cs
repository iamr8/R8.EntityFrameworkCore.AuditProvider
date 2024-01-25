using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace R8.EntityFrameworkCore.AuditProvider
{
    internal interface IEntityEntry
    {
        /// <inheritdoc cref="EntityEntry.State"/>
        EntityState State { get; set; }
    
        /// <inheritdoc cref="EntityEntry.Entity"/>
        object Entity { get; }
    
        /// <inheritdoc cref="EntityEntry.Members"/>
        IEnumerable<MemberEntry> Members { get; }

        /// <inheritdoc cref="IReadOnlyTypeBase.ClrType"/>
        Type EntityType { get; }

        /// <inheritdoc cref="EntityEntry.DetectChanges"/>
        void DetectChanges();
    
        /// <inheritdoc cref="EntityEntry.ReloadAsync"/>
        Task ReloadAsync(CancellationToken cancellationToken = default);
    }
}