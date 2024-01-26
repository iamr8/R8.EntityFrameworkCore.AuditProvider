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
        /// <remarks>Default value is <see cref="AuditFlag.Created"/>, <see cref="AuditFlag.Changed"/>, <see cref="AuditFlag.Deleted"/> and <see cref="AuditFlag.UnDeleted"/>.</remarks>
        public IList<AuditFlag> IncludedFlags { get; } = new List<AuditFlag> { AuditFlag.Created, AuditFlag.Changed, AuditFlag.Deleted, AuditFlag.UnDeleted };
        
        /// <summary>
        /// A <see cref="int"/> value that represents maximum number of audits to be stored.
        /// </summary>
        public int? MaxStoredAudits { get; set; }
        
        /// <summary>
        /// A <see cref="Func{TResult}"/> that represents a method to get current user.
        /// </summary>
        public Func<IServiceProvider, AuditProviderUser?>? UserProvider { get; set; }
    }
}