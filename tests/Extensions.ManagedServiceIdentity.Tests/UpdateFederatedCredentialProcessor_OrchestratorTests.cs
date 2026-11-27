// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Moq;
using static Extensions.ManagedServiceIdentity.Tests.IdentityProcessorTestHelpers;

namespace Extensions.ManagedServiceIdentity.Tests;

public class UpdateFederatedCredentialProcessor_OrchestratorTests
{
    private const string ExistingCredential =
        """{ "name": "fic1", "properties": { "issuer": "https://issuer", "subject": "sub", "audiences": ["api://AzureADTokenExchange"] } }""";

    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    // FederatedCredentialValidatorTests covers every claims-matching-expression case.
    [Fact]
    public async Task ProcessAsync_ClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput()
    {
        var processor = Initialize(
            new UpdateFederatedCredentialProcessor_Orchestrator
            {
                SubscriptionId = TestSubscriptionId,
                ResourceGroup = "rg1",
                IdentityName = "id1",
                Name = "fic1",
                ClaimsMatchingExpressionVersion = 1,
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // --force-string is only parsed after the existing credential is fetched, so the GET is allowed but no write is.
    [Fact]
    public async Task ProcessAsync_InvalidForceString_ThrowsInvalidInput()
    {
        _mockPipeline.RespondToGet(ExistingCredential);
        var processor = Initialize(
            new UpdateFederatedCredentialProcessor_Orchestrator
            {
                SubscriptionId = TestSubscriptionId,
                ResourceGroup = "rg1",
                IdentityName = "id1",
                Name = "fic1",
                Set = ["properties.subject=new"],
                ForceString = "maybe",
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Invalid --force-string value 'maybe'.", Assert.IsType<InvalidInputException>(exception).Message);
    }
}
