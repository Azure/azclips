// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.ManagedServiceIdentity;

[Cmdlet(VerbsCommon.New, "AzFederatedIdentityCredential", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType("Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Models.FederatedIdentityCredential")]
public class NewAzFederatedIdentityCredentialCmdlet : PSCmdlet
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

    [Parameter(Mandatory = true)]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string[]? Audience { get; set; }

    [Parameter(Mandatory = false)]
    public string? Issuer { get; set; }

    [Parameter(Mandatory = false)]
    public string? Subject { get; set; }

    [Parameter(Mandatory = false)]
    public int? ClaimsMatchingExpressionVersion { get; set; }

    [Parameter(Mandatory = false)]
    public string? ClaimsMatchingExpressionValue { get; set; }

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "FederatedIdentityCredentialError", ErrorCategory.NotSpecified);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        if (!ShouldProcess($"federated identity credential '{Name}' under identity '{IdentityName}' in resource group '{ResourceGroupName}'", "Create"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        var parameters = new Dictionary<string, object>
        {
            { nameof(ResourceGroupName), ResourceGroupName },
            { nameof(IdentityName), IdentityName },
            { nameof(Name), Name },
        };

        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId))) { parameters.Add(nameof(SubscriptionId), SubscriptionId); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Audience))) { parameters.Add(nameof(Audience), Audience!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Issuer))) { parameters.Add(nameof(Issuer), Issuer!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Subject))) { parameters.Add(nameof(Subject), Subject!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClaimsMatchingExpressionVersion))) { parameters.Add(nameof(ClaimsMatchingExpressionVersion), ClaimsMatchingExpressionVersion!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ClaimsMatchingExpressionValue))) { parameters.Add(nameof(ClaimsMatchingExpressionValue), ClaimsMatchingExpressionValue!); }
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Profile))) { parameters.Add(nameof(Profile), Profile!); }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation("ManagedServiceIdentity", "New-AzFederatedIdentityCredential", parameters, false, moduleName, moduleVersion);
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
