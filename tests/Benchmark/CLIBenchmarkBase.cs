// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Perfolizer.Mathematics.OutlierDetection;

namespace Benchmark;


/// <summary>
/// CLI benchmarks — runs Program.Main() in-process with mocked HTTP. Does NOT hit Azure.
/// Cold-start benchmarks: full DI init + parse + engine process + dispose per call.
/// </summary>
[InProcess]
public abstract class CLIBenchmarkBase : BenchmarkBase
{

}
