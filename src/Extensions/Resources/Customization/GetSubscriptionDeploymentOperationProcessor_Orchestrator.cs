// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Subscription-scoped counterpart of
    /// <see cref="GetResourceGroupDeploymentOperationProcessor_Orchestrator"/>.
    /// </summary>
    public class GetSubscriptionDeploymentOperationProcessor_Orchestrator : GetSubscriptionDeploymentOperationProcessor
    {
        public List<string> OperationIds { get; set; } = [];

        public override async Task ProcessAsync()
        {
            DeploymentOperationFanOut.ThrowIfEmpty(OperationIds);

            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                List<DeploymentOperation> operations = [];
                foreach (string operationId in OperationIds)
                {
                    using HttpResponseMessage response = await CreateExtendedProcessor<GetSubscriptionDeploymentOperationProcessor>(processor =>
                    {
                        processor.SubscriptionId = SubscriptionId;
                        processor.DeploymentName = DeploymentName;
                        processor.OperationId = operationId;
                    }).SendAsync();

                    if (await DeploymentOperationFanOut.ReadOperationAsync(response, Publisher, JsonOptions) is { } operation)
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
}
