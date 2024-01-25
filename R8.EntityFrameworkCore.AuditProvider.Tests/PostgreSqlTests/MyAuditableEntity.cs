using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests;

public record MyAuditableEntity : AggregateAuditable, IAggregateEntity
{
    [Key] public int Id { get; set; }
    public string? Name { get; set; }
    [IgnoreAudit] public string? LastName { get; set; }
    public List<int> ListOfIntegers { get; set; } = new();
    public List<string> ListOfStrings { get; set; } = new();
    public List<long>? NullableListOfLongs { get; set; } = new();
    public double[] ArrayOfDoubles { get; set; } = Array.Empty<double>();
    public double Double { get; set; }
    public DateTime Date { get; set; }
    public DateTimeOffset DateOffset { get; set; }
    public JsonDocument? Payload { get; set; }
    public int? NullableInt { get; set; }
        
    public int? MyAuditableEntityId { get; set; }
    public virtual MyAuditableEntity? Parent { get; set; }
    public virtual ICollection<MyAuditableEntity> Children { get; set; } = new List<MyAuditableEntity>();
    public virtual ICollection<MyEntity> MyEntities { get; set; } = new List<MyEntity>();
}