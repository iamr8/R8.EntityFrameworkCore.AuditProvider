using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public record MyAuditableEntityWithDeleteDate : AggregateAuditable, IAggregateEntity, IAuditDeleteDate
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? DeleteDate { get; set; }
    }
}