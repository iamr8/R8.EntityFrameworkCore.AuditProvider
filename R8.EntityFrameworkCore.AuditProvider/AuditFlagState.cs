using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    [Flags]
    public enum AuditFlagState
    {
        /// <summary>
        /// Will be excluded from entire audit process.
        /// </summary>
        Excluded = 0,
        
        /// <summary>
        /// Will be included only when relevant interface is implemented. (e.g. <see cref="IAuditCreateDate"/>, <see cref="IAuditUpdateDate"/>, <see cref="IAuditDeleteDate"/>)
        /// </summary>
        ActionDate = 1,
        
        /// <summary>
        /// Will be included only when <see cref="IAuditStorage"/> is implemented.
        /// </summary>
        Storage = 2,
    }
}