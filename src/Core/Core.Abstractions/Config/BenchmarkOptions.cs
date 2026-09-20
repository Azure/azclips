// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

[ConfigurationSection("benchmark", IncludeInSchema = false)]
public sealed class BenchmarkOptions
{
    /// <summary>
    /// When true, the engine terminates the process as soon as the command processor is resolved
    /// (<c>Events.ProcessorAcquired</c>), before execution and before teardown — so a startup
    /// benchmark times process-create-to-ready only. Default false; production never sets it.
    /// </summary>
    public bool ResolveOnly { get; set; }
}
