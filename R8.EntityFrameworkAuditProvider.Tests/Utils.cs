using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace R8.EntityFrameworkAuditProvider.Tests;

public static class Utils
{
    public static PropertyEntry<TEntity, TProperty> GetPropertyEntryWithNewValue<TDbContext, TEntity, TProperty>(this TDbContext dbContext, TEntity entity, Expression<Func<TEntity, TProperty>> propertyExpression, TProperty newValue) where TDbContext : DbContext where TEntity : class
    {
        var propEntry = dbContext.Entry(entity).Property(propertyExpression);
        propEntry.OriginalValue = propertyExpression.Compile().Invoke(entity);
        propEntry.CurrentValue = newValue;
        entity.GetType().GetProperty(propertyExpression.GetMemberAccess().Name)!.SetValue(entity, newValue);
        return propEntry;
    }
    
    public static PropertyEntry<TEntity, TProperty> GetPropertyEntry<TDbContext, TEntity, TProperty>(this TDbContext dbContext, TEntity entity, Expression<Func<TEntity, TProperty>> propertyExpression) where TDbContext : DbContext where TEntity : class
    {
        var propEntry = dbContext.Entry(entity).Property(propertyExpression);
        propEntry.OriginalValue = propertyExpression.Compile().Invoke(entity);
        propEntry.CurrentValue = propEntry.OriginalValue;
        return propEntry;
    }
}