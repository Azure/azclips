// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;
/// <summary>
/// Tests the customization layer <see cref="ListGroupMemberProcessor_Orchestrator"/> adds on top of
/// the atomic <c>ListGroupMemberProcessor</c>: display-name-prefix and exact-display-name
/// resolution to a single group object id, and required-field validation. Driven through
/// <c>ProcessAsync</c> (the real entry point) so the resolution and member requests are observed.
/// </summary>
public class ListGroupMemberProcessor_OrchestratorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";
    private const string OtherGroupId = "66666666-7777-8888-9999-aaaaaaaaaaaa";
    private const string GroupsUri = "https://graph.microsoft.com/v1.0/groups";
    private const string MembersUri = $"https://graph.microsoft.com/v1.0/groups/{GroupId}/members";
    private const string EmptyList = "{\"value\":[]}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // InvalidInputException also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);
    private readonly IEventPublisher _mockPublisher = Mock.Of<IEventPublisher>();

    [Fact]
    public async Task ProcessAsync_GroupIsObjectId_ListsMembersOnly()
    {
        _mockPipeline.RespondTo("/members", EmptyList);
        var orchestrator = CreateOrchestrator(group: GroupId);

        await orchestrator.ProcessAsync();

        Assert.Equal([$"GET {MembersUri}"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_GroupIsDisplayNamePrefix_LooksUpGroupThenListsMembers()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo("/members", EmptyList);
        var orchestrator = CreateOrchestrator(group: "Sales");

        await orchestrator.ProcessAsync();

        Assert.Equal([$"GET {GroupsUri}", $"GET {MembersUri}"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task ProcessAsync_GroupIsDisplayNamePrefix_LooksUpGroupWithStartsWithFilter()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo("/members", EmptyList);
        var orchestrator = CreateOrchestrator(group: "Sales");

        await orchestrator.ProcessAsync();

        Assert.Equal("startswith(displayName,'Sales')", _mockPipeline.QueryValue(0, "$filter"));
    }

    [Fact]
    public async Task ProcessAsync_GroupDisplayName_LooksUpGroupWithEqualsFilter()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo("/members", EmptyList);
        var orchestrator = CreateOrchestrator(groupDisplayName: "Sales");

        await orchestrator.ProcessAsync();

        Assert.Equal("displayName eq 'Sales'", _mockPipeline.QueryValue(0, "$filter"));
    }

    [Fact]
    public async Task ProcessAsync_GroupDisplayNameContainsSingleQuote_EscapesQuoteInFilter()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("O'Brien"));
        _mockPipeline.RespondTo("/members", EmptyList);
        var orchestrator = CreateOrchestrator(groupDisplayName: "O'Brien");

        await orchestrator.ProcessAsync();

        Assert.Equal("displayName eq 'O''Brien'", _mockPipeline.QueryValue(0, "$filter"));
    }

    [Fact]
    public async Task ProcessAsync_GroupDisplayNameNotFound_ThrowsInvalidInput()
    {
        _mockPipeline.RespondTo("/v1.0/groups", EmptyList);
        var orchestrator = CreateOrchestrator(groupDisplayName: "Ghost");

        Exception? exception = await Record.ExceptionAsync(orchestrator.ProcessAsync);

        Assert.Equal("Group 'Ghost' is not found in Graph.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_GroupMatchesMany_ThrowsInvalidInput()
    {
        _mockPipeline.RespondTo("/v1.0/groups", $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales-North\"}},{{\"id\":\"{OtherGroupId}\",\"displayName\":\"Sales-South\"}}]}}");
        var orchestrator = CreateOrchestrator(group: "Sales");

        Exception? exception = await Record.ExceptionAsync(orchestrator.ProcessAsync);

        Assert.Equal("More than one group matched 'Sales'. Use the object id instead.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_MissingGroupAndDisplayName_ThrowsInvalidInput()
    {
        var orchestrator = CreateOrchestrator();

        Exception? exception = await Record.ExceptionAsync(orchestrator.ProcessAsync);

        Assert.Equal("A group identifier is required. Provide Group or GroupDisplayName.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_GroupAndDisplayName_ThrowsInvalidInput()
    {
        var orchestrator = CreateOrchestrator(group: "Marketing", groupDisplayName: "Sales");

        Exception? exception = await Record.ExceptionAsync(orchestrator.ProcessAsync);

        Assert.Equal("Provide either Group or GroupDisplayName, but not both.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task ProcessAsync_ValidationFails_PublishesProcessAsyncEnd()
    {
        // Resolution runs inside the base ProcessAsync start/end lifecycle, so a failure must still
        // emit the paired end event rather than escaping the processor span.
        var publishedEvents = new List<string>();
        var mockPublisher = new Mock<IEventPublisher>();
        mockPublisher.Setup(p => p.Publish(It.IsAny<string>()))
            .Callback<string>(publishedEvents.Add)
            .Returns(Task.CompletedTask);
        var orchestrator = new ListGroupMemberProcessor_Orchestrator();
        orchestrator.WithCapturingContext(_mockPipeline.Object, mockPublisher.Object);

        await Record.ExceptionAsync(orchestrator.ProcessAsync);

        Assert.Contains(Events.ProcessorProcessAsyncEnd, publishedEvents);
    }

    private ListGroupMemberProcessor_Orchestrator CreateOrchestrator(string? group = null, string? groupDisplayName = null)
    {
        var orchestrator = new ListGroupMemberProcessor_Orchestrator { Group = group!, GroupDisplayName = groupDisplayName! };
        orchestrator.WithCapturingContext(_mockPipeline.Object, _mockPublisher);
        return orchestrator;
    }

    private string GroupList(string displayName) =>
        $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"{displayName}\"}}]}}";}
