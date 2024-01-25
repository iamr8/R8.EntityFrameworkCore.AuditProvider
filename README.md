# R8.EntityFrameworkCore.AuditProvider
A .NET 6 package for Entity Framework, providing comprehensive change tracking with deep insights. Capture creation, updates, deletions, and restorations of entities, including property names, old and new values, stack traces, and user details, all neatly stored in an Audits column as JSON.

**Seamless Entity Auditing:** Easily integrate audit functionality into your Entity Framework applications, offering a complete audit trail enriched with stack traces and user information. Gain full visibility into entity lifecycle changes for compliance, debugging, and accountability.

**Full Entity Lifecycle Visibility:** Track and visualize the complete life cycle of your entities with detailed auditing. In addition to changes, this package records the stack trace of changes and user actions, enabling a deeper understanding of data evolution and robust audit trails.

### Installation
#### Step 1:
```csharp
// ... other services

// Add AuditProvider
services.AddEntityFrameworkAuditProvider(options =>
{
    // Your options here (See Options section)
});

services.AddDbContext<YourDbContext>((serviceProvider, optionsBuilder) =>
{
    // Your DbContext connection configuration here
    ...
    
    // Add AuditProviderInterceptor
    optionsBuilder.AddEntityFrameworkAuditProviderInterceptor(serviceProvider);
});
```

#### Step 2:
Inherit your entity from `AggregateAuditable` record:
```csharp
public record YourEntity : AggregateAuditable
{
    // Your entity properties
}
```
It will add `Audits` column with `jsonb` type, and `IsDeleted` column with `boolean` type to your entity table.
```csharp
public abstract record AggregateAuditable : IAuditable, IAuditableDelete
{
    // provided by IAuditableDelete
    public bool IsDeleted { get; set; }
    
    // provided by IAuditable
    public JsonDocument? Audits { get; set; }
    
    // provided by IAuditable
    public virtual void Dispose() => Audits?.Dispose();
}
```

#### Step 3:
Migrate your database.

_Highly recommended to test it on a test database first, to avoid any data loss._

---
### Options
`EntityFrameworkAuditProviderOptions`:

| Option                           | Type                                                | Description                                                              | Default               |
|----------------------------------|-----------------------------------------------------|--------------------------------------------------------------------------|-----------------------|
| `JsonOptions`                    | `System.Text.Json.JsonSerializerOptions`            | Json serializer options to serialize and deserialize audits              | An optimal setting    |
| `IncludeStackTrace`              | `bool`                                              | Include stack trace in audit records                                     | `false`               |
| `ExcludedNamespacesInStackTrace` | `IList<string>`                                     | Namespaces to be excluded from stack trace                               | `System`, `Microsoft` |
| `UserProvider`                   | `Func<IServiceProvider, EntityFrameworkAuditUser>`  | User provider to get current user id                                     | `null`                |

---
### Tests
Currently tested with `Npgsql.EntityFrameworkCore.PostgreSQL 7.x`.

---
### Usage
You can do your stuffs on your entity, as always:
```csharp
var entity = new YourEntity
{
    Name = "SomeValue"
};
YourDbContext.Add(entity);
await YourDbContext.SaveChangesAsync();
```

And then you can get the entity audits like this: `entity.GetAudits()` which returns `Audit[]`.

---
### Output
Stored data in `Audits` column will be like this:
```json5
[
  {
    "f": "0", // Created
    "dt": "2023-09-25T12:00:00.0000000+03:30",
  },
  {
    "f": "1", // Updated
    "dt": "2023-09-25T12:00:00.0000000+03:30",
    "c": [
      {
        "n": "Name", // Name of the property
        "_v": "OldName", // Old value
        "v": "NewName" // New value
      }
    ],
    "u": {
      "id": "1", // The user id (if provided)
      "ad": { // The user additional info (if provided)
        "Username": "Foo"
      }
    },
    "st": // Stack trace (if enabled) [EXPERIMENTAL]
    [
      "Void MoveNext() (R8.EntityFrameworkCore.AuditProvider.Tests.Audit_UnitTests+<should_find_changes_including_stacktrace>d__4)",
      "System.Threading.Tasks.Task should_find_changes_including_stacktrace(Boolean)"
    ]
  },
  {
    "f": "2", // Deleted
    "dt": "2023-09-25T12:00:00.0000000+03:30",
  },
  {
    "f": "3", // Restored/Undeleted
    "dt": "2023-09-25T12:00:00.0000000+03:30",
  }
]
```
You can add some `JsonConverter`s to fit your needs. (e.g. `DateTimeToUnix`)

---
**🎆 Happy coding!**