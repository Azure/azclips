// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

/// <summary>
/// Base for every Network cmdlet. Each bound cmdlet parameter is sent to the extension under
/// its own name, so a cmdlet parameter name must match the PowerShell name in the Network
/// <c>ExtensionIndex</c>.
/// </summary>
public abstract class NetworkCmdletBase : PSCmdlet
{
    private readonly CancellationTokenSource cancellation = new();
    private IEngine engine = null!;
    private ITelemetryService telemetry = null!;
    private OutputStreams streams = null!;

    [Parameter]
    public string? SubscriptionId { get; set; }

    [Parameter]
    public string? Profile { get; set; }

    /// <summary>Cmdlet parameters that control the cmdlet only and are not sent to the extension.</summary>
    protected virtual IReadOnlySet<string> LocalParameters { get; } = new HashSet<string>();

    protected override void BeginProcessing()
    {
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
        telemetry = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        streams = CmdletOutputStreamsFactory.Create(this, "NetworkError", ErrorCategory.NotSpecified, enumerateOutput: true);
    }

    /// <summary>Runs <paramref name="command"/> with the bound parameters. A failure throws.</summary>
    protected void Execute(string command)
    {
        var parameters = new Dictionary<string, object>();
        foreach (var (name, value) in MyInvocation.BoundParameters)
        {
            if (CommonParameters.Contains(name) || OptionalCommonParameters.Contains(name) || LocalParameters.Contains(name))
            {
                continue;
            }
            parameters.Add(name, value switch
            {
                SwitchParameter switchValue => switchValue.IsPresent,
                Hashtable table => ToStringDictionary(table),
                _ => value,
            });
        }

        using var activity = telemetry.StartRootActivity();
        var invocation = new CommandInvocation("Network", command, parameters, false,
            MyInvocation.MyCommand.ModuleName, MyInvocation.MyCommand.Version?.ToString());
        telemetry.TagInvocation(invocation);
        using ISession session = engine.OpenExecutionSession(invocation, streams, cancellation.Token);
        session.Run();
    }

    private static Dictionary<string, string> ToStringDictionary(Hashtable table)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in table)
        {
            result[entry.Key.ToString()!] = entry.Value?.ToString() ?? string.Empty;
        }
        return result;
    }

    protected override void EndProcessing() => cancellation.Dispose();

    protected override void StopProcessing() => cancellation.Cancel();
}

/// <summary>Base for create and update cmdlets: asks <c>ShouldProcess</c> before it sends the request.</summary>
public abstract class NetworkWriteCmdletBase : NetworkCmdletBase
{
    protected abstract string Command { get; }

    protected abstract string Target { get; }

    protected virtual string Action => "Create";

    protected override void ProcessRecord()
    {
        if (ShouldProcess(Target, Action))
        {
            Execute(Command);
        }
    }
}

/// <summary>Base for Get cmdlets with a <c>Get</c> set and a <c>List</c> set.</summary>
public abstract class NetworkGetCmdletBase : NetworkCmdletBase
{
    protected const string GetSet = "Get";
    protected const string ListSet = "List";

    protected abstract string Noun { get; }

    protected override void ProcessRecord() => Execute($"Get-{Noun}_{ParameterSetName}");
}

/// <summary>
/// Base for removing a top-level resource, after <c>src/PS/Compute/Cmdlets/RemoveVMCmdlet.cs</c>.
/// <c>-Force</c> skips the prompt, <c>-WhatIf</c> always stops before the request, and
/// <c>-PassThru</c> writes <c>$true</c> only after the delete succeeds.
/// </summary>
public abstract class NetworkRemoveCmdletBase : NetworkCmdletBase
{
    private static readonly HashSet<string> s_localParameters = [nameof(Force), nameof(PassThru)];

    [Parameter(Mandatory = true)]
    [Alias("ResourceName")]
    public string Name { get; set; } = string.Empty;

    [Parameter(Mandatory = true)]
    public string ResourceGroupName { get; set; } = string.Empty;

    [Parameter(HelpMessage = "Do not ask for confirmation.")]
    public SwitchParameter Force { get; set; }

    [Parameter(HelpMessage = "Return $true when the resource is deleted.")]
    public SwitchParameter PassThru { get; set; }

    protected override IReadOnlySet<string> LocalParameters => s_localParameters;

    protected abstract string Command { get; }

    /// <summary>The resource type in readable form, for example "virtual network".</summary>
    protected abstract string ResourceType { get; }

    protected override void ProcessRecord()
    {
        if (!ConfirmRemove($"{ResourceType} '{Name}' in resource group '{ResourceGroupName}'"))
        {
            return;
        }

        Execute(Command);

        if (PassThru.IsPresent)
        {
            WriteObject(true);
        }
    }

    protected virtual bool ConfirmRemove(string target)
    {
        if (!ShouldProcess(target, "Remove"))
        {
            return false;
        }
        return Force.IsPresent
            || ShouldContinue($"This cmdlet will remove the {ResourceType} '{Name}'. Do you want to continue?", $"Remove {ResourceType}");
    }
}
