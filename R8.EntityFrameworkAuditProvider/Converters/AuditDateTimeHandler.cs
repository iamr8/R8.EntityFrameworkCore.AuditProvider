using System.Text.Json;

namespace R8.EntityFrameworkAuditProvider.Converters
{
    public class AuditDateTimeHandler : AuditTypeConverter
    {
        public override bool CanHandle(Type clrType) => clrType == typeof(DateTime) || clrType == typeof(DateTime?);

        public override bool Handle(object? currentValue, object? originalValue, JsonSerializerOptions serializerOptions)
        {
            var @new = currentValue == null || (DateTime)currentValue == DateTime.MinValue ? null : currentValue;
            var old = originalValue == null || (DateTime)originalValue == DateTime.MinValue ? null : originalValue;
            if (@new == null && old == null)
                return false;

            this.NewValue = currentValue == null || (DateTime)currentValue == DateTime.MinValue ? null : currentValue.ToString();
            this.OldValue = originalValue == null || (DateTime)originalValue == DateTime.MinValue ? null : originalValue.ToString();
            return true;
        }
    }
}