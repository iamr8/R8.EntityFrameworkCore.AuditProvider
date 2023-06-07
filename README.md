# EventSourcing
Simple Event-sourcing for PostgreSQL database using Entity Framework Core.

### Installation
#### Step 1:
```csharp
// ... other services

// Add AuditProvider
.AddEntityFrameworkAuditProvider(options =>
{
    // Your options 
});

services.AddDbContext<YourDbContext>((serviceProvider, optionsBuilder) =>
{
    // Your Npgsql connection string
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
public abstract record AggregateAuditable : IDisposable
{
    public bool IsDeleted { get; set; }
    public JsonDocument? Audits { get; set; }
    public virtual void Dispose() => Audits?.Dispose();
}
```

#### Step 3:
Migrate your database.

---
### Options
`EntityFrameworkAuditProviderOptions`:

| Option | Description                                                        | Default                                                                       |
| -- |--------------------------------------------------------------------|-------------------------------------------------------------------------------|
| `ExcludedColumns` | Columns to being excluded from auditing                            | `nameof(IAuditable.Audits)`                                             |
| `ChangeHandlers` | Handlers to check if the changed value is eligible to be audited or not | `new AuditListChangeHandler()`, `new AuditDateTimeChangeHandler()` |
| `JsonOptions` | Json serializer options                                            | `AuditJsonSettings.DefaultSettings`                                                            |

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
You can add some json converters to fit your needs. (e.g. `DateTime` to `Unix timestamp`)

---
### Known Issues
`Audit.UserId` not implemented yet!