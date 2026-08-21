// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated from swagger operation StorageAccounts_ListByResourceGroup
//   GET /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts
//   Kind: list_by_rg

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;
using _Response = Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models.StorageAccountListResult;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors;

public class ListStorageAccountProcessor : StorageTspProcessorBase<_Response>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    /// <summary>
    /// Copies the generated command parameter surface to an atomic child
    /// processor created by an orchestrator.
    /// </summary>
    protected void CopyParametersTo(ListStorageAccountProcessor target)
    {
        target.SubscriptionId = SubscriptionId;
        target.Profile = Profile;
        target.NoWait = NoWait;
        target.ResourceGroupName = ResourceGroupName;
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string url = StorageAccountCollectionUriByResourceGroup(ResourceGroupName);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
