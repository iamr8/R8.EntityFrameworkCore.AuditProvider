using System.Text.Json;
using System.Text.Json.Serialization;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Options for <see cref="EntityFrameworkAuditProviderInterceptor"/>.
    /// </summary>
    public class EntityFrameworkAuditProviderOptions
    {
        /// <summary>
        /// A default json serializer options to be used in serialization and deserialization of audits.
        /// </summary>
        /// <remarks>An optimal settings is already set.</remarks>
        public JsonSerializerOptions JsonOptions { get; } = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        /// <summary>
        /// [EXPERIMENTAL] Adding ability to store stack-trace in audit or not. Default value is <c>false</c>.
        /// </summary>
        public bool IncludeStackTrace { get; set; }

        /// <summary>
        /// A list of strings that represents namespaces to be ignored from stack trace. Default value is <c>System</c> and <c>Microsoft</c>.
        /// </summary>
        /// <remarks>This property is only used when <see cref="IncludeStackTrace"/> is set to <c>true</c>.</remarks>
        public IList<string> ExcludedNamespacesInStackTrace { get; } = new List<string>
        {
            "System",
            "Microsoft"
        };
        
        /// <summary>
        /// A <see cref="Func{TResult}"/> that represents a method to get current user.
        /// </summary>
        public Func<IServiceProvider, EntityFrameworkAuditUser>? UserProvider { get; set; }
    }
}