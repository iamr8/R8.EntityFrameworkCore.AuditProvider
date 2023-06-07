using System.Collections;
using System.Text.Json;

namespace R8.EventSourcing.PostgreSQL.ChangeHandlers;

public class AuditListChangeHandler : AuditChangeHandler
{
    public override bool CanHandle(Type clrType) => clrType.GetInterfaces().Any(c => c == typeof(IList));

    public override bool Handle(object? currentValue, object? originalValue, JsonSerializerOptions serializer)
    {
        this.NewValue = currentValue! is IList { Count: > 0 }
            ? JsonSerializer.Serialize(currentValue!, serializer)
            : null;
        this.OldValue = originalValue! is IList { Count: > 0 }
            ? JsonSerializer.Serialize(originalValue!, serializer)
            : null;
        
        if ((string.IsNullOrEmpty(this.NewValue) && string.IsNullOrEmpty(this.OldValue)) ||
            !string.IsNullOrEmpty(this.NewValue) && !string.IsNullOrEmpty(this.OldValue) && this.NewValue.Equals(this.OldValue))
            return false;
        
        return true;
    }
}