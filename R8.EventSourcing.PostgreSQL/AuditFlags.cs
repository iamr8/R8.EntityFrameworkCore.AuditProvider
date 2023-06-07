namespace R8.EventSourcing.PostgreSQL
{
    /// <summary>
    /// An <see cref="AuditFlags"/> enum that represents the audit flags.
    /// </summary>
    public enum AuditFlags : ushort
    {
        /// <summary>
        /// This will be used when EntityAuditable is newly created.
        /// </summary>
        Created = 0,

        /// <summary>
        /// This will be used when EntityAuditable is changed.
        /// </summary>
        Changed = 1,

        /// <summary>
        /// This will be used when EntityAuditable is deleted.
        /// </summary>
        Deleted = 2,

        /// <summary>
        /// This will be used when EntityAuditable is undeleted.
        /// </summary>
        UnDeleted = 3
    }
}