// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Lists resource groups in a subscription. ARM list endpoints return an envelope
    /// <c>{ "value": [...], "nextLink": "..." }</c>; this processor unwraps the envelope and
    /// surfaces the flat array of resource groups (first page only; <c>nextLink</c> is not followed).
    /// </summary>
    public class ListResourceGroupProcessor : ResourcesProcessorBase<object, ResourceGroupListResult>
    {
        protected override HttpMethod Method => HttpMethod.Get;

        protected override string CreateRequestUri() =>
            $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups?api-version=2025-04-01";

        protected override object SelectOutput(ResourceGroupListResult body) => body.Value ?? [];
    }
}