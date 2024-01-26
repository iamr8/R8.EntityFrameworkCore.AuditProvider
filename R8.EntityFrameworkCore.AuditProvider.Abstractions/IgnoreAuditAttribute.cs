namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// Ignores the property from being tracked by Audit Interceptor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreAuditAttribute : Attribute
    {
    }
}