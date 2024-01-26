using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

public class MyAuditableEntityWithoutSoftDelete : IAggregateEntity, IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    [Column(TypeName = "jsonb")]
    [IgnoreAudit]
    public JsonElement? Audits { get; set; }

    /// <summary>
    /// Returns an array of <see cref="Audit"/> that represents audits.
    /// </summary>
    /// <returns>An array of <see cref="Audit"/> instances deserialized from <see cref="Audits"/>.</returns>
    /// <exception cref="NullReferenceException">Thrown when <see cref="Audits"/> is null.</exception>
    public Audit[] GetAudits()
    {
        if (Audits == null)
            throw new NullReferenceException(nameof(Audits));

        var audits = this.Audits.Value.Deserialize<Audit[]>(AuditStatic.JsonStaticOptions);
        return audits!;
    }
}