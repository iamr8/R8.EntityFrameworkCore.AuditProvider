namespace R8.EntityFrameworkCore.AuditProvider
{
    public class AuditProviderFlagSupport
    {
        internal AuditProviderFlagSupport()
        {
        }

        public AuditFlagState Created { get; set; }

        public AuditFlagState Changed { get; set; }

        public AuditFlagState Deleted { get; set; }

        public AuditFlagState UnDeleted { get; set; }
    }
}