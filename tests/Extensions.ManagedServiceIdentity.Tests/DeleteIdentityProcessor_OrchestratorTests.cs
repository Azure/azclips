// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Moq;
using static Extensions.ManagedServiceIdentity.Tests.IdentityProcessorTestHelpers;

namespace Extensions.ManagedServiceIdentity.Tests;

// ManagedIdentityResolverTests covers every input case; these prove the orchestrator calls each check before any request.
public class DeleteIdentityProcessor_OrchestratorTests
{
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_MissingNameAndResourceGroup_ThrowsInvalidInput()
    {
        var processor = Initialize(new DeleteIdentityProcessor_Orchestrator(), _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(MissingIdentityAddressMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidResourceId_ThrowsInvalidInput()
    {
        var processor = Initialize(new DeleteIdentityProcessor_Orchestrator { Ids = ["not-a-resource-id"] }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(InvalidResourceIdMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }
}
