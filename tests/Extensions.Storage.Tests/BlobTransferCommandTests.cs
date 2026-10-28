// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobTransferCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "azclips-transfer-" + Guid.NewGuid().ToString("N"));

    public BlobTransferCommandTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("block", 3)]
    [InlineData("page", 512)]
    public async Task UploadDispatchesTypeAndDefaultsNameWithoutLeakingNativeContext(string type, int length)
    {
        string path = Path.Combine(root, "source.txt");
        await File.WriteAllBytesAsync(path, new byte[length]);
        int writes = 0;
        var fixture = new Fixture(request =>
        {
            Assert.Equal("/container/source.txt", request.RequestUri!.AbsolutePath);
            if (request.Method == HttpMethod.Head)
            {
                var head = BlobWorkflowTests.Head(length: length);
                head.Headers.Remove("x-ms-blob-type");
                head.Headers.Add("x-ms-blob-type", type == "page" ? "PageBlob" : "BlockBlob");
                return Task.FromResult(head);
            }
            writes++;
            if (BlobWorkflowTests.Query(request)["comp"] is null)
            {
                Assert.Equal("*", Assert.Single(request.Headers.IfNoneMatch).ToString());
                Assert.Equal("text/plain", Assert.Single(request.Headers.GetValues("x-ms-blob-content-type")));
                Assert.Equal("value", Assert.Single(request.Headers.GetValues("x-ms-meta-key")));
            }
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
            return Task.FromResult(response);
        });
        var command = new UploadStorageBlobCommand
        {
            StorageAccountName = "fixture", Container = "container", File = path,
            BlobType = type, MetadataInput = ["key=value"], NoProgress = true
        };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Equal(type == "page" ? 2 : 1, writes);
        var result = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
        Assert.Equal("\"etag\"", result.GetProperty("etag").GetString());
        Assert.Equal("2026-10-20T00:00:00Z", result.GetProperty("lastModified").GetString());
        Assert.False(result.TryGetProperty("context", out _));
        Assert.Empty(fixture.Progress);
    }

    [Fact]
    public async Task EmptyAppendPreservesTheCliNoOpShapeWithoutRequests()
    {
        string path = Path.Combine(root, "empty");
        await File.WriteAllBytesAsync(path, []);
        var fixture = new Fixture();
        var command = new UploadStorageBlobCommand { StorageAccountName = "fixture", Container = "container", File = path, BlobType = "append" };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Equal("{}", Assert.IsType<JsonElement>(Assert.Single(fixture.Output)).GetRawText());
        Assert.Equal(0, fixture.Sends);
    }

    [Theory]
    [InlineData("bad-type")]
    [InlineData("bad-md5")]
    [InlineData("duplicates")]
    [InlineData("page-chunk")]
    [InlineData("append-size-on-block")]
    public async Task InvalidUploadInputsFailBeforeAuthenticationOrRequests(string fault)
    {
        var fixture = new Fixture();
        var command = new UploadStorageBlobCommand
        {
            StorageAccountName = "fixture", Container = "container", File = Path.Combine(root, "missing")
        };
        switch (fault)
        {
            case "bad-type": command.BlobType = "invalid"; break;
            case "bad-md5": command.ContentMd5Input = "private-invalid"; break;
            case "duplicates": command.MetadataInput = ["Key=one", "key=two"]; break;
            case "page-chunk": command.BlobType = "page"; command.ChunkSize = 8 * 1024 * 1024; break;
            case "append-size-on-block": command.MaxSizeCondition = 1; break;
        }
        command.Init(fixture.Context);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.Empty(fixture.Output); Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownloadWritesExactBinaryOrFileAndProjectsOnlyFileMetadata(bool disk)
    {
        byte[] bytes = [0, 1, 255, 10, 13, 0xc3, 0xa9, 92, 34];
        var fixture = new Fixture(request =>
        {
            if (request.Method == HttpMethod.Head) return Task.FromResult(BlobWorkflowTests.Head(length: bytes.Length));
            var range = request.Headers.Range!.Ranges.Single();
            int start = (int)range.From!.Value, end = (int)range.To!.Value;
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes[start..(end + 1)]) };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, bytes.Length);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
            return Task.FromResult(response);
        });
        string path = Path.Combine(root, "output");
        if (disk) await File.WriteAllTextAsync(path, "old data that must be replaced");
        var command = new DownloadStorageBlobCommand
        {
            StorageAccountName = "fixture", Container = "container", Blob = "blob",
            File = disk ? path : null, ChunkSize = 3, NoProgress = true
        };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        if (disk)
        {
            Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
            var output = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
            Assert.Equal("blob", output.GetProperty("name").GetString());
            Assert.False(output.TryGetProperty("context", out _));
        }
        else
        {
            Assert.Equal(bytes, fixture.Output.SelectMany(value => Assert.IsType<BinaryOutputChunk>(value).Bytes.ToArray()));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        Assert.Empty(fixture.Progress);
    }

    [Theory]
    [InlineData("*.txt", "a/b.txt", true)]
    [InlineData("*", ".hidden", true)]
    [InlineData("*", "a\nb", true)]
    [InlineData("a?b", "a/b", true)]
    [InlineData("?", "\U0001f600", true)]
    [InlineData("[]]", "]", true)]
    [InlineData("[[]", "[", true)]
    [InlineData("[!a-c]", "d", true)]
    [InlineData("[!a-c]", "b", false)]
    [InlineData("[z-a]", "b", false)]
    [InlineData("[z-a-b]", "b", true)]
    [InlineData("[a--c]", "c", true)]
    [InlineData("[!]", "!", false)]
    [InlineData("[unfinished", "[unfinished", true)]
    [InlineData("a*b*c", "axybzzc", true)]
    [InlineData("a*b*c", "axybzzd", false)]
    [InlineData("[b-a!].txt", "a.txt", true)]
    [InlineData("[b-a!].txt", "b.txt", true)]
    [InlineData("[b-a!].txt", "!.txt", true)]
    [InlineData("[a--!a].txt", "a.txt", false)]
    [InlineData("[a--!a].txt", "b.txt", true)]
    [InlineData("[a--!a].txt", "!.txt", true)]
    [InlineData("*.txt", "a.txt", true)]
    [InlineData("*.txt", "b.txt", true)]
    [InlineData("*.txt", "!.txt", true)]
    [InlineData("[ab].txt", "a.txt", true)]
    [InlineData("[ab].txt", "b.txt", true)]
    [InlineData("[ab].txt", "!.txt", false)]
    public void BatchPatternMatchesIndependentPythonFnmatchVectors(string pattern, string name, bool expected) =>
        Assert.Equal(expected, StorageCliPattern.Compile(pattern)!(name));

    [Fact]
    public void PatternCaseAndSeparatorsFollowHostPlatformNormcase()
    {
        Assert.Equal(OperatingSystem.IsWindows(), StorageCliPattern.Compile("DIR/*.TXT")!("dir\\file.txt"));
        Assert.Null(StorageCliPattern.Compile(null));
    }

    [Theory]
    [InlineData("file.TXT", "text/plain")]
    [InlineData("file.json", "application/json")]
    [InlineData("file.unknown", "application/octet-stream")]
    public void MimeInferenceIsStableAndExplicit(string path, string expected) =>
        Assert.Equal(expected, StorageContentSettings.InferContentType(path));

    public void Dispose() => Directory.Delete(root, recursive: true);
}
