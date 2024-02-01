namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditSoftDelete"/> interface that enables an entity to be soft-deletable.
    /// </summary>
    /// <remarks>This interface takes effect only when <see cref="IAuditActivator"/> is implemented.</remarks>
    public interface IAuditSoftDelete
    {
        /// <summary>
        /// Gets a value indicating whether current entity is soft-deleted or not.
        /// </summary>
        bool IsDeleted { get; set; }
    }
}