namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Ignored the property from being tracked by <see cref="EntityFrameworkAuditProviderInterceptor"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAuditAttribute : Attribute
    {
    }
}