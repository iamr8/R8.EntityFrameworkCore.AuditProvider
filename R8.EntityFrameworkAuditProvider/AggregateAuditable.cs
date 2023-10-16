using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Text.Json;

namespace R8.EntityFrameworkAuditProvider
{
    public abstract record AggregateAuditable : IAuditable, IAuditableDelete
    {
        private bool _disposed;
        
        public bool IsDeleted { get; set; }

        [Column(TypeName = "jsonb")]
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public JsonDocument? Audits { get; set; }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual void Dispose()
        {
            if (_disposed)
                return;
            
            Audits?.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// Returns an array of <see cref="Audit"/> that represents audits.
        /// </summary>
        /// <returns>An array of <see cref="Audit"/> instances deserialized from <see cref="Audits"/>.</returns>
        /// <exception cref="NullReferenceException">Thrown when <see cref="Audits"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when object is disposed.</exception>
        public Audit[] GetAudits()
        {
            if (this._disposed)
                throw new ObjectDisposedException(nameof(AggregateAuditable));
            
            if (Audits == null)
                throw new NullReferenceException(nameof(Audits));

            var audits = this.Audits.Deserialize<Audit[]>(EntityFrameworkAuditProviderOptions.JsonStaticOptions);
            return audits!;
        }
    }
}