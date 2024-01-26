using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider
{
#if DEBUG
    internal static class AuditStatic
    {
        public static JsonSerializerOptions? JsonStaticOptions;
    }
#endif
}