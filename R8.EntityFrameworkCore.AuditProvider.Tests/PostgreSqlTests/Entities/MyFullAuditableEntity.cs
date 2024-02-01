using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities
{
    public record MyFullAuditableEntity : IAggregateEntity, IAuditActivator, IAuditStorage, IAuditSoftDelete, IAuditCreateDate, IAuditUpdateDate, IAuditDeleteDate
    {
        [Key, Column(TypeName = "serial")]
        public int Id { get; set; }

        [Column(TypeName = "jsonb"), AuditIgnore]
        public JsonElement? Audits { get; set; }

        public bool IsDeleted { get; set; }
        
        [Column("CreatedAt", TypeName = "timestamp")]
        public DateTime? CreateDate { get; set; }
        
        [Column("UpdatedAt", TypeName = "timestamp")]
        public DateTime? UpdateDate { get; set; }
        
        [Column("DeletedAt", TypeName = "timestamp")]
        public DateTime? DeleteDate { get; set; }
    }
}