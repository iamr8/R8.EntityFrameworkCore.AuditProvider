using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    public readonly struct AuditCollection
    {
        /// <summary>
        /// Gets the underlying <see cref="JsonElement"/> of current instance.
        /// </summary>
        public JsonElement Element { get; }

        private AuditCollection(JsonElement element)
        {
            Element = element;
        }

        /// <summary>
        /// Returns last audit except <see cref="AuditFlag.Deleted"/> flag.
        /// </summary>
        public Audit? GetLastUpdated()
        {
            var json = this.Element.Deserialize<Audit[]>();
            if (json != null && json.Length != 0)
            {
                var audit = json.Where(x => x.Flag != AuditFlag.Deleted).OrderByDescending(x => x.DateTime).FirstOrDefault();
                if (audit != Audit.Empty)
                {
                    return audit;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns first audit with <see cref="AuditFlag.Created"/> flag.
        /// </summary>
        public Audit? GetCreated()
        {
            foreach (var audit in this.Element.EnumerateArray())
            {
                var flag = (AuditFlag)audit.GetProperty(JsonNames.Audit.Flag).GetInt16();
                if (flag == AuditFlag.Created)
                    return audit.Deserialize<Audit>(AuditProviderConfiguration.JsonOptions);
            }

            return null;
        }

        /// <inheritdoc cref="JsonElement.GetRawText()"/>
        public string GetRawText()
        {
            return this.Element.GetRawText();
        }

        /// <summary>
        /// Deserializes to array of <see cref="Audit"/>.
        /// </summary>
        /// <returns></returns>
        public Audit[]? Deserialize()
        {
            var audits = this.Element.Deserialize<Audit[]>(AuditProviderConfiguration.JsonOptions);
            return audits;
        }

        public AuditTimeline? GetTimeline(string tableName)
        {
            var audits = this.Deserialize();
            if (audits == null || audits.Length == 0)
                return null;
            
            var timeline = new AuditTimeline();
            timeline.Append(tableName, audits);
            return timeline;
        }

        public static explicit operator JsonElement(AuditCollection collection)
        {
            return collection.Element;
        }
    
        public static explicit operator AuditCollection(JsonElement element)
        {
            return new AuditCollection(element);
        }
    }
}