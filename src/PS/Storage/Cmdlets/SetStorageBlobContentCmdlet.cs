// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Set, "AzStorageBlobContent", SupportsShouldProcess = true)]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.BlobTransferResult")]
public sealed class SetStorageBlobContentCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("Container")]
    public string ContainerName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("Blob")]
    public string BlobName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("File")]
    public string FilePath { get; set; } = "";

    [Parameter]
    public SwitchParameter Force { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageBlobError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{AccountName}/{ContainerName}/{BlobName}", "Upload blob content"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            ["AccountName"] = AccountName,
            ["ContainerName"] = ContainerName,
            ["BlobName"] = BlobName,
            ["FilePath"] = FilePath,
            ["Overwrite"] = Force.IsPresent,
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile)))
        {
            parameters["Profile"] = Profile!;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            "Set-AzStorageBlobContent",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
