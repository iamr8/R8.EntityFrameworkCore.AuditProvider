using System.Text;
using System.Text.Json.Serialization;

namespace R8.EventSourcing.PostgreSQL
{
    /// <summary>
    /// An object to track creation, modification, and deletion of specific entity.
    /// </summary>
    public record struct Audit
    {
        public const string DateTime_JsonName = "dt";
        public const string Flag_JsonName = "f";
        public const string Changes_JsonName = "c";
        public const string UserId_JsonName = "u";

        /// <summary>
        /// Gets or sets a <see cref="DateTime"/> object that representing when current instance is created.
        /// </summary>
        [JsonPropertyName(DateTime_JsonName)]
        public DateTime DateTime { get; init; }

        /// <summary>
        /// Gets or sets type of <see cref="Audit"/>.
        /// </summary>
        [JsonPropertyName(Flag_JsonName)]
        public AuditFlags Flag { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="List{T}"/> of <see cref="AuditChange"/> that representing changed values.
        /// </summary>
        /// <remarks>This property only works when <see cref="Flag"/> is <see cref="AuditFlags.Changed"/></remarks>
        [JsonPropertyName(Changes_JsonName)]
        public IEnumerable<AuditChange>? Changes { get; set; }

        [JsonPropertyName(UserId_JsonName)]
        public int? UserId { get; set; }

        public static Audit Empty = new()
        {
            DateTime = DateTime.MinValue,
            Changes = null,
            Flag = 0,
            UserId = null
        };

        public override string ToString()
        {
            var sb = new StringBuilder(5);
            sb.Append(Flag.ToString());
            sb.Append(" at ");
            sb.Append(DateTime.ToString("d"));
            sb.Append(' ');
            sb.Append(DateTime.ToString("T"));

            return sb.ToString();
        }
    }
}