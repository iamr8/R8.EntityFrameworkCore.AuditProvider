using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Entities
{
    public abstract record AggregateAuditable : IAuditable, IAuditableDelete
    {
        public bool IsDeleted { get; set; }

        [Column(nameof(Audits), TypeName = "nvarchar(max)"), IgnoreAudit]
        public string? AuditsJson { get; set; }

        [NotMapped]
        public JsonElement? Audits
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AuditsJson))
                    return null;
                
                var json = JsonSerializer.Deserialize<JsonElement>(AuditsJson, AuditProviderConfiguration.JsonOptions);
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
    }
}