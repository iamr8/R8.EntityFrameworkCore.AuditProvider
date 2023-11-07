using Microsoft.EntityFrameworkCore;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    public static class AuditQueryExtensions
    {
        public static IQueryable<T> IgnoreAuditing<T>(this IQueryable<T> queryable) where T : AggregateAuditable
        {
            return queryable.TagWith(EntityFrameworkAuditProviderInterceptor.AuditIgnoranceTag);
        }
    }
}