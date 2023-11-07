using Microsoft.EntityFrameworkCore;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    public static class AuditQueryExtensions
    {
        [Obsolete]
        public static DbSet<T> IgnoreAuditing<T>(this DbSet<T> dbSet) where T : AggregateAuditable
        {
            var annotation = dbSet.EntityType.SetRuntimeAnnotation(EntityFrameworkAuditProviderInterceptor.AuditIgnoranceAnnotation, true);
            return dbSet;
        }
    }
}