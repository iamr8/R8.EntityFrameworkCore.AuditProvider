using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// Holds shared configuration for the audit provider, such as the JSON serializer options used for audits.
    /// </summary>
    public static class AuditProviderConfiguration
    {
        private static JsonSerializerOptions? _jsonOptions;

        /// <summary>
        /// Gets the <see cref="JsonSerializerOptions"/> that is used to serialize and deserialize audit data.
        /// </summary>
        public static JsonSerializerOptions? JsonOptions
        {
            get => _jsonOptions;
            internal set
            {
                if (_jsonOptions != null)
                    return;
                
                _jsonOptions = value;
            }
        }
    }
}