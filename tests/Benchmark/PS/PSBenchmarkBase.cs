// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Perfolizer.Mathematics.OutlierDetection;

namespace Benchmark.PS;

[MemoryDiagnoser]
[InvocationCount(invocationCount: 1, unrollFactor: 1)]
[MaxRelativeError(0.01)]
[MaxIterationCount(1000)]
[Outliers(OutlierMode.RemoveAll)]
public abstract class PSBenchmarkBase : BenchmarkBase
{
    [GlobalSetup]
    public void GlobalSetup()
    {
        InitializeCommon();
        Console.WriteLine("[Setup] PS benchmarks ready (stub HTTP)");
    }

    [IterationSetup]
    public void IterationSetup()
    {
        RegisterBenchmarkStubs();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CleanupCommon();
    }
}
