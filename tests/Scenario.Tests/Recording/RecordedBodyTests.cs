// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Text;
using Core.Abstractions.Runtime;
using Tests.Recording;

namespace Scenario.Tests;

public sealed class RecordedBodyTests
{
    private sealed class RespondNext(Func<HttpContent> content) : ISendAsync
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, IEventPublisher callback) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content() });
    }

    [Theory]
    [InlineData("utf-16", true, true, "application/json")]
    [InlineData("utf-16", true, false, "application/json")]
    [InlineData("utf-16BE", true, true, "application/vnd.test+json")]
    [InlineData("utf-32", true, true, "application/json")]
    [InlineData("utf-8", true, true, "application/json")]
    [InlineData("iso-8859-1", false, true, "text/plain")]
    [InlineData("utf-16", true, true, "application/xml")]
    public async Task TextCharsetsAndBomsAreDecodedSanitizedAndReplayed(string charset, bool bom, bool declared, string mediaType)
    {
        bool xml = mediaType == "application/xml";
        string body = xml ? "<UserDelegationKey><Value>fixture-key</Value></UserDelegationKey>" :
            "{\"password\":\"fixture-secret\",\"keys\":[{\"value\":\"fixture-key\",\"permissions\":\"FULL\",\"keyName\":\"key1\"}],\"label\":\"caf\u00e9\"}";
        var encoding = Encoding.GetEncoding(charset);
        byte[] bytes = [.. bom ? encoding.GetPreamble() : [], .. encoding.GetBytes(body)];
        HttpContent Content()
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = declared ? charset : null };
            return content;
        }
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.invalid/text") { Content = Content() };
        using var response = await recorder.SendAsync(request, null!, new RespondNext(Content));
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        var entry = Assert.Single(record.Entries);
        Assert.Null(entry.Request.BodyBase64);
        Assert.Null(entry.Response.BodyBase64);
        foreach (string? text in new[] { entry.Request.Body, entry.Response.Body })
        {
            Assert.NotNull(text);
            Assert.DoesNotContain("fixture-secret", text);
            Assert.DoesNotContain("fixture-key", text);
            if (!xml) Assert.Contains("[redacted]", text);
            Assert.Contains(RecordingSanitizers.FakeStorageKey, text);
        }
        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true));
        using var replayRequest = new HttpRequestMessage(HttpMethod.Put, request.RequestUri) { Content = Content() };
        using var replayResponse = await playback.SendAsync(replayRequest, null!, null!);
        Assert.Equal(entry.Response.Body, await replayResponse.Content.ReadAsStringAsync());
        if (!xml)
        {
            using var json = System.Text.Json.JsonDocument.Parse(entry.Response.Body!);
            Assert.Equal("caf\u00e9", json.RootElement.GetProperty("label").GetString());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitBinaryBodiesRoundTripWithoutTextConversion(bool validUtf8)
    {
        byte[] bytes = validUtf8 ? Encoding.UTF8.GetBytes("plain byte payload") : [0xff, 0, 0x80, 1];
        HttpContent Content()
        {
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            return content;
        }
        var record = new SessionRecord();
        var recorder = new RecordPlaybackInterceptor(RecorderMode.Record, record);
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.invalid/binary") { Content = Content() };
        using var response = await recorder.SendAsync(request, null!, new RespondNext(Content));
        var entry = Assert.Single(record.Entries);
        Assert.Null(entry.Request.Body);
        Assert.Null(entry.Response.Body);
        Assert.Equal(bytes, Convert.FromBase64String(entry.Request.BodyBase64!));
        Assert.Equal(bytes, Convert.FromBase64String(entry.Response.BodyBase64!));
        var playback = new RecordPlaybackInterceptor(RecorderMode.Playback, record, new DefaultRequestMatcher(matchBody: true));
        using var replayRequest = new HttpRequestMessage(HttpMethod.Put, request.RequestUri) { Content = Content() };
        using var replayResponse = await playback.SendAsync(replayRequest, null!, null!);
        Assert.Equal(bytes, await replayResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task CompressedBytesAreNotDecodedUsingTheUnderlyingTextCharset()
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes("""{"message":"compressed data"}"""));
        byte[] bytes = buffer.ToArray();
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.invalid/compressed")
        {
            Content = new ByteArrayContent(bytes)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Content.Headers.ContentEncoding.Add("gzip");
        var snapshot = await CapturedHttpRequest.SnapshotAsync(request);
        Assert.Null(snapshot.Body);
        Assert.Equal(bytes, snapshot.BinaryBody);
    }

    [Fact]
    public async Task InvalidTextCharsetFailsExplicitly()
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "https://example.invalid/text")
        {
            Content = new ByteArrayContent([1, 2, 3])
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "invalid-charset" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => CapturedHttpRequest.SnapshotAsync(request));
    }
}
