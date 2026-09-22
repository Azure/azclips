// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Models;
using Microsoft.Azure.Azclips.Extensions.MsGraph.Processors;
using Moq;

namespace Extensions.MsGraph.Tests;

/// <summary>
/// Regression coverage for the list orchestrators' output shape. Before the fix, the orchestrators
/// delegated to the atomic via raw <c>SendAsync</c>, so <c>ProcessAsync</c> wrote the raw OData
/// envelope (a single <c>*List</c> object with a <c>Value</c> property) and never followed
/// <c>@odata.nextLink</c>. These tests drive the real <c>ProcessAsync</c> path and assert that the
/// output is the individual items (not the envelope) and that every page is returned.
/// </summary>
public class ListOrchestratorOutputShapeTests
{
    private const string GroupId = "11111111-2222-3333-4444-555555555555";

    private static IEventPublisher NoOpPublisher() => Mock.Of<IEventPublisher>();

    /// <summary>Responds with <paramref name="page2"/> once the request carries a skiptoken, else <paramref name="page1"/>.</summary>
    private static FakeGraphHttpPipeline TwoPagePipeline(string page1, string page2) =>
        new(req => req.RequestUri!.OriginalString.Contains("skiptoken", StringComparison.Ordinal)
            ? FakeGraphHttpPipeline.JsonResponse(page2)
            : FakeGraphHttpPipeline.JsonResponse(page1));

    // -------- Application --------

    [Fact]
    public async Task ListApplication_ShouldWriteIndividualApplications_NotEnvelope()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse(
            "{\"value\":[{\"id\":\"1\",\"displayName\":\"A\"},{\"id\":\"2\",\"displayName\":\"B\"}]}"));
        var orch = new ListApplicationProcessor_Orchestrator();
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        var written = Assert.Single(output);
        Assert.IsNotType<ApplicationList>(written);
        var apps = Assert.IsAssignableFrom<IEnumerable<Application>>(written).ToList();
        Assert.Equal(new[] { "A", "B" }, apps.Select(a => a.DisplayName));
    }

    [Fact]
    public async Task ListApplication_ShouldFollowNextLinkAcrossPages()
    {
        var pipeline = TwoPagePipeline(
            "{\"value\":[{\"id\":\"1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/applications?$skiptoken=p2\"}",
            "{\"value\":[{\"id\":\"2\"}]}");
        var orch = new ListApplicationProcessor_Orchestrator();
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        var apps = Assert.IsAssignableFrom<IEnumerable<Application>>(Assert.Single(output)).ToList();
        Assert.Equal(new[] { "1", "2" }, apps.Select(a => a.Id));
    }

    // -------- User --------

    [Fact]
    public async Task ListUser_ShouldWriteIndividualUsers_NotEnvelope()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse(
            "{\"value\":[{\"id\":\"1\",\"displayName\":\"Ada\"},{\"id\":\"2\",\"displayName\":\"Grace\"}]}"));
        var orch = new ListUserProcessor_Orchestrator();
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        var written = Assert.Single(output);
        Assert.IsNotType<UserList>(written);
        var users = Assert.IsAssignableFrom<IEnumerable<User>>(written).ToList();
        Assert.Equal(new[] { "Ada", "Grace" }, users.Select(u => u.DisplayName));
    }

    [Fact]
    public async Task ListUser_ShouldFollowNextLinkAcrossPages()
    {
        var pipeline = TwoPagePipeline(
            "{\"value\":[{\"id\":\"1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users?$skiptoken=p2\"}",
            "{\"value\":[{\"id\":\"2\"}]}");
        var orch = new ListUserProcessor_Orchestrator();
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        var users = Assert.IsAssignableFrom<IEnumerable<User>>(Assert.Single(output)).ToList();
        Assert.Equal(new[] { "1", "2" }, users.Select(u => u.Id));
    }

    // -------- Group --------

    [Fact]
    public async Task ListGroup_ShouldWriteIndividualGroups_NotEnvelope()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse(
            "{\"value\":[{\"id\":\"1\",\"displayName\":\"Sales\"},{\"id\":\"2\",\"displayName\":\"Eng\"}]}"));
        var orch = new ListGroupProcessor_Orchestrator();
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        var written = Assert.Single(output);
        Assert.IsNotType<GroupList>(written);
        var groups = Assert.IsAssignableFrom<IEnumerable<Group>>(written).ToList();
        Assert.Equal(new[] { "Sales", "Eng" }, groups.Select(g => g.DisplayName));
    }

    [Fact]
    public async Task ListGroup_ShouldFollowNextLinkAcrossPages()
    {
        var pipeline = TwoPagePipeline(
            "{\"value\":[{\"id\":\"1\"}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/groups?$skiptoken=p2\"}",
            "{\"value\":[{\"id\":\"2\"}]}");
        var orch = new ListGroupProcessor_Orchestrator();
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        var groups = Assert.IsAssignableFrom<IEnumerable<Group>>(Assert.Single(output)).ToList();
        Assert.Equal(new[] { "1", "2" }, groups.Select(g => g.Id));
    }

    // -------- Group members --------

    [Fact]
    public async Task ListGroupMember_ShouldWriteIndividualMembers_NotEnvelope()
    {
        var pipeline = new FakeGraphHttpPipeline(_ => FakeGraphHttpPipeline.JsonResponse(
            "{\"value\":[{\"id\":\"1\",\"displayName\":\"Ada\"},{\"id\":\"2\",\"displayName\":\"Grace\"}]}"));
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = GroupId };
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        var written = Assert.Single(output);
        Assert.IsNotType<DirectoryObjectList>(written);
        var members = Assert.IsAssignableFrom<IEnumerable<DirectoryObject>>(written).ToList();
        Assert.Equal(new[] { "Ada", "Grace" }, members.Select(m => m.DisplayName));
    }

    [Fact]
    public async Task ListGroupMember_ShouldFollowNextLinkAcrossPages()
    {
        var pipeline = TwoPagePipeline(
            $"{{\"value\":[{{\"id\":\"1\"}}],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/groups/{GroupId}/members?$skiptoken=p2\"}}",
            "{\"value\":[{\"id\":\"2\"}]}");
        var orch = new ListGroupMemberProcessor_Orchestrator { Group = GroupId };
        var output = orch.WithCapturingContext(pipeline, NoOpPublisher());

        await orch.ProcessAsync();

        Assert.Equal(2, pipeline.Sent.Count);
        var members = Assert.IsAssignableFrom<IEnumerable<DirectoryObject>>(Assert.Single(output)).ToList();
        Assert.Equal(new[] { "1", "2" }, members.Select(m => m.Id));
    }
}
