// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// FederatedCredentialValidatorTests covers every claims-matching-expression case.
public class CreateFederatedCredentialProcessor_OrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_ClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput()
    {
        var processor = CreateProcessor<CreateFederatedCredentialProcessor_Orchestrator>();
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.IdentityName = "id1";
        processor.Name = "fic1";
        processor.Issuer = "https://token.actions.githubusercontent.com";
        processor.ClaimsMatchingExpressionVersion = 1;

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal(
            "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.",
            ex.Message);
    }
}