// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>
/// Offline input-validation tests for <see cref="CreateManagementGroupDeploymentProcessor_Orchestrator"/>
/// (az deployment mg create, New-AzManagementGroupDeployment). ManagementGroupDeploymentOrchestratorBaseTests covers every shared input check; this proves the
/// orchestrator runs them before any request.
/// </summary>
public class CreateManagementGroupDeploymentProcessor_OrchestratorTests
{
    private const string TemplateUri = "https://example.com/t.json";

    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_MissingManagementGroupId_ThrowsInvalidInput()
    {
        var processor = Initialize(
            new CreateManagementGroupDeploymentProcessor_Orchestrator
            {
                ManagementGroupId = "",
                Name = "dep",
                TemplateUri = TemplateUri,
                Location = "westus",
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("ManagementGroupId must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }
}
