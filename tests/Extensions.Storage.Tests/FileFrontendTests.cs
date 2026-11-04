// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net;
using System.Text.Json;
using System.Xml.Linq;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class FileFrontendTests : IDisposable
{
    private readonly string folder = System.IO.Path.Combine(Environment.CurrentDirectory, "files-frontends-" + Guid.NewGuid().ToString("N"));

    public FileFrontendTests() => Directory.CreateDirectory(folder);
    public void Dispose() => Directory.Delete(folder, recursive: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CliListingsAreOneProjectedArrayAndFilterDirectoriesAfterScanning(bool directories)
    {
        var fixture = new Fixture(_ => Task.FromResult(Page(
            new XElement("File", new XElement("Name", "a"), new XElement("Properties", new XElement("Content-Length", "7"))),
            new XElement("Directory", new XElement("Name", "b"), new XElement("Properties")))));
        ListStorageFilesProcessor_Orchestrator command = directories ? new ListStorageDirectoriesProcessor_Orchestrator() : new ListStorageFilesProcessor_Orchestrator();
        command.Context = Context(); command.ShareName = "share"; command.ShowNextMarker = true;
        command.Init(fixture.Context);
        await command.ProcessAsync();
        var array = Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output));
        Assert.Equal(directories ? 2 : 3, array.Length);
        Assert.Equal(directories ? "b" : "a", array[0].GetProperty("name").GetString());
        Assert.Equal(directories, array[0].GetProperty("isDirectory").GetBoolean());
        Assert.False(array[0].TryGetProperty("context", out _));
        Assert.False(array[0].TryGetProperty("share", out _));
        Assert.Equal(JsonValueKind.Null, array[^1].GetProperty("nextMarker").ValueKind);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("sas")]
    public async Task CliNeverFallsBackToKeyAuthentication(string authMode)
    {
        var fixture = new Fixture();
        var command = new ListStorageFilesProcessor_Orchestrator { Context = Context(), ShareName = "share", AuthMode = authMode };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<NotSupportedException>(() => command.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Theory]
    [InlineData("https://fixture.file.core.windows.net/share")]
    [InlineData("http://fixture.file.core.windows.net/")]
    [InlineData("https://fixture.file.core.usgovcloudapi.net/")]
    [InlineData("https://different.file.core.windows.net/")]
    public async Task CliRejectsNonpublicResourceOrConflictingEndpointsBeforeAuthentication(string endpoint)
    {
        var fixture = new Fixture();
        var command = new ListStorageFilesProcessor_Orchestrator { Context = Context(), ShareName = "share", FileEndpoint = endpoint };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<ArgumentException>(() => command.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task CliRejectsDestinationSasBeforeAuthentication()
    {
        var fixture = new Fixture();
        var command = new ListStorageFilesProcessor_Orchestrator
        {
            Context = Context(), ShareName = "share",
            FileEndpoint = "https://fixture.file.core.windows.net/?sig=secret"
        };
        command.Init(fixture.Context);
        await Assert.ThrowsAsync<NotSupportedException>(() => command.ProcessAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task NamedCliEndpointUsesExplicitIntentAndDoesNotLeakNativeResources()
    {
        var fixture = new Fixture(_ => Task.FromResult(Page()));
        var command = new ListStorageFilesProcessor_Orchestrator
        {
            FileEndpoint = "https://fixture.file.core.windows.net/", ShareName = "share", EnableFileBackupRequestIntent = true
        };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Empty(Assert.IsType<JsonElement[]>(Assert.Single(fixture.Output)));
        Assert.Single(fixture.TokenRequests);
    }

    [Fact]
    public async Task ZeroByteCliUploadInfersContentTypeAndSuppressesOnlyProgress()
    {
        string source = System.IO.Path.Combine(folder, "example.txt");
        await File.WriteAllBytesAsync(source, []);
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/share/example.txt", request.RequestUri!.AbsolutePath);
            Assert.Equal("text/plain", request.Headers.GetValues("x-ms-content-type").Single());
            Assert.Equal("team", request.Headers.GetValues("x-ms-meta-owner").Single());
            return Task.FromResult(Properties(HttpStatusCode.Created));
        });
        var command = new UploadStorageFileProcessor_Orchestrator
        {
            Context = Context(), ShareName = "share", Source = source, NoProgress = true,
            MetadataInput = ["owner=team"]
        };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        var output = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
        Assert.Equal("\"etag\"", output.GetProperty("etag").GetString());
        Assert.Empty(fixture.Progress);
        Assert.Equal(1, fixture.Sends);
    }

    [Fact]
    public async Task ZeroByteCliDownloadUsesDirectoryFilenameAndVerifiedProjectedProperties()
    {
        var fixture = new Fixture(request =>
        {
            Assert.Equal(HttpMethod.Head, request.Method);
            return Task.FromResult(Properties(HttpStatusCode.OK));
        });
        var command = new DownloadStorageFileProcessor_Orchestrator
        {
            Context = Context(), ShareName = "share", Path = "remote/data.txt", Destination = folder, NoProgress = true
        };
        command.Init(fixture.Context);
        await command.ProcessAsync();
        Assert.Empty(await File.ReadAllBytesAsync(System.IO.Path.Combine(folder, "data.txt")));
        Assert.Equal(2, fixture.Sends);
        Assert.Empty(fixture.Progress);
        var output = Assert.IsType<JsonElement>(Assert.Single(fixture.Output));
        Assert.Equal("data.txt", output.GetProperty("name").GetString());
        Assert.Equal(0, output.GetProperty("properties").GetProperty("contentLength").GetInt64());
    }

    [Fact]
    public void NativePreflightRejectsMissingIntentAndFreezesDirectoryPaths()
    {
        var context = new StorageContext("fixture", Fixture.DefaultProfile);
        var preflight = new StorageFilePowerShellPreflight { Context = context, ShareName = "share", Path = "file" };
        Assert.Throws<ArgumentException>(() => preflight.GetDirectoryTarget());
        preflight.EnableFileBackupRequestIntent = true;
        var directory = preflight.GetDirectoryTarget();
        Assert.True(directory.Context.EnableFileBackupRequestIntent);
        Assert.False(context.EnableFileBackupRequestIntent);
        Assert.Equal("file", directory.Path);
    }

    [Fact]
    public void ExplicitLocalFilenameDoesNotRequireTheRemoteFilenameToBeWindowsSafe()
    {
        string destination = System.IO.Path.Combine(folder, "safe.txt");
        Assert.Equal(destination, StorageFileFrontend.ResolveDestination(destination, "remote/CON"));
        Assert.Throws<ArgumentException>(() => StorageFileFrontend.ResolveDestination(folder, "remote/a."));
    }

    private static StorageContext Context() => new("fixture", Fixture.DefaultProfile, enableFileBackupRequestIntent: true);

    private static HttpResponseMessage Properties(HttpStatusCode status)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent([]) };
        response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
        response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        response.Content.Headers.ContentLength = 0;
        response.Headers.Add("x-ms-type", "File");
        return response;
    }

    private static HttpResponseMessage Page(params XElement[] entries) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(new XElement("EnumerationResults",
            new XAttribute("ServiceEndpoint", "https://fixture.file.core.windows.net/"),
            new XAttribute("ShareName", "share"), new XAttribute("DirectoryPath", ""),
            new XElement("Entries", entries), new XElement("NextMarker")).ToString())
    };
}
