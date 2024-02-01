using System.Text.Json;
using System.Text.Json.Serialization;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    public class AuditProviderOptions
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
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false,
        };

        /// <summary>
        /// A <see cref="AuditFlag"/> value that represents included flags to be audited.
        /// </summary>
        /// <remarks>Default: All flags are included.</remarks>
        public AuditProviderFlagSupport AuditFlagSupport { get; } = new()
        {
            Created = AuditFlagState.ActionDate | AuditFlagState.Storage,
            Changed = AuditFlagState.ActionDate | AuditFlagState.Storage,
            Deleted = AuditFlagState.ActionDate | AuditFlagState.Storage,
            UnDeleted = AuditFlagState.ActionDate | AuditFlagState.Storage,
        };
        
        /// <summary>
        /// A <see cref="int"/> value that represents maximum number of audits to be stored. Audit with flag <see cref="AuditFlag.Created"/> remains as the first audit (if provided).
        /// </summary>
        public int? MaxStoredAudits { get; set; }
        
        /// <summary>
        /// A <see cref="Func{TResult}"/> that represents a method to get current date time.
        /// </summary>
        public Func<IServiceProvider, DateTime>? DateTimeProvider { get; set; }
        
        /// <summary>
        /// A <see cref="Func{TResult}"/> that represents a method to get current user.
        /// </summary>
        public Func<IServiceProvider, AuditProviderUser?>? UserProvider { get; set; }
    }
}