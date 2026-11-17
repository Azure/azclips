using System.Net;
using System.Text;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Resources;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Common.Recording;
using Tests.Recording;

namespace Scenario.Tests;

[Collection("Scenario Tests")]
public class GroupReadScenarioTests
{
    private const string SubscriptionId = "00000000-0000-0000-0000-000000000000";
    private const string ExplicitSubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string ResourceGroupName = "scenario-group-read-rg";
    private const string Location = "westus2";

    [Fact]
    public void ShowsAndListsResourceGroup()
    {
        var recording = SessionRecordFiles.OpenSession(
            typeof(GroupReadScenarioTests), nameof(ShowsAndListsResourceGroup),
            sanitizers: RecordingSanitizers.Default.With([new ResourceGroupListSanitizer(ResourceGroupName)]));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true);
            string subscriptionId = host.LiveSubscriptionId();
            string groupName = TestRun.ScopedName(ResourceGroupName, recording.Mode);
            bool created = false;
            try
            {
                var output = CreateGroup(host, ClientType.CLI, subscriptionId, groupName);
                created = true;
                AssertGroup(Assert.IsType<ResourceGroup>(Assert.Single(output)), subscriptionId, groupName);
                AssertReadWorkflow(host, ClientType.CLI, subscriptionId, groupName);
                recording.Save();
            }
            finally
            {
                if (created)
                {
                    host.RunCli("group", "delete", "--subscription-id", subscriptionId, "--name", groupName);
                }
            }
            return;
        }

        foreach (var clientType in new[] { ClientType.CLI, ClientType.PowerShell })
        {
            using var host = new ScenarioHost(clientType, recording.CreateInterceptor());
            AssertGroup(Assert.IsType<ResourceGroup>(Assert.Single(
                CreateGroup(host, clientType, SubscriptionId, ResourceGroupName))), SubscriptionId, ResourceGroupName);
            AssertReadWorkflow(host, clientType, SubscriptionId, ResourceGroupName);
        }
    }

    [Theory]
    [InlineData(ClientType.CLI, false, false)]
    [InlineData(ClientType.CLI, false, true)]
    [InlineData(ClientType.CLI, true, false)]
    [InlineData(ClientType.CLI, true, true)]
    [InlineData(ClientType.PowerShell, false, false)]
    [InlineData(ClientType.PowerShell, false, true)]
    [InlineData(ClientType.PowerShell, true, false)]
    [InlineData(ClientType.PowerShell, true, true)]
    public void Read_BindsExplicitOrDefaultSubscription_AndEnumeratesResults(
        ClientType clientType, bool list, bool explicitSubscription)
    {
        string subscriptionId = explicitSubscription ? ExplicitSubscriptionId : SubscriptionId;
        string uri = GroupUri(subscriptionId, list);
        string nextLink = uri + "&$skiptoken=next-page";
        using var backend = new ReadBackend(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            if (!list)
            {
                Assert.Equal(uri, request.RequestUri!.AbsoluteUri);
                return Response(HttpStatusCode.OK, GroupBody(subscriptionId, ResourceGroupName));
            }

            return Response(HttpStatusCode.OK, request.RequestUri!.AbsoluteUri == nextLink
                ? $$"""{"value":[{{GroupBody(subscriptionId, "second-rg")}}]}"""
                : $$"""{"value":[{{GroupBody(subscriptionId, ResourceGroupName)}}],"nextLink":"{{nextLink}}"}""");
        });
        using var host = new ScenarioHost(clientType,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()), backendHandler: backend);

        var groups = ReadGroups(host, clientType, list,
            explicitSubscription ? subscriptionId : null, ResourceGroupName);

        Assert.Equal(list ? [ResourceGroupName, "second-rg"] : [ResourceGroupName], groups.Select(group => group.Name));
        Assert.All(groups, group => AssertGroup(group, subscriptionId, group.Name!));
        Assert.Equal(list ? [uri, nextLink] : [uri], backend.Requests);
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public void List_EmptyResponse_ProducesNoResourceGroups(ClientType clientType)
    {
        using var backend = new ReadBackend(_ => Response(HttpStatusCode.OK, """{"value":[]}"""));
        using var host = new ScenarioHost(clientType,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()), backendHandler: backend);

        Assert.Empty(ReadGroups(host, clientType, list: true, subscriptionId: null, ResourceGroupName));

        Assert.Equal(GroupUri(SubscriptionId, list: true), Assert.Single(backend.Requests));
    }

    [Theory]
    [InlineData(ClientType.CLI, false)]
    [InlineData(ClientType.CLI, true)]
    [InlineData(ClientType.PowerShell, false)]
    [InlineData(ClientType.PowerShell, true)]
    public void Read_NotFound_IsSurfacedToTheCaller(ClientType clientType, bool list)
    {
        string code = list ? "SubscriptionNotFound" : "ResourceGroupNotFound";
        using var backend = new ReadBackend(_ => Response(HttpStatusCode.NotFound,
            JsonSerializer.Serialize(new { error = new { code, message = "The requested resource does not exist." } })));
        using var host = new ScenarioHost(clientType,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()), backendHandler: backend);

        var error = Record.Exception(() => ReadGroups(host, clientType, list, SubscriptionId, ResourceGroupName));

        Assert.NotNull(error);
        Assert.Contains("404", error.ToString());
        Assert.Equal(GroupUri(SubscriptionId, list), Assert.Single(backend.Requests));
    }

    [Fact]
    public void PowerShell_InvalidParameter_IsRejectedBeforeSendingRequests()
    {
        using var backend = new ReadBackend(_ => throw new InvalidOperationException("No HTTP request was expected."));
        using var host = new ScenarioHost(ClientType.PowerShell,
            new RecordPlaybackInterceptor(RecorderMode.Record, new SessionRecord()), backendHandler: backend);

        var error = Record.Exception(() => host.RunPowerShellCmdlet<GetResourceGroupCmdlet>(
            new Dictionary<string, object> { ["NotAParameter"] = "value" }));

        Assert.NotNull(error);
        Assert.Contains("NotAParameter", error.ToString());
        Assert.Empty(backend.Requests);
    }

    private static void AssertReadWorkflow(ScenarioHost host, ClientType clientType, string subscriptionId, string groupName)
    {
        foreach (bool list in new[] { false, true })
        {
            foreach (bool explicitSubscription in new[] { true, false })
            {
                var groups = ReadGroups(host, clientType, list, explicitSubscription ? subscriptionId : null, groupName);
                var group = Assert.Single(groups, group => string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase));
                if (!list) Assert.Single(groups);
                AssertGroup(group, subscriptionId, groupName);
            }
        }
    }

    private static IReadOnlyList<ResourceGroup> ReadGroups(
        ScenarioHost host, ClientType clientType, bool list, string? subscriptionId, string groupName)
    {
        if (clientType == ClientType.CLI)
        {
            var arguments = new List<string> { "group", list ? "list" : "show" };
            if (!list) arguments.AddRange(["--name", groupName]);
            if (subscriptionId is not null) arguments.AddRange(["--subscription-id", subscriptionId]);
            var output = host.RunCli([.. arguments]);
            return list
                ? Assert.IsAssignableFrom<IEnumerable<ResourceGroup>>(Assert.Single(output)).ToArray()
                : [Assert.IsType<ResourceGroup>(Assert.Single(output))];
        }

        var parameters = new Dictionary<string, object>();
        if (!list) parameters["Name"] = groupName;
        if (subscriptionId is not null) parameters["SubscriptionId"] = subscriptionId;
        return host.RunPowerShellCmdlet<GetResourceGroupCmdlet>(parameters)
            .Select(output => Assert.IsType<ResourceGroup>(output)).ToArray();
    }

    private static IReadOnlyList<object> CreateGroup(ScenarioHost host, ClientType clientType, string subscriptionId, string groupName) =>
        clientType == ClientType.CLI
            ? host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", groupName, "--location", Location)
            : host.RunPowerShell("New-AzResourceGroup", new Dictionary<string, object>
            {
                ["SubscriptionId"] = subscriptionId,
                ["Name"] = groupName,
                ["Location"] = Location,
            });

    private static void AssertGroup(ResourceGroup group, string subscriptionId, string groupName)
    {
        Assert.Equal(groupName, group.Name);
        Assert.Equal(Location, group.Location);
        Assert.Equal("Microsoft.Resources/resourceGroups", group.Type);
        Assert.Equal("Succeeded", group.Properties?.ProvisioningState);
        Assert.Equal($"/subscriptions/{subscriptionId}/resourceGroups/{groupName}", group.Id, ignoreCase: true);
    }

    private static string GroupUri(string subscriptionId, bool list) =>
        $"https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups{(list ? "" : "/" + ResourceGroupName)}?api-version=2025-04-01";

    private static string GroupBody(string subscriptionId, string groupName) =>
        JsonSerializer.Serialize(new
        {
            id = $"/subscriptions/{subscriptionId}/resourceGroups/{groupName}",
            name = groupName,
            location = Location,
            type = "Microsoft.Resources/resourceGroups",
            properties = new { provisioningState = "Succeeded" },
        });

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ReadBackend(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            var response = respond(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}