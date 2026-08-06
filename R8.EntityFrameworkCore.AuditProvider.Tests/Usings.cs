#if NET8_0_OR_GREATER
global using Xunit;
#else
global using Xunit;
global using Xunit.Abstractions;
#endif

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]