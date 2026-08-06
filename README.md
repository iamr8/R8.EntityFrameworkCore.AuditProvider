# R8.EntityFrameworkCore.AuditProvider

A .NET package for Entity Framework, providing comprehensive change tracking with deep insights. Capture creation, updates, deletions, and restorations of entities, including property names, old and new values, and user details, all neatly stored in an Audits column as JSON.

**Seamless Entity Auditing:** Easily integrate audit functionality into your Entity Framework applications, offering a complete audit trail enriched with user information. Gain full visibility into entity lifecycle changes for compliance, debugging, and accountability.

**Full Entity Lifecycle Visibility:** Track the complete life cycle of your entities with detailed auditing. For each change this package records the flag (created/changed/deleted/restored), the timestamp, the changed properties with their old and new values, and the user behind the action.

Targets `net6.0`, `net8.0`, and `net10.0`. The interceptor is registered as a thread-safe singleton, so a single registration is safe to share across all your DbContexts.

[![Nuget](https://img.shields.io/nuget/vpre/R8.EntityFrameworkCore.AuditProvider)](https://www.nuget.org/packages/R8.EntityFrameworkCore.AuditProvider/) ![Nuget](https://img.shields.io/nuget/dt/R8.EntityFrameworkCore.AuditProvider) ![Commit](https://img.shields.io/github/last-commit/iamr8/R8.EntityFrameworkCore.AuditProvider)

### Installation

```
dotnet add package R8.EntityFrameworkCore.AuditProvider
```

---

### Known Limitations

The interceptor can only detect changes on **tracked** entities. An entity loaded with `.AsNoTracking()` (or otherwise detached) has no tracked baseline to diff against. To audit an update to such an entity, `Attach` it first and then modify it, so EF captures the original values before the change.

---

### Usage

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
    
    options.DateTimeProvider = serviceProvider => DateTime.UtcNow;
    
    options.UserProvider = serviceProvider =>
    {
        var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = user.FindFirstValue(ClaimTypes.Name);
            return new AuditProviderUser(userId, new Dictionary<string, string>
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
| `UserProvider`     | `Func<IServiceProvider, AuditProviderUser?>`                     | User provider to get current user id                        | `null`                 |

* If the number of audits exceeds this number, the earliest audits (except `Created`) will be removed from the column. If `null`, all audits will be stored.

---

### Wiki

- `IAuditActivator` interface: to start auditing entities.
- `IAuditJsonStorage` interface: to store audits in a single JSON column (`JsonElement? Audits`; e.g. `jsonb` on PostgreSQL, `nvarchar(max)` on SQL Server).
- `IAuditStorage` interface: to store audits as an `Audit[]? Audits` column (serialize/deserialize with `AuditProviderConfiguration.JsonOptions`).
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
public record YourEntity : IAuditActivator, IAuditJsonStorage, IAuditSoftDelete, IAuditCreateDate, IAuditUpdateDate, IAuditDeleteDate
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

- Since `Microsoft Sql Server` does not support `json` type, `Audits` column will be stored as `nvarchar(max)` and `JsonElement` will be serialized/deserialized to/from `string`. (See [AggregateAuditable.cs](https://github.com/iamr8/R8.EntityFrameworkCore.AuditProvider/blob/master/R8.EntityFrameworkCore.AuditProvider.Tests/MsSqlTests/Entities/AggregateAuditable.cs))
- The key to **allow auditing entities** is implementation of `IAuditActivator` to your entity.
  - the `IAuditStorage`, `IAuditSoftDelete`, `IAuditCreateDate`, `IAuditUpdateDate`, and `IAuditDeleteDate` interfaces takes effect only if `IAuditActivator` is implemented to entity. If not implemented, the entity will be updated with the proper `SaveChanges`/`SaveChangesAsync` functionality in `Entity Framework Core`.
- `Deleted` and `UnDeleted` flag cannot be stored simultaneously with `Created` and `Changed` flags.
- If `IAuditStorage` or `IAuditJsonStorage` is implemented to your entity, the `Audits` column will be stored in the specified table.
- If any of `IAuditCreateDate`, `IAuditUpdateDate` or `IAuditDeleteDate` is implemented to entity, the corresponding date will be stored on its own column **alongside** the `Audits` update.
- Any support flag in `AuditProviderOptions.AuditFlagSupport` must be written as a flag: `AuditFlagState.ActionDate | AuditFlagState.Storage`
  - If any of `AuditFlag` enums are included/excluded from `AuditFlagSupport`, the corresponding flag will take action in `Audits` and/or `{Action}Date` column according to the its state in `AuditFlagSupport`. _(For instance, if `AuditFlagSupport.Created = AuditFlagState.Excluded`, `IAuditCreateDate` and `IAuditStorage`, also and `Created` flag will be ignored.)_

---

### Audit Collection

To take advantages of `JsonElement Audits` (as a property in the `IAuditJsonStorage` interface):

```csharp
var entity = await dbContext.YourEntities.FindAsync(1);
var audits = entity.GetAuditCollection();

Audit[] deserializedAudits = audits.ToArray(); // Get audits as array
Audit creationAudit = audits.First(); // Get created audit
Audit lastAudit = audits.Last(false); // Get last audit. (false) means to exclude Deleted flag audit, if is the last one.
Audit[] changes = audits.Track(nameof(entity.Name)); // Get changes of a property
```

---

### Output Example

Stored data in `Audits` column will be like this:

```json5
[
  {
    "f": 0, // Created
    "dt": "2023-09-25T12:00:00.0000000+03:30", // Date and time of the action
  },
  {
    "f": 1, // Changed
    "dt": "2023-09-25T12:00:00.0000000+03:30", // Date and time of the action
    "c": [ // Changes
      {
        "n": "Name", // Name of the property
        "_v": "OldName", // Old value
        "v": "NewName" // New value
      },
      {
        "n": "Age", // Name of the property
        "_v": 0, // Old value
        "v": 33 // New value
      }
    ],
    "u": { // User that made the change
      "id": "1", // The user id (if provided)
      "ad": { // The user additional info (if provided)
        "Username": "Foo"
      }
    }
  },
  {
    "f": 2, // Deleted
    "dt": "2023-09-25T12:00:00.0000000+03:30", // Date and time of the action
  },
  {
    "f": 3, // Restored/Undeleted
    "dt": "2023-09-25T12:00:00.0000000+03:30", // Date and time of the action
  }
]
```

---
**🎆 Happy coding!**