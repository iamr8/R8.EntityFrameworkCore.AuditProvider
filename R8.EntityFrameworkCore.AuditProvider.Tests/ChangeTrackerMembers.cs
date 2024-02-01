using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace R8.EntityFrameworkCore.AuditProvider.Tests
{
    public class ChangeTrackerMembers<TDbContext, TEntity> : List<MemberEntry> where TDbContext : DbContext where TEntity : class
    {
        private readonly TDbContext _dbContext;
        private readonly TEntity _entity;

        public ChangeTrackerMembers(TDbContext dbContext, TEntity entity, IEnumerable<MemberEntry> entries) : base(entries)
        {
            _dbContext = dbContext;
            _entity = entity;
        }

        public void Update<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression, TProperty newValue)
        {
            var entry = _dbContext.Entry(_entity);

            var entityType = _dbContext.Model.FindEntityType(_entity.GetType());
            var entityProps = entityType.GetProperties().ToArray();
            var entityNavs = entityType.GetNavigations().ToArray();

            var property = (PropertyInfo)propertyExpression.GetMemberAccess();

            if (entityProps.Any(x => x.Name.Equals(property.Name)))
            {
                var propEntry = entry.Property(property.Name);
                propEntry.IsModified = true;
                propEntry.OriginalValue = property.GetValue(_entity);
                propEntry.CurrentValue = newValue;
            }
            else if (entityNavs.Any(x => x.Name.Equals(property)))
            {
                if (property.PropertyType.GetInterfaces().Any(x => x == typeof(IEnumerable)))
                {
                    var propEntry = entry.Collection(property.Name);
                    propEntry.IsModified = true;
                    propEntry.CurrentValue = (IEnumerable)newValue;
                }
                else
                {
                    var propEntry = entry.Reference(property.Name);
                    propEntry.IsModified = true;
                    propEntry.CurrentValue = property.GetValue(_entity);
                }
            }

            // entity.GetType().GetProperty(propertyExpression.GetMemberAccess().Name)!.SetValue(entity, newValue);
        }
    }
}