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
/// <remarks>
/// Benchmark-only.
///
/// <para><b>Runs inside the measured region.</b> OnEnd executes on the thread that ends the
/// activity, so this is limited to a tag lookup and a prefix comparison.</para>
///
/// <para>Every iteration imports the module, so ImportTelemetry emits one startup trace per
/// iteration. Its root is named "PowerShell.Activity.Root", the same name a cmdlet root uses, so
/// the discriminator is the importKind tag ImportTelemetry.Emit sets on it; the child phases match
/// on name prefix. The tag survives DefaultTelemetryPropertyFilterProcessor because
/// TelemetryTags.ImportKind is on that filter's allowlist.</para>
///
/// <para>Clearing the Recorded flag is the mechanism DefaultActivityNameFilterProcessor uses: both
/// SimpleActivityExportProcessor and BatchActivityExportProcessor skip unrecorded activities.</para>
/// </remarks>
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
