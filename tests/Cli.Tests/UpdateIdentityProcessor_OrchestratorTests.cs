// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// ManagedIdentityResolverTests covers every input case; these prove the orchestrator calls each check before any request.
public class UpdateIdentityProcessor_OrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_MissingNameAndResourceGroup_ThrowsInvalidInput()
    {
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(profile: null);

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal(MissingIdentityAddressMessage, ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidResourceId_ThrowsInvalidInput()
    {
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(profile: null);
        processor.Ids = ["not-a-resource-id"];

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal(
            "Invalid managed identity resource id 'not-a-resource-id'. Expected /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{resourceName}.",
            ex.Message);
    }
    // IdentityCliArgumentParserTests covers every tag and resource-restriction case.
    [Fact]
    public async Task ProcessAsync_InvalidTags_ThrowsInvalidInput()
    {
        var processor = CreateValid();
        processor.TagsInput = ["badtag-without-equals"];

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal("Invalid dictionary entry 'badtag-without-equals'. Expected KEY=VALUE format.", ex.Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidResourceRestrictionJson_ThrowsJsonException()
    {
        var processor = CreateValid();
        processor.ResourceRestriction = ["{bad-json"];

        await Assert.ThrowsAnyAsync<JsonException>(processor.ProcessAsync);
    }

    [Fact]
    public async Task ProcessAsync_InvalidForceString_ThrowsInvalidInput()
    {
        // --force-string is only parsed after the current identity is fetched, so the GET is allowed.
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>(
            GetOnlyPipeline("""{ "name": "id1", "location": "eastus", "tags": { "env": "dev" }, "properties": { "isolationScope": "None" } }"""));
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Set = ["properties.isolationScope=Regional"];
        processor.ForceString = "maybe";

        var ex = await Assert.ThrowsAsync<InvalidInputException>(processor.ProcessAsync);

        Assert.Equal("Invalid --force-string value 'maybe'.", ex.Message);
    }

    private static UpdateIdentityProcessor_Orchestrator CreateValid()
    {
        var processor = CreateProcessor<UpdateIdentityProcessor_Orchestrator>();
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        return processor;
    }
}