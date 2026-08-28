// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.New, "AzStorageBlobSASToken")]
[OutputType(typeof(string))]
public sealed class NewStorageBlobSasTokenCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = true)]
    public string AccountName { get; set; } = "";

    [Parameter(Mandatory = true)]
    [Alias("Container")]
    public string ContainerName { get; set; } = "";

    [Parameter(Mandatory = true, Position = 0)]
    [Alias("Blob")]
    public string BlobName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string Permission { get; set; } = "";

    [Parameter(Mandatory = true)]
    public DateTimeOffset ExpiryTime { get; set; }

    [Parameter]
    public DateTimeOffset? StartTime { get; set; }

    [Parameter]
    public string? IPAddressOrRange { get; set; }

    [Parameter]
    public SwitchParameter HttpsOnly { get; set; }

    [Parameter]
    public SwitchParameter FullUri { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(AccountName)] = AccountName,
            [nameof(ContainerName)] = ContainerName,
            [nameof(BlobName)] = BlobName,
            [nameof(Permission)] = Permission,
            [nameof(ExpiryTime)] = ExpiryTime.ToString("O"),
            [nameof(HttpsOnly)] = HttpsOnly.IsPresent,
            [nameof(FullUri)] = FullUri.IsPresent,
        };
        Add(nameof(StartTime), StartTime?.ToString("O"));
        Add(nameof(IPAddressOrRange), IPAddressOrRange);
        Add(nameof(Profile), Profile);

        IEngine engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var invocation = new CommandInvocation(
            "Storage",
            "New-AzStorageBlobSASToken",
            parameters,
            false,
            MyInvocation.MyCommand.ModuleName,
            MyInvocation.MyCommand.Version?.ToString());
        telemetryService.TagInvocation(invocation);

        OutputStreams streams = CmdletOutputStreamsFactory.Create(
            this,
            "StorageBlobError",
            ErrorCategory.NotSpecified);
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
