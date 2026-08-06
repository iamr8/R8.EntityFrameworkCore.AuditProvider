namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// The short JSON property names used when serializing audits, kept compact to reduce stored size.
    /// </summary>
    public abstract class JsonNames
    {
        /// <summary>
        /// JSON property names for an <see cref="Abstractions.Audit"/>.
        /// </summary>
        public class Audit
        {
            /// <summary>The <see cref="Abstractions.Audit.DateTime"/> property name.</summary>
            public const string DateTime = "dt";

            /// <summary>The <see cref="Abstractions.Audit.Flag"/> property name.</summary>
            public const string Flag = "f";

            /// <summary>The <see cref="Abstractions.Audit.Changes"/> property name.</summary>
            public const string Changes = "c";

            /// <summary>The <see cref="Abstractions.Audit.User"/> property name.</summary>
            public const string User = "u";
        }

        /// <summary>
        /// JSON property names for an <see cref="Abstractions.AuditUser"/>.
        /// </summary>
        public class AuditUser
        {
            /// <summary>The <see cref="Abstractions.AuditUser.UserId"/> property name.</summary>
            public const string UserId = "id";

            /// <summary>The <see cref="Abstractions.AuditUser.AdditionalData"/> property name.</summary>
            public const string AdditionalData = "ad";
        }
    }
}
