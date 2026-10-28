// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    public class GetResourceGroupProcessor : ResourcesProcessorBase<object, ResourceGroup>
    {
        public string SubscriptionId { get; set; } = string.Empty;

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";

    }
}
