using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Entities
{
    public abstract record AggregateAuditable : IAuditActivator, IAuditStorage, IAuditSoftDelete
    {
        public bool IsDeleted { get; set; }

        [NotMapped, AuditIgnore]
        public Audit[]? Audits { get; set; }
    }
}