﻿using System.Diagnostics;
using System.Text.Json.Serialization;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    /// <summary>
    /// An object to track creation, modification, and deletion of specific entity.
    /// </summary>
    [DebuggerDisplay("[{" + nameof(DateTime) + "}] {" + nameof(Flag) + "}")]
    public record struct Audit
    {
        /// <summary>
        /// Gets a <see cref="DateTime"/> that represents the time that changes made.
        /// </summary>
        [JsonPropertyName(JsonNames.Audit.DateTime)]
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Gets a flag that represents what happened to entity.
        /// </summary>
        [JsonPropertyName(JsonNames.Audit.Flag)]
        public AuditFlag Flag { get; set; }

        /// <summary>
        /// Gets the changes that made to entity.
        /// </summary>
        /// <remarks>This property only works when <see cref="Flag"/> is <see cref="AuditFlag.Changed"/></remarks>
        [JsonPropertyName(JsonNames.Audit.Changes)]
        public AuditChange[]? Changes { get; set; }

        /// <summary>
        /// Gets the user that made changes.
        /// </summary>
        [JsonPropertyName(JsonNames.Audit.User)]
        public AuditUser? User { get; set; }

        public static Audit Empty = new()
        {
            DateTime = DateTime.MinValue,
            Changes = null,
            Flag = 0,
            User = null
        };
    }
}