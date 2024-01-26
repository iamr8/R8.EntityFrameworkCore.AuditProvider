using System.ComponentModel.DataAnnotations;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

public record MyEntity : IAggregateEntity
{
    [Key] public int Id { get; set; }
    public string? Name { get; set; }
        
    public int MyAuditableEntityId { get; set; }
    public MyAuditableEntity? MyAuditableEntity { get; set; }
}