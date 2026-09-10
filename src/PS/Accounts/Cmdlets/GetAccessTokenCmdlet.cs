using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>
/// Retrieves an access token for a resource, well-known resource type, or explicit scope list from
/// the current signed-in context. The token is redacted by default; pass <c>-AsPlainText</c> to emit
/// the raw token as plain text.
/// </summary>
[Cmdlet(VerbsCommon.Get, CommandNoun, DefaultParameterSetName = "ResourceType")]
[OutputType(typeof(SecureAccessTokenResult), typeof(AccessTokenResult))]
public class GetAccessTokenCmdlet : PSCmdlet
{
    private const string CommandNoun = Constants.CommandPrefix + "AccessToken";
    private const string CommandName = VerbsCommon.Get + "-" + CommandNoun;

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Resource", Mandatory = true)]
    public string? ResourceUrl { get; set; }

    [Parameter(ParameterSetName = "ResourceType", Mandatory = false)]
    public string? ResourceTypeName { get; set; }

    [Parameter(ParameterSetName = "Scope", Mandatory = true)]
    public string[]? Scope { get; set; }

    [Parameter(Mandatory = false)]
    public string? TenantId { get; set; }

    [Parameter(Mandatory = false)]
    public string? SubscriptionId { get; set; }

    [Parameter(Mandatory = false)]
    public SwitchParameter AsPlainText { get; set; }

    protected override void BeginProcessing()
    {
        streams = new OutputStreams(
            WriteOutput: (value, ct) => { WriteObject(value); return Task.CompletedTask; },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? ""),
                    "AccessTokenError",
                    ErrorCategory.NotSpecified,
                    null));
                return Task.CompletedTask;
            },
            WriteInformation: (value, ct) =>
            {
                WriteInformation(new InformationRecord(value, null));
                return Task.CompletedTask;
            });
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
        if (MyInvocation.BoundParameters.ContainsKey(nameof(SubscriptionId)))
            parameters["SubscriptionId"] = SubscriptionId!;
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
