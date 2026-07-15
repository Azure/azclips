// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

/// <summary>
/// Lists managed disks in a resource group. Minimum-viable second-namespace leaf — proves the
/// Compute extension's <c>[ExtensionVerb("vm")] [ExtensionVerb("disk")]</c> contract holds
/// end-to-end. Wired through <see cref="ExtensionIndex"/>'s <c>s_cliTable</c> as <c>disk list</c>.
/// <para>
/// The ARM list endpoint returns an envelope <c>{ "value": [...] }</c>; this processor writes its
/// <c>value</c> array. Pagination (<c>nextLink</c>) is intentionally not followed yet.
/// </para>
/// </summary>
public class ListDiskProcessor : ComputeProcessorBase<DiskListResult>
{
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override async Task<HttpRequestMessage> CreateRequestMessage()
    {
        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string uri = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/disks?api-version=2025-01-02";
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uri);
        await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
        return request;
    }

    protected override object SelectOutput(DiskListResult body) => body.Value ?? [];
}
