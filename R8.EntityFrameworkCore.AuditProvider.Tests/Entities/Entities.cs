using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.Entities
{
    public interface IAggregateEntity
    {
        int Id { get; set; }
    }

    public record FirstAuditableEntity : AggregateAuditable, IAggregateEntity
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        [IgnoreAudit] public string? LastName { get; set; }
        public List<int> ArrayOfIntegers { get; set; } = new();
        public virtual ICollection<SecondAuditableEntity> SecondEntities { get; set; } = new List<SecondAuditableEntity>();
    }

    public record SecondAuditableEntity : AggregateAuditable, IAggregateEntity
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public List<string> ListOfStrings { get; set; } = new();

        public int? FirstEntityId { get; set; }
        public virtual FirstAuditableEntity? FirstEntity { get; set; }
    }

    public record ThirdEntity : IAggregateEntity
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
    }
}