# R8.EntityFrameworkCore.AuditProvider

A .NET package for Entity Framework, providing comprehensive change tracking with deep insights. Capture creation, updates, deletions, and restorations of entities, including property names, old and new values, stack traces, and user details, all neatly stored in an Audits column as JSON.

**Seamless Entity Auditing:** Easily integrate audit functionality into your Entity Framework applications, offering a complete audit trail enriched with stack traces and user information. Gain full visibility into entity lifecycle changes for compliance, debugging, and accountability.

**Full Entity Lifecycle Visibility:** Track and visualize the complete life cycle of your entities with detailed auditing. In addition to changes, this package records the stack trace of changes and user actions, enabling a deeper understanding of data evolution and robust audit trails.

[![Nuget](https://img.shields.io/nuget/vpre/R8.EntityFrameworkCore.AuditProvider)](https://www.nuget.org/packages/R8.EntityFrameworkCore.AuditProvider/) ![Nuget](https://img.shields.io/nuget/dt/R8.EntityFrameworkCore.AuditProvider) ![Commit](https://img.shields.io/github/last-commit/iamr8/R8.EntityFrameworkCore.AuditProvider)

### Installation

```csharp
// ... other services

// Add AuditProvider
services.AddEntityFrameworkAuditProvider(options =>
{
    options.JsonOptions.WriteIndented = false;
    
    options.AuditFlagSupport.Created = AuditFlagState.ActionDate | AuditFlagState.Storage;
    options.AuditFlagSupport.Changed = AuditFlagState.ActionDate | AuditFlagState.Storage;
    options.AuditFlagSupport.Deleted = AuditFlagState.ActionDate | AuditFlagState.Storage;
    options.AuditFlagSupport.UnDeleted = AuditFlagState.ActionDate | AuditFlagState.Storage;
    
    options.MaxStoredAudits = 10;
    
    options.UserProvider = serviceProvider =>
    {
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = user.FindFirstValue(ClaimTypes.Name);
            return new AuditProviderUser(userId, new Dictionary<string, object>
            {
                { "Username", username }
            });
        }
        return null;
    };
});

services.AddDbContext<YourDbContext>((serviceProvider, optionsBuilder) =>
{
    // Your DbContext connection configuration here
    // ...
    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
});
```

---

### Options

| Option             | Type                                                             | Description                                                 | Default                |
|--------------------|------------------------------------------------------------------|-------------------------------------------------------------|------------------------|
| `JsonOptions`      | `System.Text.Json.JsonSerializerOptions`                         | Json serializer options to serialize and deserialize audits | An optimal setting     |
| `AuditFlagSupport` | ` R8.EntityFrameworkCore.AuditProvider.AuditProviderFlagSupport` | Audit flags to include                                      | All flags are included |
| `MaxStoredAudits`* | `int?`                                                           | Maximum number of audits to store in `Audits` column        | `null`                 |
| `DateTimeProvider` | `Func<IServiceProvider, DateTime>`                               | DateTime provider to get current date time                  | `DateTime.UtcNow`      |
| `UserProvider`     | `Func<IServiceProvider, EntityFrameworkAuditUser>`               | User provider to get current user id                        | `null`                 |

* If the number of audits exceeds this number, the earliest audits (except `Created`) will be removed from the column. If `null`, all audits will be stored.

---

### Implementation

- `IAuditActivator` interface: to start auditing entities.
- `IAuditStorage` interface: to store audits in a column.
- `IAuditSoftDelete` interface: to soft-delete entities.
- `IAuditCreateDate` interface: to store creation date in a column.
- `IAuditUpdateDate` interface: to store last update/restore date in a column.
- `IAuditDeleteDate` interface: to store deletion date in a column.
- `[AuditIgnore]` attribute: to ignore a property from audit.

---

### Samples:
- `PostgreSQL`: [AggregateAuditable.cs](https://github.com/iamr8/R8.EntityFrameworkCore.AuditProvider/blob/master/R8.EntityFrameworkCore.AuditProvider.Tests/PostgreSqlTests/Entities/AggregateAuditable.cs)
- `Microsoft Sql Server`: [AggregateAuditable.cs](https://github.com/iamr8/R8.EntityFrameworkCore.AuditProvider/blob/master/R8.EntityFrameworkCore.AuditProvider.Tests/MsSqlTests/Entities/AggregateAuditable.cs)
- or as below (for `PostgreSQL`):
```csharp
public record YourEntity : IAuditActivator, IAuditStorage, IAuditSoftDelete, IAuditCreateDate, IAuditUpdateDate, IAuditDeleteDate
{
    [Key]
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
    
    // ...
    // public string Name { get; set; }
    // public string Description { get; set; }
    // etc.
}
```

---
### Migration

_Highly recommended to test it on a test database first, to avoid any data loss._

---

### Considerations

- Since `Microsoft Sql Server` does not support `json` type, `Audits` column will be stored as `nvarchar(max)` and `JsonElement` will be serialized/deserialized to/from `string`. (See [AggregateAuditable.cs](https://github.com/iamr8/R8.EntityFrameworkCore.AuditProvider/blob/master/R8.EntityFrameworkCore.AuditProvider.Tests/MsSqlTests/AggregateAuditable.cs))
- The key to **allow auditing entities** is implementation of `IAuditActivator` to your entity.
  - the `IAuditStorage`, `IAuditSoftDelete`, `IAuditCreateDate`, `IAuditUpdateDate`, and `IAuditDeleteDate` interfaces takes effect only if `IAuditActivator` is implemented to entity. If not implemented, the entity will be updated with the proper `SaveChanges`/`SaveChangesAsync` functionality in `Entity Framework Core`.
- `Deleted` and `UnDeleted` flag cannot be stored simultaneously with `Created` and `Changed` flags.
- If `IAuditStorage` is implemented to your entity, `Audits` column will be stored in the specified table.
- If any of `IAuditCreateDate`, `IAuditUpdateDate` or `IAuditDeleteDate` is implemented to entity, the corresponding date will be stored **among** the `Audits` column (of `IAuditStorage` interface) update.
- Any support flag in `AuditProviderOptions.AuditFlagSupport` must be written as a flag: `AuditFlagState.ActionDate | AuditFlagState.Storage`
  - If any of `AuditFlag` enums are included/excluded from `AuditFlagSupport`, the corresponding flag will take action in `Audits` and/or `{Action}Date` column according to the its state in `AuditFlagSupport`. _(For instance, if `AuditFlagSupport.Created = AuditProviderFlagSupport.Excluded`, `IAuditCreateDate` and `IAuditStorage`, also and `Created` flag will be ignored.)_

---

### Some useful methods

To take advantages of `JsonElement Audits` property, you can easily convert it to `AuditCollection`:

```csharp
var audits = (AuditCollection)entity.Audits.Value; // Cast to AuditCollection
// or
// var audits = AuditCollection.Parse(entity.Audits.Value);

JsonElement jsonElement = audits.Element; // Get underlying JsonElement

Audit[] deserializedAudits = audits.Deserialize(); // Get deserialized audits
Audit creationAudit = audits.GetCreated(); // Get created audit
Audit lastUpdatedAudit = audits.GetLast(includedDeleted: false); // Get last audit
```

---

### Output Example

Stored data in `Audits` column will be like this:

```json5
[
  {
    "f": "0",
    // Created
    "dt": "2023-09-25T12:00:00.0000000+03:30",
  },
  {
    "f": "1",
    // Updated
    "dt": "2023-09-25T12:00:00.0000000+03:30",
    "c": [
      {
        "n": "Name",
        // Name of the property
        "_v": "OldName",
        // Old value
        "v": "NewName"
        // New value
      }
    ],
    "u": {
      "id": "1",
      // The user id (if provided)
      "ad": {
        // The user additional info (if provided)
        "Username": "Foo"
      }
    }
  },
  {
    "f": "2",
    // Deleted
    "dt": "2023-09-25T12:00:00.0000000+03:30",
  },
  {
    "f": "3",
    // Restored/Undeleted
    "dt": "2023-09-25T12:00:00.0000000+03:30",
  }
]
```

---
**🎆 Happy coding!**
