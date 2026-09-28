// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors;

/// <summary>
/// Adds multi-id addressing to <see cref="GetResourceGroupDeploymentOperationProcessor"/>, matching the
/// Azure CLI <c>deployment operation group show</c> shape: one GET per id, accumulated into a single
/// array. When no ids are supplied the base single-operation behavior runs unchanged.
/// </summary>
public class GetResourceGroupDeploymentOperationProcessor_Orchestrator : GetResourceGroupDeploymentOperationProcessor
{
    public string[]? OperationIds { get; set; }

    public override async Task ProcessAsync()
    {
        if (OperationIds is null || OperationIds.Length == 0)
        {
            await base.ProcessAsync();
            return;
        }

        try
        {
            await BeforeProcessAsync();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            List<DeploymentOperation> operations = [];
            foreach (string operationId in OperationIds)
            {
                using HttpResponseMessage response = await CreateExtendedProcessor<GetResourceGroupDeploymentOperationProcessor>(p =>
                {
                    p.SubscriptionId = SubscriptionId;
                    p.ResourceGroupName = ResourceGroupName;
                    p.DeploymentName = DeploymentName;
                    p.OperationId = operationId;
                }).SendAsync();

                if (await response.Content.ReadFromJsonAsync<DeploymentOperation>(JsonOptions) is { } operation)
                {
                    operations.Add(operation);
                }
            }

            await WriteOutput(operations);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
