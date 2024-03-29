namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// A class to represent user that made changes to be used in <see cref="EntityFrameworkAuditProviderInterceptor"/>.
    /// </summary>
    public class AuditProviderUser
    {
        public AuditProviderUser(string userId, IDictionary<string, string>? additionalData = null)
        {
            UserId = userId;
            AdditionalData = additionalData;
        }

        /// <summary>
        /// Gets or sets a <see cref="string"/> that represents user id.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="Dictionary{TKey,TValue}"/> that represents additional data.
        /// </summary>
        public IDictionary<string, string>? AdditionalData { get; set; }
    }
}