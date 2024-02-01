using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

public class MyAuditableEntityWithoutSoftDelete : IAggregateEntity, IAuditStorage
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    [Column(TypeName = "jsonb"), AuditIgnore]
    public JsonElement? Audits { get; set; }
}