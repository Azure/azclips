// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Moq;
using static Cli.Tests.IdentityProcessorTestHelpers;

namespace Cli.Tests;

// IdentityCliArgumentParserTests covers every tag and resource-restriction case; these prove the orchestrator
// parses both before any request.
public class CreateIdentityProcessor_OrchestratorTests
{
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_InvalidTags_ThrowsInvalidInput()
    {
        var processor = CreateOrchestrator(tagsInput: ["badtag-without-equals"], resourceRestriction: null);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(
            "Invalid dictionary entry 'badtag-without-equals'. Expected KEY=VALUE format.",
            Assert.IsType<InvalidInputException>(exception).Message);
    }

    // Malformed JSON surfaces the framework JsonException (a derived type), not a custom message.
    [Fact]
    public async Task ProcessAsync_InvalidResourceRestrictionJson_ThrowsJsonException()
    {
        var processor = CreateOrchestrator(tagsInput: null, resourceRestriction: ["{bad-json"]);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.IsAssignableFrom<JsonException>(exception);
    }

    private CreateIdentityProcessor_Orchestrator CreateOrchestrator(string[]? tagsInput, string[]? resourceRestriction) =>
        Initialize(
            new CreateIdentityProcessor_Orchestrator
            {
                SubscriptionId = TestSubscriptionId,
                ResourceGroup = "rg1",
                ResourceName = "id1",
                Location = "eastus",
                TagsInput = tagsInput,
                ResourceRestriction = resourceRestriction,
            },
            _mockPipeline);
}
