// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated cmdlet for the StorageTsp extension.
using System.Collections;
using System.Management.Automation;
using System.Security;
using Microsoft.Azure.Azclips.Cmdlets.Common;

namespace Microsoft.Azure.Azclips.Cmdlets.StorageAccountTsp;

/// <summary>
/// Invokes <c>Remove-AzStorageAccount</c> for the StorageAccount resource.
/// </summary>
/// <example>
/// <code>Remove-AzStorageAccount -SubscriptionId &lt;subscription-id&gt; -ResourceGroupName &lt;resource-group&gt; -Name &lt;name&gt;</code>
/// </example>
[Cmdlet(VerbsCommon.Remove, "AzStorageAccount", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public class RemoveAzStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = false)] public string? Profile { get; set; }
    [Parameter(Mandatory = true)]  public string SubscriptionId { get; set; } = "";
    [Parameter(Mandatory = true)]  public string ResourceGroupName { get; set; } = "";
    [Parameter(Mandatory = false)] public SwitchParameter Force { get; set; }
    [Parameter(Mandatory = true)]  public string Name { get; set; } = "";

    protected override void ProcessRecord()
    {
        if (!Force.IsPresent && !ShouldProcess($"StorageTsp AzStorageAccount '{Name}' in resource group '{ResourceGroupName}'", "Remove")) return;

        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"]    = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"]              = Name,
        };
        parameters.AddIfBound(this, nameof(Profile), Profile);

        CmdletEngineRunner.Run(this, "StorageAccountTsp", "Remove-AzStorageAccount", "StorageAccountTspStorageAccountError", parameters, _cts.Token);
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
