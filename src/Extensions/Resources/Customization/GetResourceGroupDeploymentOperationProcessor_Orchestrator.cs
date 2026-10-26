// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Customization over <see cref="GetResourceGroupDeploymentOperationProcessor"/> that accepts several
    /// operation ids and returns one list. ARM has no batch read for deployment operations, so every id is
    /// fetched by its own extended processor and the results are written once.
    /// </summary>
    public class GetResourceGroupDeploymentOperationProcessor_Orchestrator : GetResourceGroupDeploymentOperationProcessor
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
                    using HttpResponseMessage response = await CreateExtendedProcessor<GetResourceGroupDeploymentOperationProcessor>(processor =>
                    {
                        processor.SubscriptionId = SubscriptionId;
                        processor.ResourceGroupName = ResourceGroupName;
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

    internal static class DeploymentOperationFanOut
    {
        internal static void ThrowIfEmpty(List<string> operationIds)
        {
            if (operationIds.Count == 0)
            {
                throw AzclipsErrorException.MissingRequiredParameters("At least one deployment operation id is required.");
            }
        }

        // Same body handling as the base ProcessAsync, applied to every fanned-out child response.
        internal static async Task<DeploymentOperation?> ReadOperationAsync(HttpResponseMessage response, IEventPublisher publisher, JsonSerializerOptions jsonOptions)
        {
            if (response.Content is null)
            {
                await publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                return null;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                await publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<DeploymentOperation>(rawBody, jsonOptions);
            }
            catch (JsonException ex)
            {
                await publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                throw new InvalidOperationException($"Failed to deserialize response body into {nameof(DeploymentOperation)}. Raw body: {rawBody}", ex);
            }
        }
    }
}
