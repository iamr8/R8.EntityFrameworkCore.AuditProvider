using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace R8.EntityFrameworkCore.AuditProvider
{
    // Reusable wrapper over EntityEntry. A single instance is pooled per SaveChanges and re-pointed at
    // each entry via SetEntry, avoiding a per-entity allocation. Members is exposed lazily (no array copy).
    internal class AuditEntityEntry : IEntityEntry
    {
        private EntityEntry _entry = null!;

        public void SetEntry(EntityEntry entry) => _entry = entry;

        public EntityState State
        {
            get => _entry.State;
            set => _entry.State = value;
        }

        public object Entity => _entry.Entity;
        public Type EntityType => _entry.Metadata.ClrType;
        public IEnumerable<MemberEntry> Members => _entry.Members;
        public void DetectChanges() => _entry.DetectChanges();
    }
}
