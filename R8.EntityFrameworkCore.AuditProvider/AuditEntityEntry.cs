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
            Entity = _entry.Entity;
            EntityType = _entry.Metadata.ClrType;
            Members = _entry.Members.OfType<PropertyEntry>().ToArray();
        }
    
        public EntityState State
        {
            get => _entry.State;
            set => _entry.State = value;
        }

        public object Entity { get; }
        public Type EntityType { get; }
        public PropertyEntry[] Members { get; }
        public void DetectChanges() => _entry.DetectChanges();
    }
}