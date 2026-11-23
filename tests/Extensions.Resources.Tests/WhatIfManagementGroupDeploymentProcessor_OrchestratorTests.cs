// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using Moq;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>
/// Offline input-validation tests for <see cref="WhatIfManagementGroupDeploymentProcessor_Orchestrator"/>
/// (az deployment mg what-if). ManagementGroupDeploymentOrchestratorBaseTests covers every shared input check; this proves the
/// orchestrator runs them before any request.
/// </summary>
public class WhatIfManagementGroupDeploymentProcessor_OrchestratorTests
{
    private const string TemplateUri = "https://example.com/t.json";

    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_MissingManagementGroupId_ThrowsInvalidInput()
    {
        var processor = Initialize(
            new WhatIfManagementGroupDeploymentProcessor_Orchestrator
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

    [Fact]
    public async Task ProcessAsync_InvalidResultFormat_ThrowsInvalidInput()
    {
        var processor = Initialize(
            new WhatIfManagementGroupDeploymentProcessor_Orchestrator
            {
                ManagementGroupId = "mg1",
                ResultFormat = "ResourceId",
                Name = "dep",
                TemplateUri = TemplateUri,
                Location = "westus",
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // Current behaviour: what-if checks ResultFormat before the shared ManagementGroupId/Location checks.
    [Fact]
    public async Task ProcessAsync_InvalidResultFormatAndMissingManagementGroupId_ThrowsInvalidInputForResultFormat()
    {
        var processor = Initialize(
            new WhatIfManagementGroupDeploymentProcessor_Orchestrator
            {
                ManagementGroupId = "",
                ResultFormat = "ResourceId",
                Name = "dep",
                TemplateUri = TemplateUri,
                Location = "westus",
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }}
