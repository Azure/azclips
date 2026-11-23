// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;
/// <summary>Gets a resource group. External API: Microsoft.Resources ResourceGroups_Get.</summary>
public partial class GetResourceGroupProcessor : NetworkProcessorBase<object, ResourceGroup>
{
    /// <summary>The ID of the target subscription. The value must be an UUID.</summary>
    public string SubscriptionId { get; set; } = string.Empty;
    /// <summary>The name of the resource group to get. The name is case insensitive.</summary>
    public string ResourceGroupName { get; set; } = string.Empty;
    internal string Accept { get; set; } = string.Empty;
    protected override HttpMethod Method => HttpMethod.Get;

    protected override string CreateRequestUri() => $"{Endpoint}/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourcegroups/{Uri.EscapeDataString(ResourceGroupName)}?api-version=2025-04-01";
}
