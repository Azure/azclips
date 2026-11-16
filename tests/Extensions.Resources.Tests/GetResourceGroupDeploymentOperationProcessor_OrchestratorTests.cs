// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>
/// Offline tests for <see cref="GetResourceGroupDeploymentOperationProcessor_Orchestrator"/>
/// (az deployment operation group show / Get-AzResourceGroupDeploymentOperation).
/// </summary>
public class GetResourceGroupDeploymentOperationProcessor_OrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_NoOperationIds_ThrowsMissingRequiredArgument()
    {
        var processor = CreateProcessor(new GetResourceGroupDeploymentOperationProcessor_Orchestrator(), p =>
        {
            p.SubscriptionId = SubscriptionId;
            p.ResourceGroupName = "test-rg";
            p.DeploymentName = "dep";
            p.OperationIds = [];
        });

        MissingRequiredArgumentException ex = await Assert.ThrowsAsync<MissingRequiredArgumentException>(() => processor.ProcessAsync());

        Assert.Equal("At least one deployment operation id is required.", ex.Message);
    }
}
