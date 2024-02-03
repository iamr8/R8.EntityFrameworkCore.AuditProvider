using System.Collections;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider
{
    /// <summary>
    /// Represents a collection of <see cref="Audit"/>.
    /// </summary>
    public class AuditCollection : IReadOnlyList<Audit>
    {
        private readonly Audit[] _collection;

        internal AuditCollection(Audit[] collection)
        {
            _collection = collection;
        }

        /// <summary>
        /// Returns last audit.
        /// </summary>
        /// <param name="includeDeleted">A boolean value to specify whether to include last audit (even if it is a Deleted audit), or last Changed audit.</param>
        /// <returns>An instance of <see cref="Audit"/> that represents last audit.</returns>
        public Audit? Last(bool includeDeleted)
        {
            var index = this._collection.Length - 1;
            for (var i = index; i >= 0; i--)
            {
                var audit = this._collection[i];
                if (audit != Audit.Empty && (includeDeleted || audit.Flag != AuditFlag.Deleted))
                    return audit;
            }

            return null;
        }

        /// <summary>
        /// Returns first audit with <see cref="AuditFlag.Created"/> flag.
        /// </summary>
        /// <returns>An instance of <see cref="Audit"/> that represents first audit with <see cref="AuditFlag.Created"/> flag.</returns>
        public Audit? First()
        {
            if (this is not { Count: > 0 })
                return null;

            foreach (var audit in this._collection)
            {
                if (audit.Flag == AuditFlag.Created)
                    return audit;
            }

            return null;
        }

        /// <summary>
        /// Returns any audits that has been tracked for specific property.
        /// </summary>
        /// <param name="propertyName">A string value that represents the property name.</param>
        /// <returns>An array of <see cref="Audit"/> that has been tracked for specific property.</returns>
        public Audit[] Track(string propertyName)
        {
            Span<Audit> span = new Audit[1024];
            var lastIndex = -1;

            foreach (var audit in this._collection)
            {
                if (audit is not { Flag: AuditFlag.Changed, Changes.Length: > 0 }) 
                    continue;
                
                foreach (var change in audit.Changes)
                {
                    if (string.Equals(change.Column, propertyName, StringComparison.Ordinal))
                    {
                        span[++lastIndex] = audit with { Changes = new[] { change } };
                    }
                }
            }
            
            if (lastIndex == -1)
                return Array.Empty<Audit>();
            
            return span[..(lastIndex + 1)].ToArray();
        }

        /// <inheritdoc />
        public IEnumerator<Audit> GetEnumerator()
        {
            var enumerator = _collection.GetEnumerator();
            using var _ = enumerator as IDisposable;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is Audit audit)
                    yield return audit;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc />
        public int Count => _collection.Length;

        /// <inheritdoc />
        public Audit this[int index] => _collection[index];
        
        /// <summary>
        /// Returns all audits as an array.
        /// </summary>
        public Audit[] ToArray()
        {
            return _collection;
        }
    }
}