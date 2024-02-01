using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public abstract record AggregateAuditable : IAuditActivator, IAuditStorage, IAuditSoftDelete
    {
        public bool IsDeleted { get; set; }

        [Column(TypeName = "jsonb"), AuditIgnore]
        public JsonElement? Audits { get; set; }
    }
}