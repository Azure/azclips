// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Management.Automation;
using Core.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.Network;

/// <summary>
/// Shared plumbing for the Network cmdlets: common parameters, engine setup, and command invocation.
/// </summary>
public abstract class NetworkCmdletBase : PSCmdlet
{
    private const string ExtensionName = "Network";

    private readonly CancellationTokenSource _cts = new();
    private OutputStreams streams = null!;
    private IEngine engine = null!;

    [Parameter(Mandatory = false, HelpMessage = "Azure Subscription Id. Defaults to the active profile's subscription.")]
    public string SubscriptionId { get; set; } = "";

    [Parameter(Mandatory = false)]
    public string? Profile { get; set; }

    protected abstract string ErrorId { get; }

    protected virtual bool EnumerateOutput => false;

    protected override void BeginProcessing()
    {
        streams = CmdletOutputStreamsFactory.Create(this, ErrorId, ErrorCategory.NotSpecified, EnumerateOutput);
        engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
    }

    protected bool IsBound(string parameterName) => MyInvocation.BoundParameters.ContainsKey(parameterName);

    /// <summary>
    /// Runs the command with the given PS table key. Returns false when the command wrote an error.
    /// </summary>
    protected bool RunCommand(string commandName, Dictionary<string, object> parameters)
    {
        if (IsBound(nameof(SubscriptionId)))
        {
            parameters["SubscriptionId"] = SubscriptionId;
        }
        if (IsBound(nameof(Profile)) && Profile is not null)
        {
            parameters["Profile"] = Profile;
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var activity = telemetryService.StartRootActivity();

        var moduleName = MyInvocation.MyCommand.ModuleName;
        var moduleVersion = MyInvocation.MyCommand.Version?.ToString();
        var invocation = new CommandInvocation(ExtensionName, commandName, parameters, false, moduleName, moduleVersion);
        telemetryService.TagInvocation(invocation);

        bool errorWritten = false;
        var trackedStreams = streams with
        {
            WriteError = (value, ct) =>
            {
                errorWritten = true;
                return streams.WriteError(value, ct);
            },
        };

        using ISession session = engine.OpenExecutionSession(invocation, trackedStreams, _cts.Token);
        session.Run();
        return !errorWritten;
    }

    protected override void EndProcessing()
    {
        _cts.Dispose();
    }

    protected override void StopProcessing() => _cts.Cancel();
}

/// <summary>
/// Get-Az* shape: the default List set lists by subscription or resource group; the Get set reads one resource.
/// </summary>
public abstract class NetworkGetCmdletBase : NetworkCmdletBase
{
    protected const string GetParameterSet = "Get";
    protected const string ListParameterSet = "List";

    [Parameter(Mandatory = true, ParameterSetName = GetParameterSet, HelpMessage = "The resource group name.")]
    [Parameter(Mandatory = false, ParameterSetName = ListParameterSet, HelpMessage = "The resource group name. Omit to list in the whole subscription.")]
    public string ResourceGroupName { get; set; } = "";

    [Alias("ResourceName")]
    [Parameter(Mandatory = true, ParameterSetName = GetParameterSet, HelpMessage = "The resource name.")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = false, ParameterSetName = GetParameterSet, HelpMessage = "Expands referenced resources.")]
    public string? ExpandResource { get; set; }

    /// <summary>The cmdlet name; PS table keys are "{CmdletName}_Get" and "{CmdletName}_List".</summary>
    protected abstract string CmdletName { get; }

    protected override bool EnumerateOutput => true;

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>();
        if (IsBound(nameof(ResourceGroupName)))
        {
            parameters.Add("ResourceGroupName", ResourceGroupName);
        }
        if (ParameterSetName == GetParameterSet)
        {
            parameters.Add("Name", Name);
            if (IsBound(nameof(ExpandResource)) && ExpandResource is not null)
            {
                parameters.Add("ExpandResource", ExpandResource);
            }
        }

        RunCommand($"{CmdletName}_{ParameterSetName}", parameters);
    }
}

/// <summary>
/// Remove-Az* shape: confirms unless -Force (-WhatIf still wins) and writes $true with -PassThru after a completed delete.
/// </summary>
public abstract class NetworkRemoveCmdletBase : NetworkCmdletBase
{
    [Alias("ResourceName")]
    [Parameter(Mandatory = true, HelpMessage = "The resource name.")]
    public string Name { get; set; } = "";

    [Parameter(Mandatory = true, HelpMessage = "The resource group name.")]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(Mandatory = false, HelpMessage = "Do not ask for confirmation.")]
    public SwitchParameter Force { get; set; }

    [Parameter(Mandatory = false, HelpMessage = "Writes $true after the resource is removed.")]
    public SwitchParameter PassThru { get; set; }

    protected abstract string CmdletName { get; }

    /// <summary>Resource description used in the confirmation message, for example "virtual network".</summary>
    protected abstract string ResourceDisplayName { get; }

    protected override void ProcessRecord()
    {
        bool whatIf = MyInvocation.BoundParameters.TryGetValue("WhatIf", out var whatIfValue) && LanguagePrimitives.IsTrue(whatIfValue);
        if ((whatIf || !Force.IsPresent)
            && !ShouldProcess($"{ResourceDisplayName} '{Name}' in resource group '{ResourceGroupName}'", "Remove"))
        {
            return;
        }

        var parameters = new Dictionary<string, object>
        {
            { "Name", Name },
            { "ResourceGroupName", ResourceGroupName },
        };

        if (RunCommand(CmdletName, parameters) && PassThru.IsPresent)
        {
            WriteObject(true);
        }
    }
}
