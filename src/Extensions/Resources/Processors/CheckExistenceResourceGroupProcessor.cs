// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>Checks whether a resource group exists.</summary>
public class CheckExistenceResourceGroupProcessor : ResourcesProcessorBase<object, object>
{
    public string SubscriptionId { get; set; } = string.Empty;

    public string ResourceGroupName { get; set; } = string.Empty;

    protected override HttpMethod Method => HttpMethod.Head;

    protected override string CreateRequestUri() =>
        $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}?api-version=2025-04-01";
}
