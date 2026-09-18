// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>
/// Retrieves an access token for a resource, well-known resource type, or explicit scope list from
/// the current signed-in context. The token is returned as a <c>SecureString</c> by default; pass
/// <c>-AsPlainText</c> to return the raw token as a plain string.
/// </summary>
[Cmdlet(VerbsCommon.Get, CommandNoun, DefaultParameterSetName = "ResourceType")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Accounts.Models.SecureAccessTokenResult", "Microsoft.Azure.Azclips.Extensions.Accounts.Models.AccessTokenResult")]
public class GetAccessTokenCmdlet : PSCmdlet
{
    private const string CommandNoun = Constants.CommandPrefix + "AccessToken";
    private const string CommandName = VerbsCommon.Get + "-" + CommandNoun;

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Resource", Mandatory = true)]
    [Alias("Resource", "ResourceUri")]
    public string? ResourceUrl { get; set; }

    [Parameter(ParameterSetName = "ResourceType", Mandatory = false)]
    // Suggest + validate the PowerShell PascalCase resource-type names, giving Get-AzAccessToken parity
    // and using the repo's usual [ValidateSet] convention. This includes ResourceManager, the classic
    // Az PowerShell alias for Arm, so the resolver's accepted names are not rejected here. IgnoreCase
    // lets case variants through; the CLI kebab spellings (e.g. ms-graph) are intentionally not accepted
    // on the PowerShell surface (the CLI surface enforces those via its AllowedValues). The shared
    // TokenScopeResolver still understands both spellings, so each front end feeds it its own vocabulary.
    // A test in Cmdlets.Tests keeps this set in lockstep with the resolver's known resource types.
    [ValidateSet(
        "Arm", "ResourceManager", "AadGraph", "MSGraph", "Batch", "DataLake", "OssRdbms", "Media",
        "KeyVault", "Storage", "Synapse", "ManagedHsm", "CommunicationEmail", "Attestation",
        "AppConfiguration", "AnalysisServices", "OperationalInsights", IgnoreCase = true)]
    public string? ResourceTypeName { get; set; }

    [Parameter(ParameterSetName = "Scope", Mandatory = true)]
    public string[]? Scope { get; set; }

    [Parameter(Mandatory = false)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter AsPlainText { get; set; }

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "AccessTokenError", ErrorCategory.AuthenticationError);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object>();
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceUrl)))
            parameters["ResourceUrl"] = ResourceUrl!;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(ResourceTypeName)))
            parameters["ResourceTypeName"] = ResourceTypeName!;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Scope)))
            parameters["Scope"] = Scope!;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(TenantId)))
            parameters["TenantId"] = TenantId!;
        if (AsPlainText.IsPresent)
            parameters["AsPlainText"] = true;

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation(Constants.ExtensionName, CommandName, parameters, false, moduleName, moduleVersion);
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
