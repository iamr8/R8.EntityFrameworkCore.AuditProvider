using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public record MyAuditableEntityWithSoftDeleteWithoutActivator : IAggregateEntity, IAuditSoftDelete
    {
        [Key, Column(TypeName = "serial")]
        public int Id { get; set; }

        public string Name { get; set; }
        
        public bool IsDeleted { get; set; }
    }
}