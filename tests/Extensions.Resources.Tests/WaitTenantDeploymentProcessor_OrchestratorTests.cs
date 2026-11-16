// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>Offline input-validation tests for <see cref="WaitTenantDeploymentProcessor_Orchestrator"/> (az deployment tenant wait).</summary>
public class WaitTenantDeploymentProcessor_OrchestratorTests
{
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ProcessAsync_NoWaitCondition_ThrowsInvalidInput(string? custom)
    {
        var processor = Initialize(
            new WaitTenantDeploymentProcessor_Orchestrator
            {
                DeploymentName = "dep",
                Interval = 0,
                Timeout = 5,
                Custom = custom,
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // JmesPathConditionTests covers the expression cases; the parser's own text after the colon is not asserted.
    [Fact]
    public async Task ProcessAsync_InvalidCustomExpression_ThrowsInvalidInput()
    {
        var processor = Initialize(
            new WaitTenantDeploymentProcessor_Orchestrator
            {
                DeploymentName = "dep",
                Interval = 0,
                Timeout = 5,
                Custom = "properties.[",
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.StartsWith(
            "--custom expression 'properties.[' is not valid JMESPath: ",
            Assert.IsType<InvalidInputException>(exception).Message,
            StringComparison.Ordinal);
    }
}
