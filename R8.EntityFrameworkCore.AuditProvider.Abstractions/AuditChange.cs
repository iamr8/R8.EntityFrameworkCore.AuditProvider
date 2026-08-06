using System.Text.Json;
using System.Text.Json.Serialization;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// Represents a single property change: its column name and the old and new values.
    /// </summary>
    public record struct AuditChange
    {
        /// <summary>
        /// Initializes a new empty instance of the <see cref="AuditChange"/> struct.
        /// </summary>
        public AuditChange()
        {
            Column = string.Empty;
            OldValue = null;
            NewValue = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuditChange"/> struct.
        /// </summary>
        /// <param name="Column">The name of the property that changed.</param>
        /// <param name="OldValue">The value before the change.</param>
        /// <param name="NewValue">The value after the change.</param>
        public AuditChange(string Column, JsonElement? OldValue, JsonElement? NewValue)
        {
            this.Column = Column;
            this.OldValue = OldValue;
            this.NewValue = NewValue;
        }

        /// <summary>
        /// Name of the property that had changes.
        /// </summary>
        [JsonPropertyName("n")]
        public string Column { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="string"/> that representing value changed to a new one.
        /// </summary>
        [JsonPropertyName("_v")]
        public JsonElement? OldValue { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="string"/> that representing old value changed to this.
        /// </summary>
        [JsonPropertyName("v")]
        public JsonElement? NewValue { get; set; }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            if (string.IsNullOrEmpty(Column) || !OldValue.HasValue || !NewValue.HasValue)
                return 0;

            return Column.GetHashCode() + OldValue?.GetHashCode() ?? 0 + NewValue?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Gets a value indicating whether this change has a column name and at least one of the old or new values.
        /// </summary>
        [JsonIgnore] public bool HasValue => !string.IsNullOrEmpty(Column) && (OldValue.HasValue || NewValue.HasValue);

        /// <summary>
        /// An empty <see cref="AuditChange"/> with no column and no values.
        /// </summary>
        public static AuditChange Empty => new()
        {
            Column = string.Empty,
            NewValue = null,
            OldValue = default
        };
    }
}