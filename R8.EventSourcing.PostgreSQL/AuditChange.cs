using System.Text;
using System.Text.Json.Serialization;

namespace R8.EventSourcing.PostgreSQL
{
    public readonly record struct AuditChange(string Key, string? OldValue, string? NewValue)
    {
        /// <summary>
        /// Name of the property that had changes.
        /// </summary>
        [JsonPropertyName("n")]
        public string Key { get; init; } = Key;

        /// <summary>
        /// Gets or sets a <see cref="string"/> that representing value changed to a new one.
        /// </summary>
        [JsonPropertyName("_v")]
        public string? OldValue { get; init; } = OldValue;

        /// <summary>
        /// Gets or sets a <see cref="string"/> that representing old value changed to this.
        /// </summary>
        [JsonPropertyName("v")]
        public string? NewValue { get; init; } = NewValue;

        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(OldValue) || string.IsNullOrEmpty(NewValue))
                return 0;

            return Key.GetHashCode() + OldValue.GetHashCode() + NewValue.GetHashCode();
        }

        [JsonIgnore] public bool HasValue => !string.IsNullOrEmpty(Key) && (!string.IsNullOrEmpty(OldValue) || !string.IsNullOrEmpty(NewValue));

        public static AuditChange Empty => new()
        {
            Key = default,
            NewValue = null,
            OldValue = default
        };

        public override string? ToString()
        {
            if (string.IsNullOrEmpty(Key))
                return base.ToString();

            var sb = new StringBuilder()
                .Append('\'')
                .Append(Key)
                .Append('\'');

            if (!string.IsNullOrEmpty(OldValue))
            {
                if (!string.IsNullOrEmpty(NewValue))
                {
                    sb
                        .Append(" form ")
                        .Append('\'')
                        .Append(OldValue)
                        .Append('\'');
                }
                else
                {
                    sb.Append(" with value ")
                        .Append('\'')
                        .Append(OldValue)
                        .Append('\'');
                }
            }

            if (!string.IsNullOrEmpty(NewValue))
            {
                sb
                    .Append(" to ")
                    .Append('\'')
                    .Append(NewValue)
                    .Append('\'')
                    .Append('.');
            }
            else
            {
                sb
                    .Append(" cleaned.");
            }

            return sb.ToString();
        }
    }
}