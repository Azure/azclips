// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;
/// <summary>
/// Tests the customization layer <see cref="CheckGroupMemberProcessor_Orchestrator"/> adds on top
/// of the atomic <c>CheckGroupMemberProcessor</c>: display-name-prefix resolution to a single
/// object id, and required-field validation.
/// </summary>
public class CheckGroupMemberProcessor_OrchestratorTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";
    private const string OtherGroupId = "66666666-7777-8888-9999-aaaaaaaaaaaa";
    private const string MemberId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string GroupsUri = "https://graph.microsoft.com/v1.0/groups";
    private const string MembershipCheckUri = $"https://graph.microsoft.com/v1.0/directoryObjects/{MemberId}/checkMemberGroups";
    private const string MembershipCheckBody = $"{{\"value\":[\"{GroupId}\"]}}";

    // Strict: a request the test did not set up throws MockException, so a test that gets its expected
    // InvalidInputException also proves no unexpected request was sent.
    private readonly Mock<IRuntimeHttpPipeline> _mockPipeline = new(MockBehavior.Strict);
    private readonly IEventPublisher _mockPublisher = Mock.Of<IEventPublisher>();

    [Fact]
    public async Task SendAsync_GroupIsObjectId_SendsOnlyMembershipCheck()
    {
        _mockPipeline.RespondTo("/checkMemberGroups", MembershipCheckBody);
        var orchestrator = CreateOrchestrator(group: GroupId, memberId: MemberId);

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal([$"POST {MembershipCheckUri}"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task SendAsync_GroupIsDisplayNamePrefix_LooksUpGroupThenSendsMembershipCheck()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo("/checkMemberGroups", MembershipCheckBody);
        var orchestrator = CreateOrchestrator(group: "Sales", memberId: MemberId);

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal([$"GET {GroupsUri}", $"POST {MembershipCheckUri}"], _mockPipeline.RequestLines());
    }

    [Fact]
    public async Task SendAsync_GroupIsDisplayNamePrefix_LooksUpGroupWithStartsWithFilter()
    {
        _mockPipeline.RespondTo("/v1.0/groups", GroupList("Sales"));
        _mockPipeline.RespondTo("/checkMemberGroups", MembershipCheckBody);
        var orchestrator = CreateOrchestrator(group: "Sales", memberId: MemberId);

        using HttpResponseMessage response = await orchestrator.InvokeSendAsync();

        Assert.Equal("startswith(displayName,'Sales')", _mockPipeline.QueryValue(0, "$filter"));
    }

    [Fact]
    public async Task SendAsync_MissingGroup_ThrowsInvalidInput()
    {
        var orchestrator = CreateOrchestrator(group: "", memberId: MemberId);

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("Group is required.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task SendAsync_MissingMemberId_ThrowsInvalidInput()
    {
        var orchestrator = CreateOrchestrator(group: GroupId, memberId: "");

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("MemberId is required to check group membership.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task SendAsync_GroupNotFound_ThrowsInvalidInput()
    {
        _mockPipeline.RespondTo("/v1.0/groups", "{\"value\":[]}");
        var orchestrator = CreateOrchestrator(group: "Nope", memberId: MemberId);

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("Group 'Nope' is not found in Graph.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    [Fact]
    public async Task SendAsync_GroupMatchesMany_ThrowsInvalidInput()
    {
        _mockPipeline.RespondTo("/v1.0/groups", $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"Sales-North\"}},{{\"id\":\"{OtherGroupId}\",\"displayName\":\"Sales-South\"}}]}}");
        var orchestrator = CreateOrchestrator(group: "Sales", memberId: MemberId);

        Exception? exception = await Record.ExceptionAsync(orchestrator.InvokeSendAsync);

        Assert.Equal("More than one group matched 'Sales'. Use the object id instead.", Assert.IsType<InvalidInputException>(exception).Message);
    }

    private CheckGroupMemberProcessor_Orchestrator CreateOrchestrator(string group, string memberId)
    {
        var orchestrator = new CheckGroupMemberProcessor_Orchestrator { Group = group, MemberId = memberId };
        orchestrator.WithFakeContext(_mockPipeline.Object, _mockPublisher);
        return orchestrator;
    }

    private string GroupList(string displayName) =>
        $"{{\"value\":[{{\"id\":\"{GroupId}\",\"displayName\":\"{displayName}\"}}]}}";}
