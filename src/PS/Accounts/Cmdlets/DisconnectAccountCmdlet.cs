// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Accounts;

[Cmdlet(VerbsCommunications.Disconnect, CommandNoun, SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.Medium)]
[OutputType(typeof(void))]
public class DisconnectAccountCmdlet : PSCmdlet
{
    private const string CommandNoun = Constants.CommandPrefix + "Account";
    private const string CommandName = VerbsCommunications.Disconnect + "-" + CommandNoun;
    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, "LoginError", ErrorCategory.AuthenticationError);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected override void ProcessRecord()
    {
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();
        if (!ShouldProcess("Signed in user", "Disconnect"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>();
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
