// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Moq;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// ManagedIdentityResolverTests covers every input case; these prove the orchestrator calls each check before any request.
public class ShowIdentityProcessor_OrchestratorTests
{
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_MissingNameAndResourceGroup_ThrowsInvalidInput()
    {
        var processor = Initialize(new ShowIdentityProcessor_Orchestrator(), _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(MissingIdentityAddressMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidResourceId_ThrowsInvalidInput()
    {
        var processor = Initialize(new ShowIdentityProcessor_Orchestrator { Ids = ["not-a-resource-id"] }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(InvalidResourceIdMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }
}
