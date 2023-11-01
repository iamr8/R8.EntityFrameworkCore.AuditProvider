namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="IAuditableDelete"/> interface that enables an entity to be soft-deletable.
    /// </summary>
    public interface IAuditableDelete
    {
        /// <summary>
        /// Gets or sets a value indicating whether current entity is deleted or not.
        /// </summary>
        bool IsDeleted { get; set; }
    }
}