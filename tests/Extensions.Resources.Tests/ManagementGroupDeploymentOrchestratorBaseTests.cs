// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Extensions.Resources.Tests;

/// <summary>
/// Input-validation tests for the shared <see cref="ManagementGroupDeploymentOrchestratorBase{TRequest, TResponse}"/>
/// checks that the create, validate and what-if management-group deployment orchestrators all call.
/// </summary>
public class ManagementGroupDeploymentOrchestratorBaseTests
{
    private const string TemplateUri = "https://example.com/t.json";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildProperties_MissingManagementGroupId_ThrowsInvalidInput(string? managementGroupId)
    {
        var orchestrator = new TestOrchestrator { ManagementGroupId = managementGroupId!, Location = "westus", Name = "dep", TemplateUri = TemplateUri };

        Exception? exception = Record.Exception(orchestrator.InvokeBuildProperties);

        Assert.Equal("ManagementGroupId must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildProperties_MissingLocation_ThrowsInvalidInput(string? location)
    {
        var orchestrator = new TestOrchestrator { ManagementGroupId = "mg1", Location = location!, Name = "dep", TemplateUri = TemplateUri };

        Exception? exception = Record.Exception(orchestrator.InvokeBuildProperties);

        Assert.Equal("Location must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void BuildProperties_MissingManagementGroupIdAndLocation_ThrowsInvalidInputForManagementGroupId()
    {
        var orchestrator = new TestOrchestrator { ManagementGroupId = "", Location = "", Name = "dep", TemplateUri = TemplateUri };

        Exception? exception = Record.Exception(orchestrator.InvokeBuildProperties);

        Assert.Equal("ManagementGroupId must be provided.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public void BuildProperties_ParametersUriAndParametersFile_ThrowsInvalidInput()
    {
        var orchestrator = new TestOrchestrator
        {
            ManagementGroupId = "mg1",
            Location = "westus",
            Name = "dep",
            TemplateUri = TemplateUri,
            ParametersUri = "https://example.com/p.json",
            ParametersFile = "p.json",
        };

        Exception? exception = Record.Exception(orchestrator.InvokeBuildProperties);

        Assert.Equal("Provide at most one of ParametersUri or ParametersFile.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    // The base is abstract and BuildProperties is protected, so a minimal subclass exposes it for testing.
    private sealed class TestOrchestrator : ManagementGroupDeploymentOrchestratorBase<object, object>
    {
        public DeploymentProperties InvokeBuildProperties() => BuildProperties();
    }
}
