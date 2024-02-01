namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="AuditFlag"/> enum that represents the audit flags.
    /// </summary>
    public enum AuditFlag : ushort
    {
        /// <summary>
        /// This will be used when the entity is created.
        /// </summary>
        Created = 0,

        /// <summary>
        /// This will be used when the entity is updated.
        /// </summary>
        Changed = 1,

        /// <summary>
        /// This will be used when the entity is soft-deleted.
        /// </summary>
        Deleted = 2,

        /// <summary>
        /// This will be used when the entity is restored after soft-deletion.
        /// </summary>
        UnDeleted = 3
    }
}