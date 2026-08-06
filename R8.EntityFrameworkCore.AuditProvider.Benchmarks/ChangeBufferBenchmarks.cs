using System.Buffers;

using BenchmarkDotNet.Attributes;

using R8.EntityFrameworkCore.AuditProvider.Abstractions;

namespace R8.EntityFrameworkCore.AuditProvider.Benchmarks;

// Compares the two ways of accumulating a single save's change set: the old List<AuditChange> vs the
// current ArrayPool-rented, growable buffer. Both produce the same right-sized AuditChange[] that ends up
// on the audit (that final array is common to both); the difference measured here is the TRANSIENT
// allocation for the buffer itself. EF's property iteration is identical for both and is excluded so the
// buffer choice is isolated. [MemoryDiagnoser] reports allocated bytes per operation.
[MemoryDiagnoser]
[ShortRunJob]
public class ChangeBufferBenchmarks
{
    // Number of audited property changes in a single save. Spans the initial buffer capacity (16) and
    // several growths (17, 40, 100) as well as the common small case.
    [Params(2, 8, 16, 40, 100)]
    public int ChangeCount;

    private const int InitialChangeBufferCapacity = 16;

    private static AuditChange Make(int i) => new("P" + i, null, null);

    [Benchmark(Baseline = true)]
    public AuditChange[] List_Based()
    {
        List<AuditChange>? changes = null;
        for (var i = 0; i < ChangeCount; i++)
            (changes ??= new List<AuditChange>()).Add(Make(i));

        return changes is null ? Array.Empty<AuditChange>() : changes.ToArray();
    }

    [Benchmark]
    public AuditChange[] ArrayPool_Grow()
    {
        var pool = ArrayPool<AuditChange>.Shared;
        var buffer = pool.Rent(InitialChangeBufferCapacity);
        var count = 0;
        try
        {
            for (var i = 0; i < ChangeCount; i++)
            {
                if (count == buffer.Length)
                {
                    var larger = pool.Rent(buffer.Length * 2);
                    Array.Copy(buffer, larger, count);
                    pool.Return(buffer, clearArray: true);
                    buffer = larger;
                }

                buffer[count++] = Make(i);
            }

            return count == 0 ? Array.Empty<AuditChange>() : new ArraySegment<AuditChange>(buffer, 0, count).ToArray();
        }
        finally
        {
            pool.Return(buffer, clearArray: true);
        }
    }
}
