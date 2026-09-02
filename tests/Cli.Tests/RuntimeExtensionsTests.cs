// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using Core.Abstractions.Runtime;
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
    public async Task GetLog_DoesNotReadDisabledRequestOrResponseBodies()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.test")
        {
            Content = new ThrowingContent(),
        };
        request.Options.Set(RuntimeHttpRequestOptions.DisableBodyLogging, true);
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new ThrowingContent(),
        };

        string requestLog = await request.GetLog();
        string responseLog = await response.GetLog();

        Assert.Contains("HTTP REQUEST", requestLog);
        Assert.Contains("HTTP RESPONSE", responseLog);
    }

    [Fact]
    public async Task GetLog_HidesDisabledRequestQuery()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://account.blob.core.windows.net/container/blob?sig=secret");
        request.Options.Set(RuntimeHttpRequestOptions.DisableQueryLogging, true);

        string log = await request.GetLog();

        Assert.Contains("https://account.blob.core.windows.net/container/blob", log);
        Assert.DoesNotContain("sig=secret", log);
    }

    [Fact]
    public async Task EnsureSuccessAsync_HidesDisabledRequestQuery()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://account.blob.core.windows.net/container/blob?sig=secret");
        request.Options.Set(RuntimeHttpRequestOptions.DisableQueryLogging, true);
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            RequestMessage = request,
        };

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => response.EnsureSuccessAsync(new FakeEventPublisher()));

        Assert.Contains("https://account.blob.core.windows.net/container/blob", exception.Message);
        Assert.DoesNotContain("sig=secret", exception.Message);
    }

    private sealed class ThrowingContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new InvalidOperationException("Body logging attempted to read binary content.");

        protected override bool TryComputeLength(out long length)
        {
            length = 1;
            return true;
        }
    }
}
