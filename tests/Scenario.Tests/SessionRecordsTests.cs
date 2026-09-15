using Core.Abstractions;
using Tests.Recording;
using Tests.Common.Recording;
using Tests.Common.Utilities;

namespace Scenario.Tests;

/// <summary>
/// Acceptance tests for the SessionRecords on-disk format: lossless ordered round-trip with
/// metadata, the <c>SessionRecords/&lt;TestClass&gt;/&lt;TestName&gt;.json</c> layout, and the
/// pluggable request-matching strategy.
/// </summary>
public class SessionRecordsTests
{
    private static CapturedHttpRequest Outgoing(string method, string uri, string? body)
        => new(method, uri, new Dictionary<string, string[]>(), body);

    private static SessionRecordEntry Entry(string method, string uri, string? requestBody = null, int status = 200, string? responseBody = "{}")
        => new()
        {
            Request = new RecordedRequest
            {
                Method = method,
                Uri = uri,
                Headers = new Dictionary<string, string[]>
                {
                    ["Accept"] = ["application/json", "text/plain"],
                },
                Body = requestBody,
            },
            Response = new RecordedResponse
            {
                StatusCode = status,
                Headers = new Dictionary<string, string[]> { ["x-ms-request-id"] = ["aaaa-bbbb"] },
                ContentHeaders = new Dictionary<string, string[]> { ["Content-Type"] = ["application/json; charset=utf-8"] },
                Body = responseBody,
            },
        };

    [Fact]
    public void JsonSchema_RoundTripsLosslessly_PreservingOrderAndMetadata()
    {
        var record = new SessionRecord
        {
            Metadata =
            {
                ["scenario"] = "round-trip fixture",
                ["note"] = "unicode survives: π ≈ 3.14, ümlaut, 資源",
            },
            Entries =
            {
                Entry("PUT", "https://management.azure.com/one", requestBody: "{\"π\":\"3.14\"}", status: 201),
                Entry("GET", "https://management.azure.com/two", requestBody: null, status: 200),
                Entry("DELETE", "https://management.azure.com/one", requestBody: null, status: 202, responseBody: null),
            },
        };

        var directory = Path.Combine(Path.GetTempPath(), "azclips-sessionrecord-tests", Guid.NewGuid().ToString("N"));
        var first = Path.Combine(directory, "first.json");
        var second = Path.Combine(directory, "second.json");
        try
        {
            // Save -> Load -> Save again: byte-identical files prove nothing is lost or reordered
            // anywhere in the serialization cycle.
            record.Save(first);
            var loaded = SessionRecord.Load(first);
            loaded.Save(second);
            Assert.Equal(File.ReadAllText(first), File.ReadAllText(second));

            Assert.Equal(SessionRecord.CurrentVersion, loaded.Version);
            Assert.Equal(record.Metadata, loaded.Metadata);
            Assert.Equal(["PUT", "GET", "DELETE"], loaded.Entries.Select(e => e.Request.Method));
            Assert.Equal(record.Entries.Select(e => e.Request.Body), loaded.Entries.Select(e => e.Request.Body));
            Assert.Equal(record.Entries.Select(e => e.Response.StatusCode), loaded.Entries.Select(e => e.Response.StatusCode));
            Assert.Equal(["application/json", "text/plain"], loaded.Entries[0].Request.Headers["Accept"]);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Layout_FollowsSessionRecordsConvention()
    {
        var path = SessionRecordFiles.GetPath(@"C:\repo", typeof(SessionRecordsTests), "MyTest");

        Assert.Equal(
            Path.Combine(@"C:\repo", "tests", "Scenario.Tests", "SessionRecords", "SessionRecordsTests", "MyTest.json"),
            path);

        // The parameterless overload anchors on the repository root, so record mode writes into
        // the committable source tree, not the test's bin directory.
        Assert.Equal(
            SessionRecordFiles.GetPath(RepoLocator.GetRepoRoot(), typeof(SessionRecordsTests), "MyTest"),
            SessionRecordFiles.GetPath(typeof(SessionRecordsTests), "MyTest"));
    }

    [Fact]
    public void DefaultMatcher_MatchesOnMethodAndUri_IgnoringBodyAndCase()
    {
        var recorded = Entry("PUT", "https://management.azure.com/thing?api-version=1", requestBody: "{\"a\":1}").Request;
        var matcher = new DefaultRequestMatcher();

        Assert.True(matcher.Matches(recorded, Outgoing("put", "HTTPS://MANAGEMENT.AZURE.COM/thing?api-version=1", "{\"different\":true}")));
        Assert.False(matcher.Matches(recorded, Outgoing("PUT", "https://management.azure.com/thing?api-version=2", "{\"a\":1}")));
        Assert.False(matcher.Matches(recorded, Outgoing("GET", "https://management.azure.com/thing?api-version=1", "{\"a\":1}")));
    }

    [Fact]
    public void DefaultMatcher_WithBody_RequiresExactBody()
    {
        var recorded = Entry("PUT", "https://management.azure.com/thing", requestBody: "{\"a\":1}").Request;
        var matcher = new DefaultRequestMatcher(matchBody: true);

        Assert.True(matcher.Matches(recorded, Outgoing("PUT", "https://management.azure.com/thing", "{\"a\":1}")));
        Assert.False(matcher.Matches(recorded, Outgoing("PUT", "https://management.azure.com/thing", "{\"a\":2}")));
    }

    /// <summary>
    /// A custom matcher that ignores the query string, standing in for the normalization
    /// strategies real scenarios will need for non-deterministic request fields.
    /// </summary>
    private sealed class IgnoreQueryStringMatcher : IRequestMatcher
    {
        public string Description => "method + URI without query";

        public bool Matches(RecordedRequest recorded, CapturedHttpRequest outgoing)
            => string.Equals(recorded.Method, outgoing.Method, StringComparison.OrdinalIgnoreCase)
            && string.Equals(TrimQuery(recorded.Uri), TrimQuery(outgoing.Uri), StringComparison.OrdinalIgnoreCase);

        private static string TrimQuery(string uri)
        {
            var index = uri.IndexOf('?', StringComparison.Ordinal);
            return index < 0 ? uri : uri[..index];
        }
    }

    [Fact]
    public async Task Matcher_IsPluggable_OnTheInterceptor()
    {
        // The recording carries an older api-version than the client now sends; the default
        // matcher must refuse it, a query-insensitive matcher must serve it.
        var record = new SessionRecord
        {
            Entries = { Entry("PUT", "https://management.azure.com/thing?api-version=OLD", status: 201, responseBody: "{\"ok\":true}") },
        };

        var strict = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        var lenient = new RecordPlaybackInterceptor(RecorderMode.Playback, record, new IgnoreQueryStringMatcher());

        using var request = new HttpRequestMessage(HttpMethod.Put, "https://management.azure.com/thing?api-version=NEW");

        // Playback never touches the publisher or the next step, so nulls are safe here.
        var miss = await Assert.ThrowsAsync<InvalidOperationException>(() => strict.SendAsync(request, null!, null!));
        Assert.Contains("method + URI", miss.Message);

        using var served = await lenient.SendAsync(request, null!, null!);
        Assert.Equal(201, (int)served.StatusCode);
    }
}
