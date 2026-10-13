using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>
/// Lists all profiles, shows a single profile when <c>-Name</c> is supplied, or shows the active
/// profile when <c>-Active</c> is supplied.
/// </summary>
[Cmdlet(VerbsCommon.Get, CommandNoun, DefaultParameterSetName = "List")]
[OutputType("Microsoft.Azure.Azclips.Extensions.Accounts.Models.ProfileSummary")]
public class GetProfileCmdlet : PSCmdlet
{
    private const string CommandNoun = Constants.CommandPrefix + "Profile";
    private const string CommandName = VerbsCommon.Get + "-" + CommandNoun;

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(ParameterSetName = "Show", Mandatory = true, Position = 0)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = "ShowActive", Mandatory = true)]
    public SwitchParameter Active { get; set; }

    protected override void BeginProcessing()
    {
        streams = new OutputStreams(
            WriteOutput: (value, ct) =>
            {
                if (value is IEnumerable and not string)
                    WriteObject(value, true);
                else
                    WriteObject(value);
                return Task.CompletedTask;
            },
            WriteVerbose: (value, ct) => { WriteVerbose(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteDebug: (value, ct) => { WriteDebug(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteWarning: (value, ct) => { WriteWarning(value?.ToString() ?? ""); return Task.CompletedTask; },
            WriteError: (value, ct) =>
            {
                WriteError(new ErrorRecord(
                    new Exception(value?.ToString() ?? ""),
                    "ProfileError",
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
        var commandName = $"{CommandName}_List";
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Name)))
        {
            parameters.Add("Name", Name!);
            commandName = $"{CommandName}_Show";
        }
        else if (Active.IsPresent)
        {
            parameters.Add("Active", true);
            commandName = $"{CommandName}_Show";
        }

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation(Constants.ExtensionName, commandName, parameters, false, moduleName, moduleVersion);
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
