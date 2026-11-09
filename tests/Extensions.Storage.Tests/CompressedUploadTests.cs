// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class CompressedUploadTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "azclips-compressed-" + Guid.NewGuid().ToString("N"));

    public CompressedUploadTests() => Directory.CreateDirectory(directory);

    public static IEnumerable<object[]> UploadCases()
    {
        foreach (string surface in new[] { "cli-blob", "powershell-blob", "cli-files" })
            foreach (bool type in new[] { false, true })
                foreach (bool encoding in new[] { false, true })
                    yield return [surface, type, encoding];
    }

    [Theory]
    [MemberData(nameof(UploadCases))]
    public async Task CompressedPayloadAndExplicitContentSettingsArePreserved(string surface, bool explicitType, bool explicitEncoding)
    {
        string source = Path.Combine(directory, "report.json.gz");
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes("""{"message":"compressed payload"}"""));
        byte[] payload = buffer.ToArray();
        await File.WriteAllBytesAsync(source, payload);
        bool files = surface == "cli-files";
        string? type = explicitType ? "application/json" : null;
        string? encoding = explicitEncoding ? "gzip" : null;
        string? expectedType = type ?? (explicitEncoding ? (files ? "application/octet-stream" : null) : "application/gzip");
        byte[]? uploaded = null;
        int creates = 0;
        var fixture = new Fixture(async request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                var response = BlobWorkflowTests.Head(length: payload.Length);
                if (expectedType is not null) response.Content.Headers.ContentType = new MediaTypeHeaderValue(expectedType);
                if (encoding is not null) response.Content.Headers.ContentEncoding.Add(encoding);
                return response;
            }
            Assert.Equal(HttpMethod.Put, request.Method);
            if (BlobWorkflowTests.Query(request)["comp"] == "range")
                uploaded = await request.Content!.ReadAsByteArrayAsync();
            else
            {
                creates++;
                string prefix = files ? "x-ms-" : "x-ms-blob-";
                Assert.Equal(expectedType, request.Headers.TryGetValues(prefix + "content-type", out var types) ? types.Single() : null);
                Assert.Equal(encoding, request.Headers.TryGetValues(prefix + "content-encoding", out var encodings) ? encodings.Single() : null);
                if (!files) uploaded = await request.Content!.ReadAsByteArrayAsync();
            }
            var created = new HttpResponseMessage(HttpStatusCode.Created) { Content = new ByteArrayContent([]) };
            created.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
            created.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
            return created;
        });
        StorageDataPlaneProcessorBase orchestrator = surface switch
        {
            "cli-blob" => new UploadStorageBlobProcessor_Orchestrator
            {
                Context = BlobWorkflowTests.Context(), Container = "container", File = source,
                ContentType = type, ContentEncoding = encoding
            },
            "powershell-blob" => new SetStorageBlobContentProcessor_Orchestrator
            {
                Context = BlobWorkflowTests.Context(), Container = "container", File = source, Force = true,
                ContentType = type, ContentEncoding = encoding
            },
            "cli-files" => new UploadStorageFileProcessor_Orchestrator
            {
                Context = new StorageContext("fixture", Fixture.DefaultProfile, enableFileBackupRequestIntent: true),
                ShareName = "share", Source = source, ContentType = type, ContentEncoding = encoding
            },
            _ => throw new ArgumentOutOfRangeException(nameof(surface))
        };
        orchestrator.Init(fixture.Context);
        await orchestrator.ProcessAsync();
        Assert.Equal(1, creates);
        Assert.Equal(payload, uploaded);
        Assert.Single(fixture.Output);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
