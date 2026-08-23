// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// Generated from TypeSpec operation StorageAccounts_Delete
//   Source spec: specification/storage/Storage.Management
//   DELETE /subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Storage/storageAccounts/{Name}
//   Kind: delete

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Models;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors;

public class DeleteStorageAccountProcessor : StorageTspProcessorBase<object>
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Copies the generated command parameter surface to an atomic child
    /// processor created by an orchestrator.
    /// </summary>
    protected void CopyParametersTo(DeleteStorageAccountProcessor target)
    {
        target.SubscriptionId = SubscriptionId;
        target.Profile = Profile;
        target.NoWait = NoWait;
        target.ResourceGroupName = ResourceGroupName;
        target.Name = Name;
    }

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, StorageAccountUri(ResourceGroupName, Name));
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
