using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public record MyAuditableEntityWithUpdateDate : AggregateAuditable, IAggregateEntity, IAuditUpdateDate
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? UpdateDate { get; set; }
    }
}