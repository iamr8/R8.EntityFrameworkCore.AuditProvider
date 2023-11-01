namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    public abstract class JsonNames
    {
        public class Audit
        {
            public const string DateTime = "dt";
            public const string Flag = "f";
            public const string Changes = "c";
            public const string User = "u";
            public const string StackTrace = "st";
        }

        public class AuditUser
        {
            public const string UserId = "id";
            public const string AdditionalData = "ad";
        }
    }
}