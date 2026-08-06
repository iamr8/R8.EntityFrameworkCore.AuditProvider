using BenchmarkDotNet.Running;

using R8.EntityFrameworkCore.AuditProvider.Benchmarks;

BenchmarkRunner.Run<ChangeBufferBenchmarks>();
