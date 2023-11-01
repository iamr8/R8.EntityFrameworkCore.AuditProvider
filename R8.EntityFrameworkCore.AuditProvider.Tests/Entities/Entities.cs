using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.Entities
{
    public interface IAggregateEntity
    {
        int Id { get; set; }
    }

    public record MyAuditableEntity : AggregateAuditable, IAggregateEntity
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        [IgnoreAudit] public string? LastName { get; set; }
        public List<int> ListOfIntegers { get; set; } = new();
        public List<string> ListOfStrings { get; set; } = new();
        public double[] ArrayOfDoubles { get; set; } = Array.Empty<double>();
        public DateTime Date { get; set; }
        public DateTimeOffset DateOffset { get; set; }
        public JsonDocument Payload { get; set; }
        public int? NullableInt { get; set; }
        public virtual ICollection<MyEntity> RelationalEntities { get; set; } = new List<MyEntity>();
    }

    public record MyEntity : IAggregateEntity
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        
        public int MyAuditableEntityId { get; set; }
        public MyAuditableEntity MyAuditableEntity { get; set; }
    }
}