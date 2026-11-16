// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;
using static Extensions.Resources.Tests.ResourcesProcessorTestHelpers;

namespace Extensions.Resources.Tests;

/// <summary>
/// Offline input-validation tests for the management-group deployment orchestrators built on
/// <see cref="ManagementGroupDeploymentOrchestratorBase{TRequest, TResponse}"/>
/// (az deployment mg create|validate|what-if, New-/Test-AzManagementGroupDeployment).
/// </summary>
public class ManagementGroupDeploymentOrchestratorBaseTests
{
    public static TheoryData<string> Commands => ["create", "validate", "what-if"];

    public static TheoryData<string, string?> CommandsWithBlankValues
    {
        get
        {
            var data = new TheoryData<string, string?>();
            foreach (string command in new[] { "create", "validate", "what-if" })
            {
                foreach (string? value in new[] { null, "", "   " })
                {
                    data.Add(command, value);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CommandsWithBlankValues))]
    public async Task ProcessAsync_MissingManagementGroupId_ThrowsInvalidInput(string command, string? managementGroupId)
    {
        IProcessor processor = CreateProcessor(command, managementGroupId: managementGroupId!);

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("ManagementGroupId must be provided.", ex.Message);
    }

    [Theory]
    [MemberData(nameof(CommandsWithBlankValues))]
    public async Task ProcessAsync_MissingLocation_ThrowsInvalidInput(string command, string? location)
    {
        IProcessor processor = CreateProcessor(command, location: location!);

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("Location must be provided.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_MissingManagementGroupIdAndLocation_ThrowsInvalidInputForManagementGroupId()
    {
        IProcessor processor = CreateProcessor("create", managementGroupId: "", location: "");

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("ManagementGroupId must be provided.", ex.Message);
    }

    [Theory]
    [MemberData(nameof(Commands))]
    public async Task ProcessAsync_ParametersUriAndParametersFile_ThrowsInvalidInput(string command)
    {
        IProcessor processor = CreateProcessor(
            command, parametersUri: "https://example.com/p.json", parametersFile: "p.json");

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("Provide at most one of ParametersUri or ParametersFile.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhatIfInvalidResultFormat_ThrowsInvalidInput()
    {
        var processor = (WhatIfManagementGroupDeploymentProcessor_Orchestrator)CreateProcessor("what-if");
        processor.ResultFormat = "ResourceId";

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_WhatIfInvalidResultFormatAndMissingManagementGroupId_ThrowsInvalidInputForResultFormat()
    {
        // Current behaviour: what-if checks ResultFormat before the ManagementGroupId/Location inputs.
        var processor = (WhatIfManagementGroupDeploymentProcessor_Orchestrator)CreateProcessor("what-if", managementGroupId: "");
        processor.ResultFormat = "ResourceId";

        InvalidInputException ex = await Assert.ThrowsAsync<InvalidInputException>(() => processor.ProcessAsync());

        Assert.Equal("ResultFormat must be one of 'ResourceIdOnly' or 'FullResourcePayloads'.", ex.Message);
    }

    // Valid inputs unless a test overrides one of them.
    private static IProcessor CreateProcessor(
        string command,
        string managementGroupId = "mg1",
        string location = "westus",
        string? parametersUri = null,
        string? parametersFile = null)
    {
        void Apply<TRequest, TResponse>(ManagementGroupDeploymentOrchestratorBase<TRequest, TResponse> p)
            where TRequest : class
            where TResponse : class
        {
            p.ManagementGroupId = managementGroupId;
            p.Location = location;
            p.Name = "dep";
            p.TemplateUri = "https://example.com/t.json";
            p.ParametersUri = parametersUri;
            p.ParametersFile = parametersFile;
        }

        return command switch
        {
            "create" => ResourcesProcessorTestHelpers.CreateProcessor(new CreateManagementGroupDeploymentProcessor_Orchestrator(), Apply),
            "validate" => ResourcesProcessorTestHelpers.CreateProcessor(new ValidateManagementGroupDeploymentProcessor_Orchestrator(), Apply),
            "what-if" => ResourcesProcessorTestHelpers.CreateProcessor(new WhatIfManagementGroupDeploymentProcessor_Orchestrator(), Apply),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };
    }
}
