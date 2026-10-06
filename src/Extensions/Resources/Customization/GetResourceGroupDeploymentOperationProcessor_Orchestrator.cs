// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Customization over <see cref="GetResourceGroupDeploymentOperationProcessor"/> that accepts several
    /// operation ids and returns one array. ARM has no batch read for deployment operations, so the first id
    /// travels through the normal processor lifecycle and the remainder are fetched as extended processors.
    /// </summary>
    public class GetResourceGroupDeploymentOperationProcessor_Orchestrator : GetResourceGroupDeploymentOperationProcessor
    {
        public List<string> OperationIds { get; set; } = [];

        protected override Task BeforeProcessAsync()
        {
            OperationId = DeploymentOperationFanOut.FirstId(OperationIds);
            return base.BeforeProcessAsync();
        }

        protected override async Task<bool> BeforeWriteOutputAsyncAndReturn(DeploymentOperation? body)
        {
            List<DeploymentOperation> operations = body is null ? [] : [body];

            foreach (string operationId in OperationIds.Skip(1))
            {
                GetResourceGroupDeploymentOperationProcessor child = CreateExtendedProcessor<GetResourceGroupDeploymentOperationProcessor>(processor =>
                {
                    processor.SubscriptionId = SubscriptionId;
                    processor.ResourceGroupName = ResourceGroupName;
                    processor.DeploymentName = DeploymentName;
                    processor.OperationId = operationId;
                });

                using HttpResponseMessage response = await child.SendAsync();
                if (await response.Content.ReadFromJsonAsync<DeploymentOperation>(JsonOptions) is { } operation)
                {
                    operations.Add(operation);
                }
            }

            await WriteOutput(operations);
            return true;
        }
    }

    internal static class DeploymentOperationFanOut
    {
        internal static string FirstId(List<string> operationIds) =>
            operationIds.Count > 0
                ? operationIds[0]
                : throw AzclipsErrorException.MissingRequiredParameters("At least one deployment operation id is required.");
    }
}
