// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// IdentityCliArgumentParserTests covers every tag and resource-restriction case; these prove the orchestrator
// parses both before any request.
public class CreateIdentityProcessor_OrchestratorTests
{
    private static CreateIdentityProcessor_Orchestrator CreateValid()
    {
        var processor = CreateProcessor<CreateIdentityProcessor_Orchestrator>();
        processor.SubscriptionId = TestSubscriptionId;
        processor.ResourceGroup = "rg1";
        processor.ResourceName = "id1";
        processor.Location = "eastus";
        return processor;
    }

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
}