namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditCreateDate"/> interface that enables an entity to store creation date.
    /// </summary>
    public interface IAuditCreateDate
    {
        /// <summary>
        /// Gets the creation date of this record.
        /// </summary>
        DateTime? CreateDate { get; set; }
    }
}