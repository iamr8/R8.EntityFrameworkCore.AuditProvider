using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
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