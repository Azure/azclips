// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using OpenTelemetry;

namespace Benchmark.Telemetry;

/// <summary>
/// Drops the PowerShell module-import trace so a benchmark's sampled output contains only cmdlet
/// invocations.
/// </summary>
internal sealed class ImportTraceFilter : BaseProcessor<Activity>
{
    private const string ImportPhasePrefix = "PowerShell.Startup";

    public override void OnEnd(Activity data)
    {
        if (data is null) return;

        var isImportTrace =
            data.GetTagItem(TelemetryTags.ImportKind) is not null ||
            data.OperationName.StartsWith(ImportPhasePrefix, StringComparison.Ordinal);

        if (!isImportTrace) return;

        data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }
}
