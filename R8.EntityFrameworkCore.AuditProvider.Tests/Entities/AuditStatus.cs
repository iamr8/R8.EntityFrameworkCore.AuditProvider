namespace R8.EntityFrameworkCore.AuditProvider.Tests.Entities
{
    // Used to exercise the interceptor's value-converter path: mapped to a string column via an explicit
    // ValueConverter, so a change must be audited as "Active"/"Inactive", not the numeric enum value.
    public enum AuditStatus
    {
        Active,
        Inactive
    }
}
