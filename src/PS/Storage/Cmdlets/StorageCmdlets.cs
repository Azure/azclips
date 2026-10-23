// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

public abstract class StorageCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource cancellation = new();
    private IEngine engine = null!;
    private ITelemetryService telemetry = null!;
    private OutputStreams streams = null!;
    protected CancellationToken StoppingToken => cancellation.Token;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        streams = CmdletOutputStreamsFactory.Create(this, "StorageError", ErrorCategory.NotSpecified, enumerateOutput: true);
    }

    protected void Execute(string command, Dictionary<string, object> parameters)
    {
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) parameters.Add(nameof(Profile), Profile!);
        using var activity = telemetry.StartRootActivity();
        var invocation = new CommandInvocation("Storage", command, parameters, false,
            MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);
        using var session = engine.OpenExecutionSession(invocation, streams, cancellation.Token);
        session.Run();
    }

    protected override void EndProcessing() => cancellation.Dispose();
    protected override void StopProcessing() => cancellation.Cancel();
}

[Cmdlet(VerbsCommon.New, "AzStorageContext")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext")]
public sealed class NewAzStorageContextCmdlet : StorageCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string StorageAccountName { get; set; } = string.Empty;

    [Parameter]
    public SwitchParameter UseConnectedAccount { get; set; } = new(true);

    [Parameter]
    public SwitchParameter EnableFileBackupRequestIntent { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object> { [nameof(StorageAccountName)] = StorageAccountName };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(UseConnectedAccount)))
            parameters.Add(nameof(UseConnectedAccount), UseConnectedAccount.IsPresent);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(EnableFileBackupRequestIntent)))
            parameters.Add(nameof(EnableFileBackupRequestIntent), EnableFileBackupRequestIntent.IsPresent);
        Execute("New-AzStorageContext", parameters);
    }
}

[Cmdlet(VerbsCommon.Get, "AzStorageAccountKey")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageAccountKey")]
public sealed class GetAzStorageAccountKeyCmdlet : StorageCmdletBase
{
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string ResourceGroupName { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    [Alias("AccountName", "StorageAccountName")]
    [ValidateNotNullOrEmpty]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? SubscriptionId { get; set; }

    [Parameter]
    public SwitchParameter ListKerbKey { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>
        {
            [nameof(ResourceGroupName)] = ResourceGroupName,
            [nameof(Name)] = Name
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
            parameters.Add(nameof(SubscriptionId), SubscriptionId!);
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ListKerbKey)))
            parameters.Add(nameof(ListKerbKey), ListKerbKey.IsPresent);
        Execute("Get-AzStorageAccountKey", parameters);
    }
}
