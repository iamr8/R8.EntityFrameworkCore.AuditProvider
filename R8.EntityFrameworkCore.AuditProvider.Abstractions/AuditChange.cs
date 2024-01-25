using System.Text.Json;
using System.Text.Json.Serialization;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    public readonly record struct AuditChange(string Column, JsonElement? OldValue, JsonElement? NewValue)
    {
        /// <summary>
        /// Name of the property that had changes.
        /// </summary>
        [JsonPropertyName("n")]
        public string Column { get; init; } = Column;

        /// <summary>
        /// Gets or sets a <see cref="string"/> that representing value changed to a new one.
        /// </summary>
        [JsonPropertyName("_v")]
        public JsonElement? OldValue { get; init; } = OldValue;

        /// <summary>
        /// Gets or sets a <see cref="string"/> that representing old value changed to this.
        /// </summary>
        [JsonPropertyName("v")]
        public JsonElement? NewValue { get; init; } = NewValue;

        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Column) || !OldValue.HasValue || !NewValue.HasValue)
                return 0;

            return Column.GetHashCode() + OldValue.GetHashCode() + NewValue.GetHashCode();
        }

        [JsonIgnore] 
        public bool HasValue => !string.IsNullOrEmpty(Column) && (OldValue.HasValue || NewValue.HasValue);

        public static AuditChange Empty => new()
        {
            Column = string.Empty,
            NewValue = null,
            OldValue = default
        };
    }
}