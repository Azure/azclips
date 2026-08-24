// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated from TypeSpec operation StorageAccounts_List
//   Source spec: specification/storage/Storage.Management@56e69a6
//   GET /subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts
//   Kind: list_by_sub

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;
using _Response = Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models.StorageAccountListResult;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors;

public class ListBySubscriptionStorageAccountProcessor : StorageTspProcessorBase<_Response>
{

    /// <summary>
    /// Copies the generated command parameter surface to an atomic child
    /// processor created by an orchestrator.
    /// </summary>
    protected void CopyParametersTo(ListBySubscriptionStorageAccountProcessor target)
    {
        target.SubscriptionId = SubscriptionId;
        target.Profile = Profile;
        target.NoWait = NoWait;
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string url = $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";


        HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("GET"), url);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
