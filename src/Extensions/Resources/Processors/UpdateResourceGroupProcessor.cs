// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

public class UpdateResourceGroupProcessor : ResourcesProcessorBase<ResourceGroupPatchable, ResourceGroup>
{
    public string SubscriptionId { get; set; } = string.Empty;

    [ArgumentInfo(
        Required = true,
        Description = "The name of the resource group.",
        SerializedName = "ResourceGroupName")]
    public string ResourceGroupName { get; set; } = string.Empty;

    [ArgumentInfo(
        Required = false,
        Description = "The tags attached to the resource group.",
        SerializedName = "Tags")]
    public IDictionary<string, string>? Tags { get; set; }

    protected override HttpMethod Method => HttpMethod.Patch;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}?api-version=2025-04-01";

    protected override ResourceGroupPatchable CreateRequestBody() => new() { Tags = Tags };
}
