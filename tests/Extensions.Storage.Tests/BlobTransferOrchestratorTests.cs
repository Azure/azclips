// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobTransferOrchestratorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "azclips-transfer-" + Guid.NewGuid().ToString("N"));

    public BlobTransferOrchestratorTests() => Directory.CreateDirectory(root);

    [Theory]
    [InlineData("block", 3)]
    [InlineData("page", 512)]
    public async Task UploadDispatchesTypeAndDefaultsNameWithoutLeakingNativeContext(string type, int length)
    {
        string path = Path.Combine(root, "source.txt");
        await File.WriteAllBytesAsync(path, Enumerable.Repeat((byte)1, length).ToArray());
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
        var command = new UploadStorageBlobProcessor_Orchestrator
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
        Assert.Equal(JsonValueKind.Null, result.GetProperty("versionId").ValueKind);
        Assert.False(result.TryGetProperty("context", out _));
        Assert.Empty(fixture.Progress);
    }

    [Theory]
    [InlineData("block", 0, 2, 0, false)]
    [InlineData("block", 0, 2, 0, true)]
    [InlineData("block", 3, 8, 3, false)]
    [InlineData("block", 3, 8, 3, true)]
    [InlineData("block", 3, 2, 3, false)]
    [InlineData("block", 3, 2, 3, true)]
    [InlineData("page", 0, 512, 0, false)]
    [InlineData("page", 0, 512, 0, true)]
    [InlineData("page", 1024, 512, 0, false)]
    [InlineData("page", 1024, 512, 0, true)]
    [InlineData("page", 1024, 512, 512, false)]
    [InlineData("page", 1024, 512, 512, true)]
    [InlineData("page", 1024, 512, 1024, false)]
    [InlineData("page", 1024, 512, 1024, true)]
    [InlineData("append", 3, 2, 3, false)]
    [InlineData("append", 3, 2, 3, true)]
    [InlineData("append", 0, 2, 0, true)]
    public async Task UploadReturnsFinalWriteVersionWithoutUsingThePropertiesReadVersion(
        string type, int length, int chunkSize, int nonzeroBytes, bool powerShell)
    {
        string path = Path.Combine(root, "source.txt");
        byte[] bytes = new byte[length];
        bytes.AsSpan(0, nonzeroBytes).Fill(1);
        await File.WriteAllBytesAsync(path, bytes);
        string blobType = type switch { "block" => "BlockBlob", "page" => "PageBlob", _ => "AppendBlob" };
        string etag = "\"created\"";
        string? versionId = null;
        int writes = 0, appendBlocks = 0;
        long appendedLength = 0;
        var fixture = new Fixture(request =>
        {
            Assert.Equal("/container/source.txt", request.RequestUri!.AbsolutePath);
            Assert.Null(BlobWorkflowTests.Query(request)["versionid"]);
            if (request.Method == HttpMethod.Head)
            {
                Assert.Equal(etag, request.Headers.IfMatch.Single().ToString());
                var head = BlobWorkflowTests.Head(blobType, length);
                head.Headers.ETag = new EntityTagHeaderValue(etag);
                return Task.FromResult(head);
            }
            string? operation = BlobWorkflowTests.Query(request)["comp"];
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            if (operation == "block")
            {
                response.Headers.ETag = new EntityTagHeaderValue("\"staged\"");
                response.Headers.Add("x-ms-version-id", "staged-version");
                return Task.FromResult(response);
            }
            etag = $"\"write-{++writes}\"";
            versionId = $"write-{writes}";
            response.Headers.ETag = new EntityTagHeaderValue(etag);
            response.Headers.Add("x-ms-version-id", versionId);
            if (operation == "appendblock")
            {
                response.Headers.Add("x-ms-blob-append-offset", appendedLength.ToString());
                response.Headers.Add("x-ms-blob-committed-block-count", (++appendBlocks).ToString());
                appendedLength += request.Content!.Headers.ContentLength!.Value;
            }
            return Task.FromResult(response);
        });
        IProcessor command = powerShell
            ? new SetStorageBlobContentProcessor_Orchestrator
            {
                Context = BlobWorkflowTests.Context(), Container = "container", File = path,
                BlobType = type switch { "block" => "Block", "page" => "Page", _ => "Append" },
                ChunkSize = chunkSize, Force = true
            }
            : new UploadStorageBlobProcessor_Orchestrator
            {
                StorageAccountName = "fixture", Container = "container", File = path,
                BlobType = type, ChunkSize = chunkSize, Overwrite = true
            };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.NotNull(versionId);
        object output = Assert.Single(fixture.Output);
        if (powerShell)
        {
            var blob = Assert.IsType<StorageBlob>(output);
            Assert.Equal(versionId, blob.VersionId);
            Assert.Equal(versionId, System.Web.HttpUtility.ParseQueryString(blob.Uri.Query)["versionid"]);
        }
        else
        {
            var json = Assert.IsType<JsonElement>(output);
            Assert.Equal(["contentMd5", "etag", "lastModified", "requestServerEncrypted", "versionId"],
                json.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
            Assert.Equal(versionId, json.GetProperty("versionId").GetString());
        }
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("blank")]
    [InlineData("multiple")]
    public async Task UploadRejectsMalformedWriteVersionMetadata(string fault)
    {
        string path = Path.Combine(root, "source.txt");
        await File.WriteAllBytesAsync(path, [1]);
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            var response = new HttpResponseMessage(HttpStatusCode.Created);
            response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
            string[] values = fault switch
            {
                "empty" => [""],
                "blank" => [" "],
                _ => ["one", "two"]
            };
            response.Headers.TryAddWithoutValidation("x-ms-version-id", values);
            return Task.FromResult(response);
        });
        var command = new UploadStorageBlobProcessor_Orchestrator
        {
            StorageAccountName = "fixture", Container = "container", File = path
        };
        command.Init(fixture.Context);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => command.ProcessAsync());
        Assert.Equal("Storage returned an invalid uploaded blob version ID.", error.Message);
        Assert.Empty(fixture.Output);
    }

    [Fact]
    public async Task EmptyAppendPreservesTheCliNoOpShapeWithoutRequests()
    {
        string path = Path.Combine(root, "empty");
        await File.WriteAllBytesAsync(path, []);
        var fixture = new Fixture();
        var command = new UploadStorageBlobProcessor_Orchestrator { StorageAccountName = "fixture", Container = "container", File = path, BlobType = "append" };
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
        var command = new UploadStorageBlobProcessor_Orchestrator
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
    [InlineData("[a-]", "a", true)]
    [InlineData("[a-]", "b", false)]
    [InlineData("[a-]", "-", true)]
    [InlineData("[a-]", "z", false)]
    [InlineData("[!a-]", "a", false)]
    [InlineData("[!a-]", "b", true)]
    [InlineData("[!a-]", "-", false)]
    [InlineData("[!a-]", "z", true)]
    [InlineData("[--]", "a", false)]
    [InlineData("[--]", "b", false)]
    [InlineData("[--]", "-", true)]
    [InlineData("[--]", "z", false)]
    [InlineData("[a-b-]", "a", true)]
    [InlineData("[a-b-]", "b", true)]
    [InlineData("[a-b-]", "-", true)]
    [InlineData("[a-b-]", "z", false)]
    [InlineData("[-a]", "a", true)]
    [InlineData("[-a]", "b", false)]
    [InlineData("[-a]", "-", true)]
    [InlineData("[-a]", "z", false)]
    [InlineData("[a-z]", "a", true)]
    [InlineData("[a-z]", "b", true)]
    [InlineData("[a-z]", "-", false)]
    [InlineData("[a-z]", "z", true)]
    [InlineData("[a--]", "a", false)]
    [InlineData("[a--]", "b", false)]
    [InlineData("[a--]", "-", false)]
    [InlineData("[a--]", "z", false)]
    [InlineData("*[a-]", "xa", true)]
    [InlineData("*[a-]", "x-", true)]
    [InlineData("*[a-]", "xb", false)]
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
    [InlineData("report.json.gz", "application/gzip")]
    [InlineData("report.json.GZ", "application/gzip")]
    [InlineData("report.json.bz2", "application/x-bzip2")]
    [InlineData("report.json.xz", "application/x-xz")]
    [InlineData("report.json.Z", "application/x-compress")]
    [InlineData("image.svgz", "application/gzip")]
    public void MimeInferenceIsStableAndExplicit(string path, string expected) =>
        Assert.Equal(expected, StorageContentSettings.InferContentType(path));

    public void Dispose() => Directory.Delete(root, recursive: true);
}
