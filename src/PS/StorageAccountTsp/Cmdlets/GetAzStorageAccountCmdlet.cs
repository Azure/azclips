// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated cmdlet for the StorageAccountTsp extension.
// Source: TypeSpec (specification/storage/Storage.Management)
using System.Collections;
using System.Management.Automation;
using System.Security;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.StorageAccountTsp;

/// <summary>
/// Invokes <c>Get-AzStorageAccount</c> for the StorageAccount resource.
/// </summary>
/// <example>
/// <code>Get-AzStorageAccount -SubscriptionId &lt;subscription-id&gt; -ResourceGroupName &lt;resource-group&gt; -Name &lt;name&gt;</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "AzStorageAccount", DefaultParameterSetName = "List")]
public class GetAzStorageAccountCmdlet : PSCmdlet
{
    private const string ParamSetGet = "Get";
    private const string ParamSetList = "List";
    private const string ParamSetListBySubscription = "ListBySubscription";

    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = false)] public string? Profile { get; set; }

    [Parameter(ParameterSetName = ParamSetGet, Mandatory = true)]
    [Parameter(ParameterSetName = ParamSetList, Mandatory = true)]
    [Parameter(ParameterSetName = ParamSetListBySubscription, Mandatory = true)]
    public string SubscriptionId { get; set; } = "";

    [Parameter(ParameterSetName = ParamSetGet, Mandatory = true)]
    [Parameter(ParameterSetName = ParamSetList, Mandatory = true)]
    public string ResourceGroupName { get; set; } = "";

    [Parameter(ParameterSetName = ParamSetGet, Mandatory = true)]
    public string? Name { get; set; }

    [Parameter(ParameterSetName = ParamSetGet, Mandatory = false)]
    public string? Expand { get; set; }


    // Escape hatch: raw JSON deep-merged into the built body just before send.
    // Used for cases the tsp emitter can't cleanly flatten (array-of-model,
    // required nested properties that our whitelist doesn't cover, etc.).
    [Parameter(Mandatory = false)] public string? BodyJsonPatch { get; set; }

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object> { ["SubscriptionId"] = SubscriptionId };
        parameters.AddIfBound(this, nameof(ResourceGroupName), ResourceGroupName);
        parameters.AddIfBound(this, nameof(Name), Name);
        parameters.AddIfBound(this, nameof(Profile), Profile);
        parameters.AddIfBound(this, nameof(BodyJsonPatch), BodyJsonPatch);
        parameters.AddIfBound(this, "Expand", Expand);

        // Command key matches the InvocationName registered in ExtensionIndex.s_psTable.
        // The suffix follows PowerShell's ParameterSetName so a single cmdlet dispatches
        // to one of 3 processors. When PS binds to the fallback "Default"
        // set (only variant, no suffix in the registered InvocationName), strip the suffix.
        string commandName = ParameterSetName == "Default" || string.IsNullOrEmpty(ParameterSetName)
            ? "Get-AzStorageAccount"
            : $"Get-AzStorageAccount_{ParameterSetName}";
        CmdletEngineRunner.Run(this, "StorageAccountTsp", commandName, "StorageAccountTspAzStorageAccountError", parameters, _cts.Token, enumerateOutput: true);
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
