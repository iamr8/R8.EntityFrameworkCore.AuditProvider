using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace R8.EntityFrameworkCore.AuditProvider
{
    internal class AuditEntityEntry : IEntityEntry
    {
        private readonly EntityEntry _entry;

        public AuditEntityEntry(EntityEntry entry)
        {
            _entry = entry;
        }
    
        public EntityState State
        {
            get => _entry.State;
            set => _entry.State = value;
        }

        public object Entity => _entry.Entity;
        public Type EntityType => _entry.Metadata.ClrType;
        public IEnumerable<MemberEntry> Members => _entry.Members.Cast<MemberEntry>();
        public void DetectChanges() => _entry.DetectChanges();
        public Task ReloadAsync(CancellationToken cancellationToken = default) => _entry.ReloadAsync(cancellationToken);
    }
}