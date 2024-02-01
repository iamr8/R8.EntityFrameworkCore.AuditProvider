using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public record MyAuditableEntityWithCreateDate : AggregateAuditable, IAggregateEntity, IAuditCreateDate
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? CreateDate { get; set; }
    }
}