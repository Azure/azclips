// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;
/// <summary>
/// Tests the customization layer <see cref="GetGroupProcessor_Orchestrator"/> adds on top of the
/// atomic <c>GetGroupProcessor</c>: display-name-prefix resolution to a single object id, the
/// <c>AppendSelected</c> defaults merging, and deterministic errors when zero/many groups match.
/// </summary>
public class GetGroupProcessor_OrchestratorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";
    private const string OtherGroupId = "66666666-7777-8888-9999-aaaaaaaaaaaa";
    private const string GroupsUri = "https://graph.microsoft.com/v1.0/groups";
    private const string GroupUri = $"https://graph.microsoft.com/v1.0/groups/{GroupId}";
    private const string GroupBody = $"{{\"id\":\"{GroupId}\"}}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // InvalidInputException also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);
    private readonly IEventPublisher _mockPublisher = Mock.Of<IEventPublisher>();

    [Fact]
    public async Task SendAsync_GroupIsObjectId_SendsOnlyGroupGet()
    {
        _mockPipeline.RespondTo($"/groups/{GroupId}", GroupBody);
        var orchestrator = CreateOrchestrator(group: GroupId);

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal([$"GET {GroupUri}"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task SendAsync_GroupIsDisplayNamePrefix_LooksUpGroupThenGetsGroup()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo($"/groups/{GroupId}", GroupBody);
        var orchestrator = CreateOrchestrator(group: "Sales");

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal([$"GET {GroupsUri}", $"GET {GroupUri}"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task SendAsync_GroupIsDisplayNamePrefix_LooksUpGroupWithStartsWithFilter()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo($"/groups/{GroupId}", GroupBody);
        var orchestrator = CreateOrchestrator(group: "Sales");

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal("startswith(displayName,'Sales')", _mockPipeline.QueryValue(0, "$filter"));
    }

    [Fact]
    public async Task SendAsync_DisplayNameContainsSingleQuote_EscapesQuoteInFilter()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("O'Brien"));
        _mockPipeline.RespondTo($"/groups/{GroupId}", GroupBody);
        var orchestrator = CreateOrchestrator(group: "O'Brien");

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal("startswith(displayName,'O''Brien')", _mockPipeline.QueryValue(0, "$filter"));
    }

    [Fact]
    public async Task SendAsync_AppendSelected_SelectsRequestedAndDefaultProperties()
    {
        _mockPipeline.RespondTo($"/groups/{GroupId}", GroupBody);
        var orchestrator = CreateOrchestrator(group: GroupId, select: ["mailNickname"], appendSelected: true);

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Superset(new HashSet<string> { "mailNickname", "displayName" }, new HashSet<string>(_mockPipeline.QueryValue(0, "$select")!.Split(',')));
    }

    [Fact]
    public async Task SendAsync_MissingGroup_ThrowsInvalidInput()
    {
        var orchestrator = CreateOrchestrator(group: "");

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("Group is required.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task SendAsync_GroupNotFound_ThrowsInvalidInput()
    {
        _mockPipeline.RespondTo("/v1.0/groups", "{\"value\":[]}");
        var orchestrator = CreateOrchestrator(group: "Ghost");

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("Group 'Ghost' is not found in Graph.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task SendAsync_GroupMatchesMany_ThrowsInvalidInput()
    {
        _mockPipeline.RespondTo("/v1.0/groups", $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales-North\"}},{{\"id\":\"{OtherGroupId}\",\"displayName\":\"Sales-South\"}}]}}");
        var orchestrator = CreateOrchestrator(group: "Sales");

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("More than one group matched 'Sales'. Use the object id instead.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    private GetGroupProcessor_Orchestrator CreateOrchestrator(string group, string[]? select = null, bool appendSelected = false)
    {
        var orchestrator = new GetGroupProcessor_Orchestrator { Group = group, Select = select, AppendSelected = appendSelected };
        orchestrator.WithFakeContext(_mockPipeline.Object, _mockPublisher);
        return orchestrator;
    }

    private string GroupList(string displayName) =>
        $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"{displayName}\"}}]}}";}
