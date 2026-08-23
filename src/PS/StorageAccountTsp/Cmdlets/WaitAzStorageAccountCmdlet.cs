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
/// Invokes <c>Wait-AzStorageAccount</c> for the StorageAccount resource.
/// </summary>
/// <example>
/// <code>Wait-AzStorageAccount -SubscriptionId &lt;subscription-id&gt; -ResourceGroupName &lt;resource-group&gt; -Name &lt;name&gt;</code>
/// </example>
[Cmdlet("Wait", "AzStorageAccount")]
public class WaitAzStorageAccountCmdlet : PSCmdlet
{
    private readonly CancellationTokenSource _cts = new();

    [Parameter(Mandatory = false)] public string? Profile { get; set; }
    [Parameter(Mandatory = true)]  public string SubscriptionId { get; set; } = "";
    [Parameter(Mandatory = true)]  public string ResourceGroupName { get; set; } = "";
    [Parameter(Mandatory = true)]  public string Name { get; set; } = "";

    protected override void ProcessRecord()
    {
        var parameters = new Dictionary<string, object>
        {
            ["SubscriptionId"]    = SubscriptionId,
            ["ResourceGroupName"] = ResourceGroupName,
            ["Name"] = Name,
        };
        parameters.AddIfBound(this, nameof(Profile), Profile);

        CmdletEngineRunner.Run(this, "StorageAccountTsp", "Wait-AzStorageAccount", "StorageAccountTspStorageAccountError", parameters, _cts.Token);
    }

    protected override void EndProcessing() => _cts.Dispose();
    protected override void StopProcessing() => _cts.Cancel();
}
