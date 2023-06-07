using R8.EventSourcing.PostgreSQL.ChangeHandlers;

namespace R8.EventSourcing.PostgreSQL
{
    /// <summary>
    /// Options for <see cref="EntityFrameworkAuditProviderInterceptor"/>.
    /// </summary>
    public class EntityFrameworkAuditProviderOptions
    {
        /// <summary>
        /// Gets or sets an array of <see cref="string"/> that represents columns to be ignored from audit.
        /// </summary>
        public string[]? IgnoredColumns { get; set; }

        /// <summary>
        /// A list of <see cref="IAuditChangeHandler"/> to handle changes in audit for specific types.
        /// </summary>
        public List<IAuditChangeHandler> ChangeHandlers { get; } = new()
        {
            new AuditListChangeHandler(),
            new AuditDateTimeChangeHandler()
        };
    }
}