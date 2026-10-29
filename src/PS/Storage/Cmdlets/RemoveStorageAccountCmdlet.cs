// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Storage;

[Cmdlet(VerbsCommon.Remove, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(void))]
public class RemoveStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true, Position = 0, ValueFromPipelineByPropertyName = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true, Position = 1, ValueFromPipelineByPropertyName = true)]
    [Alias("StorageAccountName", "AccountName")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public SwitchParameter Force { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "StorageError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        // Deleting an account destroys its data, hence the high confirm impact: this prompts by
        // default rather than only under -Confirm. -Force skips the prompt, but -WhatIf must still
        // short-circuit.
        var whatIf = MyInvocation.BoundParameters.TryGetValue("WhatIf", out var value)
                     && value is SwitchParameter { IsPresent: true };
        if ((whatIf || !Force.IsPresent)
            && !ShouldProcess($"Storage account '{Name}' in resource group '{ResourceGroupName}'", "Remove"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>
        {
            { "ResourceGroupName", ResourceGroupName },
            { "Name", Name },
        };
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add("SubscriptionId", SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add("Profile", Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("Storage", "Remove-AzStorageAccount", parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing() => _cts.Dispose();

    protected override void StopProcessing() => _cts.Cancel();
}
