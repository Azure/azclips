using System.Net;
using System.Text;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Tests.Recording;

namespace Scenario.Tests;

/// <summary>
/// Covers record-time sanitisation: a recording made against a live-looking subscription keeps no
/// live ids or tokens, the sanitiser list is extensible, and playback still matches once both the
/// recording and the outgoing request have been sanitised.
/// </summary>
[Collection("Scenario Tests")]
public class RecordingSanitizerTests
{
    // Deliberately not the all-zero placeholder: these stand in for real values.
    private const string LiveSubscriptionId = "3f2a91c4-7b6d-4e18-9a05-c1d83e77b204";
    private const string LiveTenantId = "8c41d0be-2f95-4a37-b6e1-59d7a0c42f18";
    private const string ResourceGroupName = "sanitiser-rg";
    private const string Location = "westeurope";

    private sealed class FakeArmBackend : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Mirrors ARM: the resource id in the body carries the subscription id too.
            var body =
                $"{{\"id\":\"/subscriptions/{LiveSubscriptionId}/resourceGroups/{ResourceGroupName}\"," +
                $"\"name\":\"{ResourceGroupName}\",\"type\":\"Microsoft.Resources/resourceGroups\"," +
                $"\"location\":\"{Location}\",\"properties\":{{\"provisioningState\":\"Succeeded\"}}}}";
            var response = new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            response.Headers.TryAddWithoutValidation("x-ms-tenant-authority", $"https://login.microsoftonline.com/{LiveTenantId}");
            return Task.FromResult(response);
        }
    }

    private sealed class UnreachableBackend : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException($"Playback must not reach the network, but sent: {request.Method} {request.RequestUri}");
    }

    private static SessionRecord RecordAgainstLiveIds(RecordingSanitizers? sanitizers = null)
    {
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record, matcher: null, sanitizers);
        using var host = new ScenarioHost(ClientType.CLI, recorder, new FakeArmBackend());
        host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", LiveSubscriptionId);
        return record;
    }

    [Fact]
    public void Recording_ContainsNoLiveIdsOrTokens()
    {
        var record = RecordAgainstLiveIds();

        // The whole serialized file is the thing that gets committed, so assert against it.
        var path = Path.Combine(Path.GetTempPath(), "azclips-sanitiser-tests", Guid.NewGuid().ToString("N"), "Recording.json");
        try
        {
            record.Save(path);
            var json = File.ReadAllText(path);

            Assert.DoesNotContain(LiveSubscriptionId, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(LiveTenantId, json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stub-access-token", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(RecordingSanitizers.PlaceholderGuid, json, StringComparison.Ordinal);

            var entry = Assert.Single(record.Entries);
            Assert.False(entry.Request.Headers.ContainsKey("Authorization"));
            Assert.Contains($"/subscriptions/{RecordingSanitizers.PlaceholderGuid}/", entry.Request.Uri, StringComparison.Ordinal);
            Assert.Contains($"/subscriptions/{RecordingSanitizers.PlaceholderGuid}/", entry.Response.Body!, StringComparison.Ordinal);
            Assert.Contains(RecordingSanitizers.PlaceholderGuid, entry.Response.Headers["x-ms-tenant-authority"].Single(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void Playback_MatchesAfterSanitisation()
    {
        // Recorded against a live subscription and sanitised to the placeholder; the replayed
        // request still carries the live id, so matching only works if it is normalised too.
        var record = RecordAgainstLiveIds();

        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(ClientType.CLI, playback, new UnreachableBackend());

        var outputs = host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", LiveSubscriptionId);

        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
        Assert.Equal(Location, resourceGroup.Location);
    }

    [Fact]
    public void Playback_MatchesWhenReplayedWithADifferentSubscription()
    {
        // A teammate replaying the committed recording under their own subscription still matches,
        // because both sides normalise to the placeholder.
        var record = RecordAgainstLiveIds();

        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        using var host = new ScenarioHost(ClientType.CLI, playback, new UnreachableBackend());

        var outputs = host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", "d94b7e60-1a2c-4f83-8e5b-6207ac9d1f33");

        Assert.IsType<ResourceGroup>(Assert.Single(outputs));
    }

    [Fact]
    public void SanitizerList_IsExtensible()
    {
        var custom = RecordingSanitizers.Default.With(
            [new RegexSanitizer("resource-group-name", ResourceGroupName, "[rg]")],
            ["x-ms-tenant-authority"]);

        var record = RecordAgainstLiveIds(custom);
        var entry = Assert.Single(record.Entries);

        Assert.DoesNotContain(ResourceGroupName, entry.Request.Uri, StringComparison.Ordinal);
        Assert.Contains("[rg]", entry.Request.Uri, StringComparison.Ordinal);
        Assert.False(entry.Response.Headers.ContainsKey("x-ms-tenant-authority"));

        // The defaults still apply alongside the additions.
        Assert.DoesNotContain(LiveSubscriptionId, entry.Request.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BodyMatching_Works_WhenTheBodyIsSanitised()
    {
        // With body matching on, the recorded body is sanitised and the outgoing one is normalised
        // the same way, so the two are identical at compare time even though neither equals what
        // went over the wire.
        var custom = RecordingSanitizers.Default.With(
            [new RegexSanitizer("location", Location, "[location]")]);

        var record = RecordAgainstLiveIds(custom);
        Assert.Contains("[location]", Assert.Single(record.Entries).Request.Body!, StringComparison.Ordinal);

        var playback = new RecordPlaybackInterceptor(
            RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true), custom);
        using var host = new ScenarioHost(ClientType.CLI, playback, new UnreachableBackend());

        var outputs = host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", LiveSubscriptionId);

        Assert.IsType<ResourceGroup>(Assert.Single(outputs));
    }

    [Fact]
    public void Playback_Works_WhenSanitisingChangesBodyLength()
    {
        // Sanitising can change the body's length, so the derived Content-Length is not recorded
        // at all; playback rebuilds it from the stored body.
        var custom = RecordingSanitizers.Default.With(
            [new RegexSanitizer("shrink-location", Location, "eu")]);

        var record = RecordAgainstLiveIds(custom);
        var entry = Assert.Single(record.Entries);
        Assert.False(entry.Response.ContentHeaders.ContainsKey("Content-Length"));

        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record, matcher: null, custom);
        using var host = new ScenarioHost(ClientType.CLI, playback, new UnreachableBackend());

        var outputs = host.RunCli("group", "create",
            "--name", ResourceGroupName,
            "--location", Location,
            "--subscription-id", LiveSubscriptionId);

        var resourceGroup = Assert.IsType<ResourceGroup>(Assert.Single(outputs));
        Assert.Equal(ResourceGroupName, resourceGroup.Name);
    }

    [Theory]
    [InlineData("https://management.azure.com/subscriptions/3f2a91c4-7b6d-4e18-9a05-c1d83e77b204/rg", "/subscriptions/00000000-0000-0000-0000-000000000000/rg")]
    [InlineData("https://login.microsoftonline.com/8c41d0be-2f95-4a37-b6e1-59d7a0c42f18/oauth2", "login.microsoftonline.com/00000000-0000-0000-0000-000000000000/oauth2")]
    [InlineData("Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.payload", "Bearer [redacted]")]
    [InlineData("{\"key\":\"sk-proj-A1b2C3d4E5f6G7h8I9j0K1l2\"}", "[redacted-openai-key]")]
    [InlineData("https://contoso-aoai.openai.azure.com/openai/deployments/gpt", "https://[resource].openai.azure.com/openai/deployments/gpt")]
    public void DefaultSanitizers_ReplaceKnownSecretShapes(string input, string expectedFragment)
    {
        Assert.Contains(expectedFragment, RecordingSanitizers.Default.Apply(input), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("operationresults")]
    [InlineData("operationstatuses")]
    public void DefaultSanitizers_ReplaceArmOperationTokensAndSignatures(string operationPath)
    {
        string input =
            $"https://management.azure.com/subscriptions/sub/{operationPath}/eyJvcGVyYXRpb24iOiJkZWxldGUifQ" +
            "?api-version=2025-04-01&t=123&c=certificate&s=signature&h=hash";

        Assert.Equal(
            $"https://management.azure.com/subscriptions/sub/{operationPath}/sanitized-operation-token" +
            "?api-version=2025-04-01&t=sanitized&c=sanitized&s=sanitized&h=hash",
            RecordingSanitizers.Default.Apply(input));
    }

    [Fact]
    public void DefaultSanitizers_DropTheAzureOpenAiKeyHeader()
    {
        var request = RecordingSanitizers.Default.Sanitize(new RecordedRequest
        {
            Method = "POST",
            Uri = "https://contoso-aoai.openai.azure.com/openai/deployments/gpt/chat/completions",
            Headers = new Dictionary<string, string[]>
            {
                ["api-key"] = ["8f3c1d9e4b7a2650fa1c8e93d47b6205"],
                ["Content-Type"] = ["application/json"],
            },
        });

        Assert.False(request.Headers.ContainsKey("api-key"));
        Assert.True(request.Headers.ContainsKey("Content-Type"));
        Assert.DoesNotContain("contoso-aoai", request.Uri, StringComparison.OrdinalIgnoreCase);
    }
}
