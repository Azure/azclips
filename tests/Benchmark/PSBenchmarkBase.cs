// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Benchmark;

/// <summary>
/// PS benchmarks — runs cmdlets with mocked HTTP. Does NOT hit Azure.
/// Cold-start benchmarks: fresh runspace + module import + DI init + engine process + dispose per call.
/// </summary>
public abstract class PSBenchmarkBase : BenchmarkBase
{
}
