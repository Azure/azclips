// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;

[Cmdlet(VerbsCommon.Get, "AzFederatedIdentityCredential", DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models.FederatedIdentityCredential")]
public class GetAzFederatedIdentityCredentialCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = true)]
    public string IdentityName { get; set; } = "";

    [Parameter(ParameterSetName = "Get", Mandatory = true)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public string? Skiptoken { get; set; }

    [Parameter(ParameterSetName = "List", Mandatory = false)]
    public int? Top { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "FederatedIdentityCredentialError", ErrorCategory.NotSpecified, enumerateOutput: true);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { nameof(ResourceGroupName), ResourceGroupName },
            { nameof(IdentityName), IdentityName },
        };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add(nameof(SubscriptionId), SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name))) { parameters.Add(nameof(Name), Name!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Skiptoken))) { parameters.Add(nameof(Skiptoken), Skiptoken!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Top))) { parameters.Add(nameof(Top), Top!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add(nameof(Profile), Profile!); }

        var parameterSetName = ParameterSetName;
        var commandName = $"Get-AzFederatedIdentityCredential_{parameterSetName}";
        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("ManagedServiceIdentity", commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        using ISession session = engine.OpenExecutionSession(invocation, streams, _cts.Token);
        session.Run();
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}
