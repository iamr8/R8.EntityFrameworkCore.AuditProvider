using System.Text.Json;

namespace R8.EntityFrameworkCore.AuditProvider.Abstractions
{
    public class AuditCollection
    {
        private Audit[]? _cachedDeserialized;
        private bool _deserialized;

        /// <summary>
        /// Gets the underlying <see cref="JsonElement"/> of current instance.
        /// </summary>
        public JsonElement Element { get; }

        private AuditCollection(JsonElement element)
        {
            Element = element;
        }

        /// <summary>
        /// Returns last audit.
        /// </summary>
        public Audit? GetLast(bool includeDeleted = false)
        {
            this.Deserialize();

            if (_deserialized && _cachedDeserialized is { Length: > 0 })
            {
                var index = _cachedDeserialized.Length - 1;
                while (index >= 0)
                {
                    var audit = _cachedDeserialized[index];
                    if (audit != Audit.Empty && (includeDeleted || audit.Flag != AuditFlag.Deleted))
                        return audit;
                    index--;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns first audit with <see cref="AuditFlag.Created"/> flag.
        /// </summary>
        public Audit? GetCreated()
        {
            if (_deserialized && _cachedDeserialized is { Length: > 0 })
            {
                foreach (var audit in _cachedDeserialized)
                {
                    if (audit.Flag == AuditFlag.Created)
                        return audit;
                }
            }
            else
            {
                foreach (var audit in this.Element.EnumerateArray())
                {
                    var flag = (AuditFlag)audit.GetProperty(JsonNames.Audit.Flag).GetInt16();
                    if (flag == AuditFlag.Created)
                        return audit.Deserialize<Audit>(AuditProviderConfiguration.JsonOptions);
                }
            }


            return null;
        }

        /// <summary>
        /// Deserializes to array of <see cref="Audit"/>.
        /// </summary>
        /// <returns></returns>
        public Audit[]? Deserialize()
        {
            if (_cachedDeserialized == null || _cachedDeserialized.Length == 0)
            {
                _cachedDeserialized = this.Element.Deserialize<Audit[]>(AuditProviderConfiguration.JsonOptions);
                _deserialized = true;
            }

            return _cachedDeserialized;
        }

        public static explicit operator AuditCollection(JsonElement element)
        {
            return new AuditCollection(element);
        }
    }
}