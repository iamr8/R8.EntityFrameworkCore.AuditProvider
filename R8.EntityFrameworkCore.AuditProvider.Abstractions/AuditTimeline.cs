namespace R8.EntityFrameworkCore.AuditProvider.Abstractions;

public class AuditTimeline : SortedList<DateTime, AuditTimeline.Node>
{
    private int _timeAdjustment;

    internal AuditTimeline()
    {
    }

    public void Append(string table, Audit[] collection)
    {
        Array.Sort(collection, (x, y) => x.DateTime.CompareTo(y.DateTime));
        var enumerator = collection.GetEnumerator();
        using var disposable = (IDisposable) enumerator;
        while (enumerator.MoveNext())
        {
            var audit = (Audit) enumerator.Current;
            if (audit.Flag == AuditFlag.Changed)
            {
                if (this.TryGetValue(audit.DateTime, out var nodeCollection) && nodeCollection.Flag == audit.Flag)
                {
                    if (nodeCollection.TryGetValue(table, out var changes) && audit.Changes != null)
                    {
                        foreach (var change in audit.Changes)
                        {
                            changes.Add(new AuditChange
                            {
                                Column = change.Column,
                                OldValue = change.OldValue,
                                NewValue = change.NewValue
                            });
                        }
                    }
                    else
                    {
                        nodeCollection.Add(table, audit.Changes?.Select(x => new AuditChange
                        {
                            Column = x.Column,
                            OldValue = x.OldValue,
                            NewValue = x.NewValue
                        }).ToList());
                    }
                }
                else
                {
                    var dict = new Dictionary<string, List<AuditChange>>
                    {
                        {
                            table, audit.Changes?.Select(x => new AuditChange
                            {
                                Column = x.Column,
                                OldValue = x.OldValue,
                                NewValue = x.NewValue
                            }).ToList()
                        }
                    };
                    this.Add(audit.DateTime.AddSeconds(++_timeAdjustment), new Node(audit.Flag, audit.DateTime, dict));
                }
            }
            else
            {
                if (this.TryGetValue(audit.DateTime, out var node) && node.Flag == audit.Flag)
                {
                    node.Add(table, new List<AuditChange>());
                }
                else
                {
                    if (audit.Flag == AuditFlag.Created && Created == null)
                        Created = new Node(table, audit.Flag, audit.DateTime);

                    this.Add(audit.DateTime, new Node(table, audit.Flag, audit.DateTime));
                }
            }
        }
    }

    public string[] Tables => this.Values.SelectMany(x => x.Keys).Distinct().ToArray();

    public Node? Created { get; private set; }

    public class Node : Dictionary<string, List<AuditChange>>
    {
        public Node(string table, AuditFlag flag, DateTime dateTime) : base(new Dictionary<string, List<AuditChange>> { { table, new List<AuditChange>() } })
        {
            Flag = flag;
            DateTime = dateTime;
        }

        public Node(AuditFlag flag, DateTime dateTime, IDictionary<string, List<AuditChange>> dictionary) : base(dictionary)
        {
            Flag = flag;
            DateTime = dateTime;
        }

        /// <inheritdoc cref="Audit.Flag"/> 
        public AuditFlag Flag { get; }

        /// <inheritdoc cref="Audit.DateTime"/> 
        public DateTime DateTime { get; }
    }
}