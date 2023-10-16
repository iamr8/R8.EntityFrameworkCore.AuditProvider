using System.Text.Json.Serialization;

namespace R8.EntityFrameworkAuditProvider
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

        [JsonIgnore] 
        public bool HasValue => !string.IsNullOrEmpty(Key) && (!string.IsNullOrEmpty(OldValue) || !string.IsNullOrEmpty(NewValue));

        public static AuditChange Empty => new()
        {
            Key = default,
            NewValue = null,
            OldValue = default
        };
    }
}