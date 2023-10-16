using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using R8.EntityFrameworkCore.AuditProvider.Converters;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Options for <see cref="EntityFrameworkAuditProviderInterceptor"/>.
    /// </summary>
    public class EntityFrameworkAuditProviderOptions
    {
        internal static JsonSerializerOptions JsonStaticOptions;

        /// <summary>
        /// Gets or sets an array of <see cref="string"/> that represents columns to be ignored from audit.
        /// </summary>
        public IList<string> ExcludedColumns { get; } = new List<string> { nameof(IAuditable.Audits) };

        /// <summary>
        /// A list of <see cref="IAuditTypeHandler"/> to handle changes in audit for specific clr types.
        /// </summary>
        public IList<IAuditTypeHandler> TypeHandlers { get; } = new List<IAuditTypeHandler>();

        /// <summary>
        /// Gets or sets a <see cref="JsonSerializerOptions"/> that used for <see cref="Audit"/> serialization.
        /// </summary>
        /// <remarks>An optimal settings is already set.</remarks>
        public JsonSerializerOptions JsonOptions => new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        /// <summary>
        /// Gets or sets a <see cref="bool"/> value that represents whether to store stack trace in audit or not. Default value is <c>false</c>.
        /// </summary>
        /// <remarks>Please note that auditing the entity takes more time when this property is set to <c>true</c>.</remarks>
        public bool IncludeStackTrace { get; set; } = false;

        /// <summary>
        /// Gets or sets an array of <see cref="string"/> that represents namespaces to be ignored from stack trace. Default value is <c>System</c> and <c>Microsoft</c>.
        /// </summary>
        /// <remarks>This property is only used when <see cref="IncludeStackTrace"/> is set to <c>true</c>.</remarks>
        public IList<string> ExcludedNamespacesInStackTrace { get; } = new List<string>
        {
            "System",
            "Microsoft"
        };
        
        public Func<IServiceProvider, EntityFrameworkAuditUser> UserProvider { get; set; }
    }
}