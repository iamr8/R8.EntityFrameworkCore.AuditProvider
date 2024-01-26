using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public abstract record AggregateAuditable : IAuditable, IAuditableDelete
    {
        public bool IsDeleted { get; set; }

        [Column(TypeName = "jsonb"), IgnoreAudit]
        public JsonElement? Audits { get; set; }
    }
}