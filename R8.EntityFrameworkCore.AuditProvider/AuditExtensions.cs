using System.Text.Json;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    public static class AuditExtensions
    {
        /// <summary>
        /// Returns last audit except <see cref="AuditFlag.Deleted"/> flag.
        /// </summary>
        public static Audit? GetLastUpdated(this JsonElement audits)
        {
            if (audits is { ValueKind: JsonValueKind.Array })
            {
                var json = audits.Deserialize<Audit[]>();
                if (json != null && json.Length != 0)
                {
                    var audit = json.Where(x => x.Flag != AuditFlag.Deleted).OrderByDescending(x => x.DateTime).FirstOrDefault();
                    if (audit != Audit.Empty)
                    {
                        return audit;
                    }
                }
            }

            return null;
        }

        public static Audit? GetCreated(this JsonElement audits, DateTime? fallbackValue = null)
        {
            var json = new List<Audit>();

            if (audits is { ValueKind: JsonValueKind.Array })
            {
                var _json = audits.Deserialize<List<Audit>>();
                if (_json is { Count: > 0 })
                    json = _json;
            }

            var first = json.ElementAtOrDefault(0);
            if (first == Audit.Empty || first.Flag > AuditFlag.Created)
            {
                if (fallbackValue != null && fallbackValue.Value != DateTime.MinValue)
                {
                    json.Insert(0, new Audit { DateTime = fallbackValue.Value, Flag = AuditFlag.Created });
                }
            }

            var audit = json.OrderBy(x => x.DateTime).FirstOrDefault();
            if (audit != Audit.Empty)
            {
                if (audit.Flag == AuditFlag.Created && (audit.Flag != AuditFlag.Created || audit.DateTime != DateTime.MinValue)) 
                    return audit;
            }

            return null;
        }
    }
}