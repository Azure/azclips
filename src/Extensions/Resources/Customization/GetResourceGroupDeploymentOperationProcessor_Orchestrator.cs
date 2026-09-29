// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Orchestrator (handwritten/customized) half of <c>deployment operation group show</c>.
    /// Azure CLI accepts several operation ids in one invocation and issues one GET per id, so this
    /// orchestrator owns the <c>--operation-ids</c> surface and fans out to the atomic
    /// <see cref="GetResourceGroupDeploymentOperationProcessor"/> via <c>CreateExtendedProcessor</c>,
    /// then writes the collected operations as a single array.
    /// </summary>
    public class GetResourceGroupDeploymentOperationProcessor_Orchestrator : GetResourceGroupDeploymentOperationProcessor
    {
        [ArgumentInfo(
        Required = true,
        Description = @"A list of operation ids to show.",
        SerializedName = @"OperationIds")]
        public string[]? OperationIds { get; set; }

        public override async Task ProcessAsync()
        {
            try
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);

                List<string> operationIds = ResolveOperationIds();
                List<DeploymentOperation> operations = new(operationIds.Count);

                foreach (string operationId in operationIds)
                {
                    GetResourceGroupDeploymentOperationProcessor processor = CreateExtendedProcessor<GetResourceGroupDeploymentOperationProcessor>(p =>
                    {
                        p.SubscriptionId = SubscriptionId;
                        p.ResourceGroupName = ResourceGroupName;
                        p.DeploymentName = DeploymentName;
                        p.OperationId = operationId;
                    });

                    using HttpResponseMessage response = await processor.SendAsync();
                    string rawBody = await response.Content.ReadAsStringAsync();

                    DeploymentOperation? operation;
                    try
                    {
                        operation = JsonSerializer.Deserialize<DeploymentOperation>(rawBody, JsonOptions);
                    }
                    catch (JsonException ex)
                    {
                        await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException($"Failed to deserialize response body into {nameof(DeploymentOperation)}. Raw body: {rawBody}", ex);
                    }

                    if (operation is not null)
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

        // OperationIds is the CLI surface; OperationId is inherited from the atomic processor and
        // accepted as a single-id shorthand so the orchestrator stays a strict superset of its base.
        private List<string> ResolveOperationIds()
        {
            List<string> ids = (OperationIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            if (ids.Count == 0 && !string.IsNullOrWhiteSpace(OperationId))
            {
                ids.Add(OperationId);
            }

            if (ids.Count == 0)
            {
                throw new ArgumentException("At least one operation id must be provided.");
            }

            return ids;
        }
    }
}
