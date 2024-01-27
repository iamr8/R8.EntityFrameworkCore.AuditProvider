# R8.EntityFrameworkCore.AuditProvider

A .NET package for Entity Framework, providing comprehensive change tracking with deep insights. Capture creation, updates, deletions, and restorations of entities, including property names, old and new values, stack traces, and user details, all neatly stored in an Audits column as JSON.

**Seamless Entity Auditing:** Easily integrate audit functionality into your Entity Framework applications, offering a complete audit trail enriched with stack traces and user information. Gain full visibility into entity lifecycle changes for compliance, debugging, and accountability.

**Full Entity Lifecycle Visibility:** Track and visualize the complete life cycle of your entities with detailed auditing. In addition to changes, this package records the stack trace of changes and user actions, enabling a deeper understanding of data evolution and robust audit trails.

### Installation

#### Step 1:

```csharp
// ... other services

// Add AuditProvider
services.AddEntityFrameworkAuditProvider(options =>
{
    options.JsonOptions.WriteIndented = false;
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
| Option             | Type                                               | Description                                                 | Default            |
|--------------------|----------------------------------------------------|-------------------------------------------------------------|--------------------|
| `JsonOptions`      | `System.Text.Json.JsonSerializerOptions`           | Json serializer options to serialize and deserialize audits | An optimal setting |
| `MaxStoredAudits`* | `int?`                                             | Maximum number of audits to store in `Audits` column        | `null`             |
| `UserProvider`     | `Func<IServiceProvider, EntityFrameworkAuditUser>` | User provider to get current user id                        | `null`             |
If the number of audits exceeds this number, the earliest audits (except `Created`) will be removed from the column. If `null`, all audits will be stored.

#### Step 2:

- Implement `IAuditable` (and `IAuditableDelete`) in your `Aggregate Auditable Entity` _(Or you can implement `IAuditable`/`IAuditableDelete` in your `Entity` directly)_:
    - Example for `PostgreSQL`: [AggregateAuditable.cs](https://github.com/iamr8/R8.EntityFrameworkCore.AuditProvider/blob/master/R8.EntityFrameworkCore.AuditProvider.Tests/PostgreSqlTests/AggregateAuditable.cs)
    - Example for `Microsoft Sql Server`: [AggregateAuditable.cs](https://github.com/iamr8/R8.EntityFrameworkCore.AuditProvider/blob/master/R8.EntityFrameworkCore.AuditProvider.Tests/MsSqlTests/AggregateAuditable.cs)
- then inherit your entity from `AggregateAuditable`:

---
```csharp
public record YourEntity : AggregateAuditable
{
    // Your entity properties
    // ...
    public string FirstName { get; set; }
    
    [IgnoreAudit] // Ignore this property from audit
    public string LastName { get; set; }
    
    // ...
}
```
- `Deleted` and `UnDeleted` flags would be stored, only if `IAuditableDelete` implemented.
- Flags `Created`/`Changed` cannot be done simultaneously with `Deleted`/`UnDeleted`.

#### Step 3:

Migrate your database.

_Highly recommended to test it on a test database first, to avoid any data loss._

#### Step 4:
To take advantages of `JsonElement Audits` property, you can easily cast it to `AuditCollection`:
```csharp
var audits = (AuditCollection)entity.Audits.Value; // Cast to AuditCollection
    
JsonElement jsonElement = audits.Element; // Get underlying JsonElement

Audit[] deserializedAudits = audits.Deserialize(); // Get deserialized audits
Audit creationAudit = audits.GetCreated(); // Get created audit
Audit lastUpdatedAudit = audits.GetLast(includedDeleted: false); // Get last audit
```

---

### Output

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