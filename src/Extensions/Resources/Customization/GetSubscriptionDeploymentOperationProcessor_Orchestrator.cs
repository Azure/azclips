// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
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
                GetSubscriptionDeploymentOperationProcessor child = CreateExtendedProcessor<GetSubscriptionDeploymentOperationProcessor>(processor =>
                {
                    processor.SubscriptionId = SubscriptionId;
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
}
