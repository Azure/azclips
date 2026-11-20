// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors;

/// <summary>
/// Reads the location of a resource group, for creates whose Location defaults to it.
/// </summary>
internal static class ResourceGroupLocationResolver
{
    /// <summary>
    /// Sends <paramref name="getResourceGroup"/>, which the caller creates with
    /// <c>CreateExtendedProcessor&lt;GetResourceGroupsProcessor&gt;</c>, and returns the resource
    /// group's location. A failed GET throws its service error, so the caller sends no PUT.
    /// </summary>
    internal static async Task<string> GetLocationAsync(
        GetResourceGroupsProcessor getResourceGroup,
        IEventPublisher publisher,
        JsonSerializerOptions jsonOptions)
    {
        ResourceGroup resourceGroup = await ChildResourceReader.ReadAsync<ResourceGroup>(getResourceGroup, publisher, jsonOptions);
        if (string.IsNullOrWhiteSpace(resourceGroup.Location))
        {
            throw AzclipsErrorException.InvalidInput(
                $"Could not read the location of resource group '{getResourceGroup.ResourceGroupName}'. Specify the location.");
        }

        return resourceGroup.Location;
    }
}