// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

public class UpdateFederatedCredentialProcessor_OrchestratorTests
{
    // FederatedCredentialValidatorTests covers every claims-matching-expression case.
    [Fact]
    public async Task ProcessAsync_ClaimsMatchingExpressionVersionWithoutValue_ThrowsInvalidInput()
    {
        var processor = CreateProcessor<UpdateFederatedCredentialProcessor_Orchestrator>();
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.IdentityName = "id1";
        processor.Name = "fic1";
        processor.ClaimsMatchingExpressionVersion = 1;

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal(
            "Both 'ClaimsMatchingExpressionVersion' and 'ClaimsMatchingExpressionValue' must be specified together.",
            ex.Message);
    }
    [Fact]
    public async Task ProcessAsync_InvalidForceString_ThrowsInvalidInput()
    {
        // --force-string is only parsed after the existing credential is fetched, so the GET is allowed.
        var processor = CreateProcessor<UpdateFederatedCredentialProcessor_Orchestrator>(
            GetOnlyPipeline("""{ "name": "fic1", "properties": { "issuer": "https://issuer", "subject": "sub", "audiences": ["api://AzureADTokenExchange"] } }"""));
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.IdentityName = "id1";
        processor.Name = "fic1";
        processor.Set = ["properties.subject=new"];
        processor.ForceString = "maybe";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal("Invalid --force-string value 'maybe'.", ex.Message);
    }
}
