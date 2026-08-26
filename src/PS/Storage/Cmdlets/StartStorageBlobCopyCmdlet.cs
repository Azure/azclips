// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsLifecycle.Start, "AzStorageBlobCopy", SupportsShouldProcess = true)]
[Alias("Start-CopyAzureStorageBlob")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageBlobCopyResult")]
public sealed class StartStorageBlobCopyCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("DestinationContainer")]
    public string DestContainer { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("DestinationBlob")]
    public string DestBlob { get; set; } = "";

    [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true)]
    [Alias("SrcUri", "SourceUri")]
    public string AbsoluteUri { get; set; } = "";

    [Parameter]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageBlobError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"{AccountName}/{DestContainer}/{DestBlob}", $"Copy from {AbsoluteUri}"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            [nameof(AccountName)] = AccountName,
            [nameof(DestContainer)] = DestContainer,
            [nameof(DestBlob)] = DestBlob,
            [nameof(AbsoluteUri)] = AbsoluteUri,
        };
        if (!string.IsNullOrWhiteSpace(Profile))
        {
            parameters[nameof(Profile)] = Profile;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            "Start-AzStorageBlobCopy",
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
