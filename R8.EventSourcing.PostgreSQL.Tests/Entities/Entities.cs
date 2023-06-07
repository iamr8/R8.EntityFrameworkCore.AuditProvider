using System.ComponentModel.DataAnnotations;

namespace R8.EventSourcing.PostgreSQL.Tests.Entities
{
    public interface IEntityAggregateRoot
    {
        int Id { get; set; }
    }

    public record FirstEntity : AuditableAggregateRoot, IEntityAggregateRoot
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        [IgnoreAudit] public string? LastName { get; set; }
        public List<int> ArrayOfIntegers { get; set; } = new();
        public virtual ICollection<SecondEntity> SecondEntities { get; set; } = new List<SecondEntity>();
    }

    public record SecondEntity : AuditableAggregateRoot, IEntityAggregateRoot
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
        public DateTime Date { get; set; }
        public List<string> ArrayOfStrings { get; set; } = new();

        public int? FirstEntityId { get; set; }
        public virtual FirstEntity? FirstEntity { get; set; }
    }

    public record ThirdEntity : IEntityAggregateRoot
    {
        [Key] public int Id { get; set; }
        public string Name { get; set; }
    }
}