// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Customization;
using Moq;
using static Extensions.ManagedServiceIdentity.Tests.IdentityProcessorTestHelpers;

namespace Extensions.ManagedServiceIdentity.Tests;

// ManagedIdentityResolverTests and IdentityCliArgumentParserTests cover every input case; these prove the
// orchestrator calls each check before any write.
public class UpdateIdentityProcessor_OrchestratorTests
{
    private const string ExistingIdentity =
        """{ "name": "id1", "location": "eastus", "tags": { "env": "dev" }, "properties": { "isolationScope": "None" } }""";

    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);

    [Fact]
    public async Task ProcessAsync_MissingNameAndResourceGroup_ThrowsInvalidInput()
    {
        var processor = Initialize(new UpdateIdentityProcessor_Orchestrator(), _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(MissingIdentityAddressMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_InvalidResourceId_ThrowsInvalidInput()
    {
        var processor = Initialize(new UpdateIdentityProcessor_Orchestrator { Ids = ["not-a-resource-id"] }, _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal(InvalidResourceIdMessage, Assert.IsType<InvalidInputException>(exception).Message);
    }

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

    // --force-string is only parsed after the current identity is fetched, so the GET is allowed but no write is.
    [Fact]
    public async Task ProcessAsync_InvalidForceString_ThrowsInvalidInput()
    {
        _mockPipeline.RespondToGet(ExistingIdentity);
        var processor = Initialize(
            new UpdateIdentityProcessor_Orchestrator
            {
                SubscriptionId = TestSubscriptionId,
                ResourceGroup = "rg1",
                ResourceName = "id1",
                Set = ["properties.isolationScope=Regional"],
                ForceString = "maybe",
            },
            _mockPipeline);

        Exception? exception = await Record.ExceptionAsync(processor.ProcessAsync);

        Assert.Equal("Invalid --force-string value 'maybe'.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    private UpdateIdentityProcessor_Orchestrator CreateOrchestrator(string[]? tagsInput, string[]? resourceRestriction) =>
        Initialize(
            new UpdateIdentityProcessor_Orchestrator
            {
                SubscriptionId = TestSubscriptionId,
                ResourceGroup = "rg1",
                ResourceName = "id1",
                TagsInput = tagsInput,
                ResourceRestriction = resourceRestriction,
            },
            _mockPipeline);
}
