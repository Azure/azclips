// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using JsonNode = Microsoft.Azure.Azclips.ExtensionRuntime.Json.JsonNode;

namespace Cli.Tests;

/// <summary>
/// Tests for <see cref="RuntimeExtensions"/> HTTP trace-logging helpers.
///
/// Regression coverage for the engine tracing path: <see cref="RuntimeExtensions.FormatString"/>
/// previously JSON-parsed every traced body unconditionally, so any non-JSON body
/// (text/plain, xml, html error pages, octet-stream, etc.) threw inside the tracing
/// subscriber and failed the whole command before the response could be surfaced.
/// Logging must tolerate ANY body and never throw.
/// </summary>
public class RuntimeExtensionsTests
{
    // ── FormatString: valid JSON keeps normalized/pretty behavior ──────────────

    [Fact]
    public void FormatString_NormalizesValidJson_UnchangedBehavior()
    {
        // Raw has extra whitespace so the normalized form is distinguishable from the input.
        const string raw = "{   \"a\"  :  1 , \"b\" : [ 1, 2, 3 ] }";
        string expected = JsonNode.Parse(raw)!.ToString();

        string result = RuntimeExtensions.FormatString(raw);

        Assert.Equal(expected, result);
        Assert.NotEqual(raw, result);
    }

    // ── FormatString: non-JSON falls back to raw and never throws ──────────────

    [Theory]
    [InlineData("this is not json")]
    [InlineData("<html><body><h1>502 Bad Gateway</h1></body></html>")]
    [InlineData("<?xml version=\"1.0\"?><error><code>BadGateway</code></error>")]
    [InlineData("plain text error: upstream timed out")]
    public void FormatString_ReturnsRaw_ForNonJsonBody(string body)
    {
        string result = RuntimeExtensions.FormatString(body);

        Assert.Equal(body, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void FormatString_IsNullAndEmptySafe(string? body)
    {
        string result = RuntimeExtensions.FormatString(body!);

        Assert.Equal(body ?? string.Empty, result);
    }

    // ── FormatString: large non-JSON body is truncated with the existing marker ─

    [Fact]
    public void FormatString_TruncatesLargeNonJsonBody()
    {
        const int streamCutOffSize = 10 * 1024; // 10KB - must match FormatString
        string body = new string('x', streamCutOffSize + 500);

        string result = RuntimeExtensions.FormatString(body);

        Assert.Contains("DATA TRUNCATED DUE TO SIZE", result);
        Assert.StartsWith(new string('x', streamCutOffSize), result);
        Assert.Equal(
            body.Substring(0, streamCutOffSize) + "\r\nDATA TRUNCATED DUE TO SIZE\r\n",
            result);
    }

    // ── GetLog(HttpResponseMessage): non-JSON body completes and is included ────

    [Theory]
    [InlineData("text/plain")]
    [InlineData("text/html")]
    public async Task GetLog_Response_DoesNotThrow_ForNonJsonBody(string mediaType)
    {
        const string body = "upstream returned an error page <not json>";
        using var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

        string log = await response.GetLog();

        Assert.Contains(body, log);
        Assert.Contains("HTTP RESPONSE", log);
    }

    [Fact]
    public async Task GetLog_Response_NormalizesJsonBody()
    {
        const string raw = "{ \"status\" : \"Succeeded\" }";
        string expectedBody = JsonNode.Parse(raw)!.ToString();
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(raw, Encoding.UTF8, "application/json"),
        };

        string log = await response.GetLog();

        Assert.Contains(expectedBody, log);
    }

    [Fact]
    public void Clone_RetryPreservesClientRequestId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resources");
        request.Headers.Add("x-ms-client-request-id", "request-id");

        using var clone = request.Clone();

        Assert.Equal("request-id", Assert.Single(clone.Headers.GetValues("x-ms-client-request-id")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Clone_NewRequestDropsClientRequestId(bool overrideUri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/resources");
        request.Headers.Add("x-ms-client-request-id", "request-id");

        using var clone = overrideUri
            ? request.Clone(new Uri("https://example.com/resources?page=2"))
            : request.Clone(method: HttpMethod.Post);

        Assert.False(clone.Headers.Contains("x-ms-client-request-id"));
    }
}
