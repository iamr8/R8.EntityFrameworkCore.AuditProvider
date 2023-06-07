using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Text.Json;

namespace R8.EventSourcing.PostgreSQL
{
    public abstract record AggregateAuditable : IAuditable, IAuditableDelete
    {
        public bool IsDeleted { get; set; }

        [Column(TypeName = "jsonb")]
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public JsonDocument? Audits { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void Dispose() => Audits?.Dispose();

        public Audit[]? GetAudits()
        {
            if (Audits == null)
                return Array.Empty<Audit>();

            var audits = this.Audits.Deserialize<Audit[]>(AuditJsonSettings.DefaultSettings);
            return audits;
        }
    }
}