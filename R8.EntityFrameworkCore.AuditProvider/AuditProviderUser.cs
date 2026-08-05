namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// A class to represent user that made changes to be used in <see cref="EntityFrameworkAuditProviderInterceptor"/>.
    /// </summary>
    public class AuditProviderUser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuditProviderUser"/> class.
        /// </summary>
        /// <param name="userId">The id of the user that made the change.</param>
        /// <param name="additionalData">Optional additional data to store alongside the user id.</param>
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