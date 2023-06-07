using System.Text.Json;
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
        public List<string> ExcludedColumns { get; } = new()
        {
            nameof(IAuditable.Audits)
        };

        /// <summary>
        /// A list of <see cref="IAuditChangeHandler"/> to handle changes in audit for specific types.
        /// </summary>
        /// <remarks>Default handlers are <see cref="AuditListChangeHandler"/> and <see cref="AuditDateTimeChangeHandler"/>.</remarks>
        public List<IAuditChangeHandler> ChangeHandlers { get; } = new()
        {
            new AuditListChangeHandler(),
            new AuditDateTimeChangeHandler()
        };
        
        /// <summary>
        /// Gets or sets a <see cref="JsonSerializerOptions"/> that used for <see cref="Audit"/> serialization.
        /// </summary>
        /// <remarks>An optimal settings is already set.</remarks>
        public JsonSerializerOptions JsonOptions { get; set; } = AuditJsonSettings.DefaultSettings;
    }
}