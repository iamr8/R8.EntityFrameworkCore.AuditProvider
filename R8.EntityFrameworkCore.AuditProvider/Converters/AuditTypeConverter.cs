using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Converters
{
    public interface IAuditTypeHandler
    {
        /// <summary>
        /// Gets a <see cref="string"/> value that represents the new value of the property.
        /// </summary>
        string? NewValue { get; }
        
        /// <summary>
        /// Gets a <see cref="string"/> value that represents the old value of the property.
        /// </summary>
        string? OldValue { get; }
        
        /// <summary>
        /// Checks whether the given <paramref name="clrType"/> can be handled or not.
        /// </summary>
        /// <param name="clrType">A <see cref="Type"/> value that represents the CLR type of the property.</param>
        /// <returns>A <see cref="bool"/> value indicating whether the given <paramref name="clrType"/> can be handled or not.</returns>
        bool CanHandle(Type clrType);
        
        /// <summary>
        /// Handles the change of the given <paramref name="currentValue"/> and <paramref name="originalValue"/> and returns a <see cref="bool"/> value indicating whether the change should be recorded or not.
        /// </summary>
        /// <param name="currentValue">A <see cref="object"/> value that represents the current value of the property.</param>
        /// <param name="originalValue">A <see cref="object"/> value that represents the original value of the property.</param>
        /// <param name="serializerOptions">A <see cref="JsonSerializerOptions"/> that represents the serializer options.</param>
        /// <returns>A <see cref="bool"/> value indicating whether the change should be recorded or not.</returns>
        bool Handle(object? currentValue, object? originalValue, JsonSerializerOptions serializerOptions);
    }

    public abstract class AuditTypeConverter : IAuditTypeHandler
    {
        public string? NewValue { get; protected set; }
        public string? OldValue { get; protected set; }

        public abstract bool CanHandle(Type clrType);

        public abstract bool Handle(object? currentValue, object? originalValue, JsonSerializerOptions serializerOptions);
    }
}