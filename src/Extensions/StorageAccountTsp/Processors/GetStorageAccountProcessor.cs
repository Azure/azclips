// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated from TypeSpec operation StorageAccounts_GetProperties
//   Source spec: specification/storage/Storage.Management
//   GET /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{Name}
//   Kind: get

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;
using _Response = Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models.StorageAccount;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors;

public class GetStorageAccountProcessor : StorageTspProcessorBase<_Response>
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // Body parameters (flattened from ).
    // Optional escape hatch for cases where the tsp emitter can't cleanly flatten
    // (e.g. array-of-model like VMSS networkProfile.networkInterfaceConfigurations).
    // Deep-merged into the built body just before the request goes out.
    public string? BodyJsonPatch { get; set; }
    public string? Expand { get; set; }

    /// <summary>
    /// Copies the generated command parameter surface to an atomic child
    /// processor created by an orchestrator.
    /// </summary>
    protected void CopyParametersTo(GetStorageAccountProcessor target)
    {
        target.SubscriptionId = SubscriptionId;
        target.Profile = Profile;
        target.NoWait = NoWait;
        target.ResourceGroupName = ResourceGroupName;
        target.Name = Name;
        target.BodyJsonPatch = BodyJsonPatch;
        target.Expand = Expand;
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string url = StorageAccountUri(ResourceGroupName, Name);
        if (Expand is not null)
        {
            url += "&$expand=" + Uri.EscapeDataString(
                System.Convert.ToString(Expand, System.Globalization.CultureInfo.InvariantCulture)!);
        }

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
