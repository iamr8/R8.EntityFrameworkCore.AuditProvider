using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace R8.EntityFrameworkCore.AuditProvider.Tests;

public static class Utils
{
    public static ChangeTrackerMembers<TDbContext, TEntity> GetChangeTrackerMembers<TDbContext, TEntity>(this TEntity model, TDbContext dbContext) where TDbContext : DbContext where TEntity : class
    {
        var typeProperties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var entries = new MemberEntry[typeProperties.Length];
        var entry = dbContext.Entry(model);

        var entityType = dbContext.Model.FindEntityType(model.GetType());
        var entityProps = entityType.GetProperties().ToArray();
        var entityNavs = entityType.GetNavigations().ToArray();

        for (var index = 0; index < typeProperties.Length; index++)
        {
            var property = typeProperties[index];
            if (entityProps.Any(x => x.Name.Equals(property.Name)))
            {
                var propEntry = entry.Property(property.Name);
                propEntry.IsModified = false;
                propEntry.OriginalValue = property.GetValue(model);
                propEntry.CurrentValue = propEntry.OriginalValue;
                entries[index] = propEntry;
            }
            else if (entityNavs.Any(x => x.Name.Equals(property.Name)))
            {
                if (property.PropertyType.GetInterfaces().Any(x => x == typeof(IEnumerable)))
                {
                    var propEntry = entry.Collection(property.Name);
                    propEntry.IsModified = false;
                    propEntry.CurrentValue = (IEnumerable)property.GetValue(model)!;
                    entries[index] = propEntry;
                }
                else
                {
                    var propEntry = entry.Reference(property.Name);
                    propEntry.IsModified = false;
                    propEntry.CurrentValue = property.GetValue(model);
                    entries[index] = propEntry;
                }
            }
        }

        return new ChangeTrackerMembers<TDbContext, TEntity>(dbContext, model, entries);
    }
}