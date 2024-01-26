using Microsoft.Extensions.Logging;

namespace R8.EntityFrameworkCore.AuditProvider
{
    internal static class AuditEventId
    {
        public static EventId Created { get; } = new((int)AuditFlag.Created);
        public static EventId Changed { get; } = new((int)AuditFlag.Changed);
        public static EventId Deleted { get; } = new((int)AuditFlag.Deleted);
        public static EventId UnDeleted { get; } = new((int)AuditFlag.UnDeleted);
   
        public static EventId NotAuditable { get; } = new(100);
        public static EventId NotAuditableDelete { get; } = new(101);
    
        public static EventId NoChangesFound { get; } = new(200);
    }
}