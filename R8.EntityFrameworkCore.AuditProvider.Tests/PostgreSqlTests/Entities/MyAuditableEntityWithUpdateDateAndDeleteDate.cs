using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public record MyAuditableEntityWithUpdateDateAndDeleteDate : AggregateAuditable, IAggregateEntity, IAuditUpdateDate, IAuditDeleteDate
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public DateTime? UpdateDate { get; set; }
        public DateTime? DeleteDate { get; set; }
    }
}