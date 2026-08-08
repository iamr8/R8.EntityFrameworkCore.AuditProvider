# Change-buffer benchmark

Isolated A/B of how one save's change set is accumulated: the old `List<AuditChange>` vs the current
`ArrayPool`-rented, growable buffer. Both return the same right-sized `AuditChange[]` that lands on the
audit (common to both); what is measured is the **transient** allocation + time for the buffer itself.
EF's property iteration is identical for both and is excluded, so only the buffer choice is measured.

Run:

```
dotnet run -c Release --project R8.EntityFrameworkCore.AuditProvider.Benchmarks
```

This project is intentionally **not** in `R8.sln`, so it doesn't add BenchmarkDotNet to CI or the license
check.

## Results

BenchmarkDotNet v0.14.0 · Apple M2 Pro · .NET 8.0 host · ShortRun (indicative, not a certified run).

| Method         | Changes | Mean       | Allocated | Alloc ratio |
|----------------|--------:|-----------:|----------:|------------:|
| List_Based     | 2       |   52.3 ns  |     480 B |        1.00 |
| ArrayPool_Grow | 2       |   61.4 ns  |     200 B |    **0.42** |
| List_Based     | 8       |  167.9 ns  |   1,480 B |        1.00 |
| ArrayPool_Grow | 8       |  148.9 ns  |     728 B |    **0.49** |
| List_Based     | 16      |  331.5 ns  |   3,104 B |        1.00 |
| ArrayPool_Grow | 16      |  272.3 ns  |   1,432 B |    **0.46** |
| List_Based     | 40      |  893.2 ns  |  10,640 B |        1.00 |
| ArrayPool_Grow | 40      |  803.1 ns  |   3,544 B |    **0.33** |
| List_Based     | 100     | 2,073.1 ns |  23,112 B |        1.00 |
| ArrayPool_Grow | 100     | 1,896.9 ns |   8,824 B |    **0.38** |

## Reading it

- **Allocation** drops to ~33–49% of the List approach across the board (the remaining allocation is the
  final right-sized `AuditChange[]`, which both must produce). This is the point: at millions of saves,
  less transient garbage = less GC pressure = better sustained throughput.
- **Time** is faster for 8+ changes; ~17% slower only at 2 changes, where the pool rent/return overhead
  outweighs the tiny work. Real saves usually change few columns, so time is roughly a wash and the win is
  the allocation reduction.
