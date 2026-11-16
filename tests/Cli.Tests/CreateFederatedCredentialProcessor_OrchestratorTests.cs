// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Moq;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// FederatedCredentialValidatorTests covers every claims-matching-expression case.
public class CreateFederatedCredentialProcessor_OrchestratorTests
{
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_ClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput()
    {
        var processor = Initialize(
            new CreateFederatedCredentialProcessor_Orchestrator
            {
                SubscriptionId = TestSubscriptionId,
                ResourceGroup = "rg1",
                IdentityName = "id1",
                Name = "fic1",
                Issuer = "https://token.actions.githubusercontent.com",
                ClaimsMatchingExpressionVersion = 1,
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }
}
