﻿namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An <see cref="AuditFlag"/> enum that represents the audit flags.
    /// </summary>
    public enum AuditFlag : ushort
    {
        /// <summary>
        /// This will be used when <see cref="IAuditable"/> is newly created.
        /// </summary>
        Created = 0,

        /// <summary>
        /// This will be used when <see cref="IAuditable"/> is changed.
        /// </summary>
        Changed = 1,

        /// <summary>
        /// This will be used when <see cref="IAuditable"/> is deleted.
        /// </summary>
        Deleted = 2,

        /// <summary>
        /// This will be used when <see cref="IAuditable"/> is undeleted.
        /// </summary>
        UnDeleted = 3
    }
}