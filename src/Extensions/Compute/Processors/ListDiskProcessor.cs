// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.Extensions.Compute.Models;

namespace Microsoft.Azure.Cirrus.Extensions.Compute.Processors;

/// <summary>
/// Lists managed disks in a resource group. Phase 9 multi-namespace stub: this is the
/// minimum-viable second-namespace leaf so the Compute extension can prove the
/// <c>[ExtensionVerb("vm")] [ExtensionVerb("disk")]</c> contract end-to-end. Wired through
/// <see cref="ExtensionIndex"/>'s <c>s_cliTable</c> as <c>disk list</c>.
/// </summary>
public class ListDiskProcessor : ComputeProcessorBase<Disk>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    public override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/disks?api-version=2025-01-02";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }
}
