using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace R8.EventSourcing.PostgreSQL
{
    public static class AuditJsonSettings
    {
        /// <summary>
        /// A <see cref="JsonSerializerOptions"/> that used for <see cref="Audit"/> serialization.
        /// </summary>
        public static JsonSerializerOptions Settings
        {
            get
            {
                var settings = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    ReferenceHandler = ReferenceHandler.IgnoreCycles,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode,
                    DefaultBufferSize = 1024,
                    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
                };

                // Your custom converters goes here:
                // settings.Converters.Add(new JsonCultureToStringConverter());

                return settings;
            }
        }
    }
}