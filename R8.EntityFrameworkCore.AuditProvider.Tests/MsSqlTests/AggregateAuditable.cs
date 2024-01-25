using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests
{
    public abstract record AggregateAuditable : IAuditable, IAuditableDelete
    {
        public bool IsDeleted { get; set; }

        [Column(nameof(Audits), TypeName = "nvarchar(max)")]
        [IgnoreAudit]
        public string? AuditsJson { get; set; }

        [NotMapped]
        public JsonElement? Audits
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AuditsJson))
                    return null;
                
                var json = JsonSerializer.Deserialize<JsonElement>(AuditsJson, AuditStatic.JsonStaticOptions);
                return json;
            }
            
            set
            {
                if (value == null)
                {
                    AuditsJson = null;
                    return;
                }

                AuditsJson = value.Value.GetRawText();
            }
        }

        /// <summary>
        /// Returns an array of <see cref="Audit"/> that represents audits.
        /// </summary>
        /// <returns>An array of <see cref="Audit"/> instances deserialized from <see cref="Audits"/>.</returns>
        /// <exception cref="NullReferenceException">Thrown when <see cref="Audits"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when object is disposed.</exception>
        public Audit[] GetAudits()
        {
            if (Audits == null)
                throw new NullReferenceException(nameof(Audits));

            var audits = this.Audits.Value.Deserialize<Audit[]>(AuditStatic.JsonStaticOptions);
            return audits!;
        }
    }
}