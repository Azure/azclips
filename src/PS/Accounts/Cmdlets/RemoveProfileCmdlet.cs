using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

/// <summary>Removes a profile. If the removed profile was active, the active pointer is cleared.</summary>
[Cmdlet(VerbsCommon.Remove, CommandNoun, SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(void))]
public class RemoveProfileCmdlet : PSCmdlet
{
    private const string CommandNoun = Constants.CommandPrefix + "Profile";
    private const string CommandName = VerbsCommon.Remove + "-" + CommandNoun;

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = true, Position = 0)]
    public string Name { get; set; } = "";

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
        if (!ShouldProcess(Name, "Remove profile"))
        {
            return;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var parameters = new Dictionary<string, object> { ["Name"] = Name };
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
