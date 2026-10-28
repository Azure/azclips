// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Compute.Models;
using Microsoft.Azure.Azclips.ExtensionRuntime;

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
public class ListDiskProcessor : ComputeProcessorBase<object, DiskListResult>
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionIdValidator.Require(SubscriptionId)}/resourceGroups/{ResourceGroupName}/providers/Microsoft.Compute/disks?api-version=2025-01-02";

    private string? nextLink(DiskListResult body) => body.NextLink;
    private Disk[]? pageItems(DiskListResult body) => body.Value;

    protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DiskListResult? body)
    {
        await body.WriteOutputWithPaginationAsync(pageItems, nextLink, CreateRequestMessage, WriteOutput, Publisher, HttpPipeline, JsonOptions);
        return true;
    }
}
