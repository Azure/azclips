// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Remove, "AzStorageBlob", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(bool))]
public sealed class RemoveStorageBlobCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("Container")]
    public string ContainerName { get; set; } = "";

    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Blob")]
    public string BlobName { get; set; } = "";

    [Parameter]
    public DateTimeOffset? SnapshotTime { get; set; }

    [Parameter]
    public string? VersionId { get; set; }

    [Parameter]
    public SwitchParameter DeleteSnapshot { get; set; }

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public string? LeaseId { get; set; }

    [Parameter]
    public string? IfMatch { get; set; }

    [Parameter]
    public string? IfNoneMatch { get; set; }

    [Parameter]
    public DateTimeOffset? IfModifiedSince { get; set; }

    [Parameter]
    public DateTimeOffset? IfUnmodifiedSince { get; set; }

    [Parameter]
    public string? TagCondition { get; set; }

    [Parameter]
    public SwitchParameter PassThru { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageBlobError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{AccountName}/{ContainerName}/{BlobName}", "Remove blob"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            ["AccountName"] = AccountName,
            ["ContainerName"] = ContainerName,
            ["BlobName"] = BlobName,
            ["PassThru"] = PassThru.IsPresent,
        };
        Add(nameof(SnapshotTime), SnapshotTime?.ToString("O"));
        Add(nameof(VersionId), VersionId);
        Add("DeleteSnapshots", DeleteSnapshot ? "only" : Force ? "include" : null);
        Add(nameof(LeaseId), LeaseId);
        Add(nameof(IfMatch), IfMatch);
        Add(nameof(IfNoneMatch), IfNoneMatch);
        Add(nameof(IfModifiedSince), IfModifiedSince?.ToString("O"));
        Add(nameof(IfUnmodifiedSince), IfUnmodifiedSince?.ToString("O"));
        Add(nameof(TagCondition), TagCondition);
        Add(nameof(Profile), Profile);

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            "Remove-AzStorageBlob",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters[name] = value;
            }
        }
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
