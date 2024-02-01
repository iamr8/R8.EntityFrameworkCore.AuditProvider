namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditDeleteDate"/> interface that enables an entity to store delete date.
    /// </summary>
    public interface IAuditDeleteDate
    {
        /// <summary>
        /// Gets the delete date of this record.
        /// </summary>
        DateTime? DeleteDate { get; set; }
    }
}