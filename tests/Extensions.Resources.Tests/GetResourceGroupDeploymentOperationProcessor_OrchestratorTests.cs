// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>
/// Offline tests for <see cref="GetResourceGroupDeploymentOperationProcessor_Orchestrator"/>
/// (az deployment operation group show / Get-AzResourceGroupDeploymentOperation).
/// </summary>
public class GetResourceGroupDeploymentOperationProcessor_OrchestratorTests
{
    private const string SubscriptionId = "282bb33f-067d-4179-a7a4-b833c13af735";

    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_NoOperationIds_ThrowsMissingRequiredArgument()
    {
        var processor = Initialize(
            new GetResourceGroupDeploymentOperationProcessor_Orchestrator
            {
                SubscriptionId = SubscriptionId,
                ResourceGroupName = "test-rg",
                DeploymentName = "dep",
                OperationIds = [],
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("At least one deployment operation id is required.", Assert.IsType<MissingRequiredArgumentException>(exception).Message);
    }
}
