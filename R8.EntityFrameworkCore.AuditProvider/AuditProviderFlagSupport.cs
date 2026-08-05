namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Configures which audit flags are stored for each kind of change (created, changed, deleted, un-deleted).
    /// </summary>
    public class AuditProviderFlagSupport
    {
        internal AuditProviderFlagSupport()
        {
        }

        /// <summary>
        /// Gets or sets the <see cref="AuditFlagState"/> applied when an entity is created.
        /// </summary>
        public AuditFlagState Created { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="AuditFlagState"/> applied when an entity is changed.
        /// </summary>
        public AuditFlagState Changed { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="AuditFlagState"/> applied when an entity is deleted.
        /// </summary>
        public AuditFlagState Deleted { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="AuditFlagState"/> applied when an entity is un-deleted (restored).
        /// </summary>
        public AuditFlagState UnDeleted { get; set; }
    }
}