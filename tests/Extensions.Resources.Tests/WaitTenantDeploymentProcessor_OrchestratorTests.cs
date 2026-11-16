// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>Offline input-validation tests for <see cref="WaitTenantDeploymentProcessor_Orchestrator"/> (az deployment tenant wait).</summary>
public class WaitTenantDeploymentProcessor_OrchestratorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessAsync_NoWaitCondition_ThrowsInvalidInput(string? custom)
    {
        var processor = CreateProcessor(p => p.Custom = custom);

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidCustomExpression_ThrowsInvalidInput()
    {
        var processor = CreateProcessor(p => p.Custom = "properties.[");

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        // JmesPathConditionTests covers the expression cases; the parser's own text after the colon is not asserted.
        Assert.StartsWith("--custom expression 'properties.[' is not valid JMESPath: ", ex.Message, StringComparison.Ordinal);
    }

    private static WaitTenantDeploymentProcessor_Orchestrator CreateProcessor(Action<WaitTenantDeploymentProcessor_Orchestrator> configure) =>
        ResourcesProcessorTestHelpers.CreateProcessor(new WaitTenantDeploymentProcessor_Orchestrator(), p =>
        {
            p.DeploymentName = "dep";
            p.Interval = 0;
            p.Timeout = 5;
            configure(p);
        });
}
