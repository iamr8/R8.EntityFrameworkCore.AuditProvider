namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditCreateDate"/> interface that enables an entity to store update/restore date.
    /// </summary>
    public interface IAuditUpdateDate
    {
        /// <summary>
        /// Gets the update date of this record.
        /// </summary>
        DateTime? UpdateDate { get; set; }
    }
}