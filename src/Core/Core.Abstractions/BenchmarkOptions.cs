// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Internal-only options for the startup-benchmark seam. Bound from the <c>benchmark</c>
/// configuration section but intentionally NOT registered in <c>ConfigurationSchema</c>: the key
/// (<c>benchmark.resolveOnly</c>, set via the <c>AZCLIPS_BENCH_RESOLVE_ONLY</c> environment variable)
/// is therefore hidden from <c>config info</c> / <c>config list --all</c> and rejected by
/// <c>config set</c> without <c>--force</c>. Read only by the engine's benchmark subscriber; it is
/// for internal benchmarking and must not be documented as a user-facing setting.
/// </summary>
public sealed class BenchmarkOptions
{
    /// <summary>
    /// When true, the engine terminates the process as soon as the command processor is resolved
    /// (<c>Events.ProcessorAcquired</c>), before execution and before teardown — so a startup
    /// benchmark times process-create-to-ready only. Default false; production never sets it.
    /// </summary>
    public bool ResolveOnly { get; set; }
}
