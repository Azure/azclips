// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Microsoft.Azure.Azclips.Extensions.Storage.Processors;
using Fixture = Extensions.Storage.Tests.StorageFoundationTests.Fixture;

namespace Extensions.Storage.Tests;

public sealed class BlobBatchDownloadTests : IDisposable
{
    private readonly string destination = Path.Combine(Path.GetTempPath(), "azclips-batch-" + Guid.NewGuid().ToString("N"));

    public BlobBatchDownloadTests() => Directory.CreateDirectory(destination);

    [Fact]
    public async Task AllPagesIncludingEmptyPagesArePlannedBeforeAnyDownload()
    {
        var service = new BatchService("one", "nested/two", "literal%2F+name")
        {
            Pages = [(null, ["one"]), ("empty", []), ("last", ["nested/two", "literal%2F+name"])]
        };
        var fixture = new Fixture(service.SendAsync);
        await Processor(fixture).ProcessNativeAsync();
        Assert.Equal(new[] { "one", "nested/two", "literal%2F+name" }, fixture.Output.Cast<string>());
        Assert.Equal(3, service.Lists);
        Assert.Equal(3, service.ListsBeforeFirstDownload);
        foreach (var (name, bytes) in service.Blobs)
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(destination, name.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal(100, fixture.Progress.Last().Value);
        Assert.All(fixture.Progress.SkipLast(1), item => Assert.InRange(item.Value, 0, 99));
        Assert.Equal(fixture.Progress.Select(item => item.Value).OrderBy(value => value), fixture.Progress.Select(item => item.Value));
        Assert.All(fixture.TokenRequests, request => Assert.Equal(new[] { "https://storage.azure.com/.default" }, request.Scopes));
    }

    [Fact]
    public async Task FilterRunsOnOriginalNamesAndExcludedPathsCannotBlockDownloads()
    {
        var service = new BatchService("../unsafe", "keep/file", "other");
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.NameFilter = name => name.StartsWith("keep/", StringComparison.Ordinal);
        await processor.ProcessNativeAsync();
        Assert.Equal("keep/file", Assert.Single(fixture.Output));
        Assert.Single(service.Heads);
        Assert.True(File.Exists(Path.Combine(destination, "keep", "file")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("folder/../../outside")]
    [InlineData("folder/../file")]
    [InlineData(".")]
    [InlineData("/")]
    public async Task InvalidLateNamesPreventAllLocalWrites(string name)
    {
        var service = new BatchService("good", name);
        var fixture = new Fixture(service.SendAsync);
        Exception? error = await Record.ExceptionAsync(() => Processor(fixture).ProcessNativeAsync());
        Assert.True(error is ArgumentException or InvalidDataException, error?.ToString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        Assert.Empty(service.Heads);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("file", "/file")]
    [InlineData("nested/file", "nested//file")]
    [InlineData("parent", "parent/child")]
    [InlineData("parent/child", "parent")]
    public async Task NormalizedAndFileDirectoryCollisionsFailBeforeWrites(string first, string second)
    {
        var service = new BatchService(first, second);
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        Assert.Empty(service.Heads);
        Assert.Empty(fixture.Output);
    }

    [Theory]
    [InlineData("File", "file")]
    [InlineData("folder\\file", "folder/file")]
    [InlineData("folder\\..\\file", "file")]
    public async Task WindowsAliasesAreRejectedBeforeWrites(string first, string second)
    {
        if (!OperatingSystem.IsWindows()) return;
        var service = new BatchService(first, second);
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.Empty(service.Heads);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    [Theory]
    [InlineData("C:/outside")]
    [InlineData("folder/file:stream")]
    [InlineData("folder/NUL.txt")]
    [InlineData("folder/NUL .txt")]
    [InlineData("folder/COM\u00b9.txt")]
    [InlineData("LPT\u00b2/file")]
    [InlineData("CONIN$")]
    [InlineData("CONOUT$")]
    [InlineData("COM1/file")]
    [InlineData("folder /file")]
    [InlineData("folder/file.")]
    [InlineData("folder/file?")]
    public async Task WindowsUnrepresentablePathsFailDuringPreflight(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        var service = new BatchService("good", name);
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<ArgumentException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.Empty(service.Heads);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingFilesRequireExplicitOverwrite(bool overwrite)
    {
        string path = Path.Combine(destination, "existing");
        await File.WriteAllBytesAsync(path, [9]);
        var service = new BatchService("new", "existing");
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Overwrite = overwrite;
        if (overwrite)
        {
            await processor.ProcessNativeAsync();
            Assert.Equal(service.Blobs["existing"], await File.ReadAllBytesAsync(path));
            Assert.Equal(2, fixture.Output.Count);
        }
        else
        {
            await Assert.ThrowsAsync<IOException>(() => processor.ProcessNativeAsync());
            Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(path));
            Assert.False(File.Exists(Path.Combine(destination, "new")));
            Assert.Empty(service.Heads);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingFileDirectoryConflictsPreventEarlierDownloads(bool directory)
    {
        string path = Path.Combine(destination, "conflict");
        if (directory) Directory.CreateDirectory(path);
        else await File.WriteAllBytesAsync(path, [9]);
        var service = new BatchService("new", directory ? "conflict" : "conflict/file");
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.Overwrite = true;
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessNativeAsync());
        Assert.False(File.Exists(Path.Combine(destination, "new")));
        Assert.Empty(service.Heads);
    }

    [Fact]
    public async Task DryRunListsAndReportsNamesWithoutCreatingDirectoriesOrOpeningDestinations()
    {
        await File.WriteAllBytesAsync(Path.Combine(destination, "existing"), [9]);
        var service = new BatchService("existing", "nested/file");
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.DryRun = true;
        await processor.ProcessNativeAsync();
        Assert.Empty(service.Heads);
        Assert.False(Directory.Exists(Path.Combine(destination, "nested")));
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(Path.Combine(destination, "existing")));
        Assert.Empty(fixture.Output);
        Assert.Empty(fixture.Progress);
        Assert.Equal(3, fixture.Messages.Count(message => message.Id == Events.Warning));
    }

    [Fact]
    public async Task WhatIfDoesNotEvenList()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.ShouldProcess = (_, _) => false;
        await processor.ProcessNativeAsync();
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(fixture.Output);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(5, 8)]
    [InlineData(2, 0)]
    [InlineData(2, 8388609)]
    public async Task InvalidTransferControlsFailBeforeAuthentication(int workers, int chunk)
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Concurrency = workers; processor.ChunkSize = chunk;
        await Assert.ThrowsAsync<ArgumentException>(() => processor.ProcessNativeAsync());
        Assert.Empty(fixture.TokenRequests);
    }

    [Fact]
    public async Task MissingRootIsNotCreated()
    {
        var fixture = new Fixture();
        var processor = Processor(fixture);
        processor.Destination = Path.Combine(destination, "missing");
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => processor.ProcessNativeAsync());
        Assert.Empty(fixture.TokenRequests);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    [Fact]
    public async Task CyclicListingFailsBeforeTransferOrLocalWrites()
    {
        var service = new BatchService("one", "two")
        {
            Pages = [(null, ["one"]), ("repeated", ["two"]), ("repeated", [])]
        };
        var fixture = new Fixture(service.SendAsync);
        await Assert.ThrowsAsync<InvalidDataException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.Empty(service.Heads);
        Assert.Empty(fixture.Output);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
    }

    [Theory]
    [InlineData(304)]
    [InlineData(412)]
    public async Task FailedPreconditionsAreExplicitSkipsWithoutFalseErrorDiagnostics(int status)
    {
        var service = new BatchService("skip", "good");
        var fixture = new Fixture(request => request.Method == HttpMethod.Head && Name(request) == "skip"
            ? Task.FromResult(BlobWorkflowTests.Error((HttpStatusCode)status, "ConditionNotMet"))
            : service.SendAsync(request));
        await Processor(fixture).ProcessNativeAsync();
        Assert.Equal("good", Assert.Single(fixture.Output));
        Assert.False(File.Exists(Path.Combine(destination, "skip")));
        Assert.Contains(fixture.Messages, message => message.Id == Events.Warning && message.Message.Contains("\"skip\"", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.Contains("1 downloaded, 1 skipped", fixture.Progress.Last().Message);
    }

    [Fact]
    public async Task APreconditionFailureAfterWritingFailsAndDeletesThePartialFile()
    {
        var service = new BatchService("changed", "good");
        var fixture = new Fixture(request => request.Headers.Range?.Ranges.Single().From == 2 && Name(request) == "changed"
            ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.PreconditionFailed, "ConditionNotMet"))
            : service.SendAsync(request));
        var processor = Processor(fixture);
        processor.Concurrency = 1; processor.ChunkSize = 2;
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => processor.ProcessNativeAsync());
        Assert.Equal(HttpStatusCode.PreconditionFailed, error.StatusCode);
        Assert.False(File.Exists(Path.Combine(destination, "changed")));
        Assert.False(File.Exists(Path.Combine(destination, "good")));
        Assert.Empty(fixture.Output);
        Assert.Contains(fixture.Messages, message => message.Id == Events.Warning &&
            message.Message.Contains("partial local file was deleted", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task CorruptContentReportsOneFailureAndNeverCompletesTheBatch()
    {
        var service = new BatchService("corrupt", "later");
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Headers.Range is not null) response.Content.Headers.ContentMD5 = new byte[16];
            return response;
        });
        var processor = Processor(fixture);
        processor.ValidateContent = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessNativeAsync());
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.False(File.Exists(Path.Combine(destination, "later")));
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task DefiniteFailureStopsBatchAndRetainsOnlyEarlierSuccessNames()
    {
        var service = new BatchService("good", "denied", "later");
        var fixture = new Fixture(request => request.Method == HttpMethod.Head && Name(request) == "denied"
            ? Task.FromResult(BlobWorkflowTests.Error(HttpStatusCode.Forbidden, "AuthorizationPermissionMismatch"))
            : service.SendAsync(request));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.DoesNotContain("private-response", error.ToString());
        Assert.Equal("good", Assert.Single(fixture.Output));
        Assert.False(File.Exists(Path.Combine(destination, "later")));
        Assert.Contains(fixture.Messages, message => message.Id == Events.Warning && message.Message.Contains("\"denied\"", StringComparison.Ordinal));
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task CreationRaceCannotOverwriteTheRacingFile()
    {
        var service = new BatchService("race");
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Method == HttpMethod.Head) await File.WriteAllBytesAsync(Path.Combine(destination, "race"), [9]);
            return response;
        });
        await Assert.ThrowsAsync<IOException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.Equal(new byte[] { 9 }, await File.ReadAllBytesAsync(Path.Combine(destination, "race")));
        Assert.Empty(fixture.Output);
        Assert.Single(fixture.Messages, message => message.Id == Events.ResourceOperationFailed);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task TotalConcurrencyIsBoundedAcrossMultipleFiles(int workers)
    {
        var service = new BatchService("first", "second");
        int active = 0, peak = 0;
        var fixture = new Fixture(async request =>
        {
            if (request.Headers.Range is null) return await service.SendAsync(request);
            int current = Interlocked.Increment(ref active);
            int previous;
            do { previous = peak; } while (Interlocked.CompareExchange(ref peak, Math.Max(previous, current), previous) != previous);
            await Task.Delay(20);
            var response = await service.SendAsync(request);
            Interlocked.Decrement(ref active);
            return response;
        });
        var processor = Processor(fixture);
        processor.Concurrency = workers; processor.ChunkSize = 2;
        processor.CheckMd5 = true;
        await processor.ProcessNativeAsync();
        Assert.Equal(workers, peak);
        Assert.Equal(new[] { "first", "second" }, fixture.Output.Cast<string>());
        foreach (var (name, bytes) in service.Blobs)
            Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(destination, name)));
    }

    [Fact]
    public async Task BatchConditionsAreAppliedToHeadsAndPinnedGets()
    {
        var service = new BatchService("blob");
        var fixture = new Fixture(request =>
        {
            if (BlobWorkflowTests.Query(request)["comp"] != "list")
            {
                Assert.Equal("\"etag\"", request.Headers.IfMatch.Single().ToString());
                Assert.Equal("lease", request.Headers.GetValues("x-ms-lease-id").Single());
                Assert.Equal("tag='value'", request.Headers.GetValues("x-ms-if-tags").Single());
                Assert.Equal("\"other\"", request.Headers.IfNoneMatch.Single().ToString());
                Assert.NotNull(request.Headers.IfModifiedSince);
                Assert.NotNull(request.Headers.IfUnmodifiedSince);
            }
            Assert.Equal("17", BlobWorkflowTests.Query(request)["timeout"]);
            return service.SendAsync(request);
        });
        var processor = Processor(fixture);
        processor.IfMatch = "\"etag\""; processor.LeaseId = "lease"; processor.IfTags = "tag='value'";
        processor.IfNoneMatch = "\"other\""; processor.Timeout = 17;
        processor.IfModifiedSince = DateTimeOffset.Parse("2026-10-19T00:00:00Z");
        processor.IfUnmodifiedSince = DateTimeOffset.Parse("2026-10-20T00:00:00Z");
        await processor.ProcessNativeAsync();
        Assert.Equal("blob", Assert.Single(fixture.Output));
    }

    [Fact]
    public async Task NativeContainerCarriesItsOwnAccountAndProfileWithoutMutatingInputs()
    {
        var service = new BatchService("blob");
        var fixture = new Fixture(request =>
        {
            Assert.Equal("sourceaccount.blob.core.windows.net", request.RequestUri!.IdnHost);
            return service.SendAsync(request);
        });
        var profile = new Core.Abstractions.Profile { Name = "source", TenantId = "source-tenant", CloudName = "AzureCloud" };
        var processor = Processor(fixture);
        processor.Context = null; processor.Container = null;
        processor.ContainerObject = new StorageBlobContainer("container", new StorageContext("sourceaccount", profile));
        await processor.ProcessNativeAsync();
        Assert.Equal("blob", Assert.Single(fixture.Output));
        Assert.All(fixture.TokenRequests, request => Assert.Equal(profile, request.Profile));
        Assert.Null(processor.Context);
        Assert.Null(processor.Profile);
        Assert.Null(processor.Container);
    }

    [Fact]
    public async Task CancellationDuringListingPreventsAllWrites()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new BatchService("blob");
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(fixture).ProcessNativeAsync());
        Assert.Empty(fixture.Output);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        Assert.Empty(fixture.Progress);
    }

    [Fact]
    public async Task CancellationDuringTransferLeavesPartialFileButNoSuccessOrBatchCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new BatchService("blob", "later");
        int ranges = 0;
        var fixture = new Fixture(async request =>
        {
            var response = await service.SendAsync(request);
            if (request.Headers.Range is not null && ++ranges == 2) cancellation.Cancel();
            return response;
        }, token: cancellation.Token);
        var processor = Processor(fixture);
        processor.Concurrency = 1; processor.ChunkSize = 2;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessNativeAsync());
        Assert.Equal(service.Blobs["blob"][..2], await File.ReadAllBytesAsync(Path.Combine(destination, "blob")));
        Assert.False(File.Exists(Path.Combine(destination, "later")));
        Assert.Empty(fixture.Output);
        Assert.DoesNotContain(fixture.Progress, item => item.Value == 100);
    }

    [Fact]
    public async Task EmptySelectionHasNoChildOutputAndCompletes()
    {
        var service = new BatchService("excluded");
        var fixture = new Fixture(service.SendAsync);
        var processor = Processor(fixture);
        processor.NameFilter = _ => false;
        await processor.ProcessNativeAsync();
        Assert.Empty(fixture.Output);
        Assert.Empty(service.Heads);
        Assert.Equal(100, Assert.Single(fixture.Progress).Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitOverwritePreservesExistingHardLinksAndDirectoryJunctions(bool directory)
    {
        if (!OperatingSystem.IsWindows()) return;
        string outside = Path.Combine(Path.GetTempPath(), "azclips-batch-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        string target = Path.Combine(outside, "file");
        await File.WriteAllBytesAsync(target, [9]);
        string link = Path.Combine(destination, "linked");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? throw new InvalidOperationException("Windows command processor is unavailable."),
                ArgumentList = { "/d", "/c", "mklink", directory ? "/J" : "/H", link, directory ? outside : target },
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Could not start link creation.");
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, output + error);
            string name = directory ? "linked/file" : "linked";
            var service = new BatchService(name);
            var fixture = new Fixture(service.SendAsync);
            var processor = Processor(fixture);
            processor.Overwrite = true;
            await processor.ProcessNativeAsync();
            Assert.Equal(service.Blobs[name], await File.ReadAllBytesAsync(target));
            Assert.Equal(name, Assert.Single(fixture.Output));
        }
        finally
        {
            if (directory && Directory.Exists(link)) Directory.Delete(link);
            else File.Delete(link);
            Directory.Delete(outside, recursive: true);
        }
    }

    private DownloadStorageBlobBatchProcessor_Orchestrator Processor(Fixture fixture)
    {
        var processor = new DownloadStorageBlobBatchProcessor_Orchestrator
        {
            Context = BlobWorkflowTests.Context(), Container = "container", Destination = destination
        };
        processor.Init(fixture.Context);
        return processor;
    }

    private static string Name(HttpRequestMessage request) =>
        Uri.UnescapeDataString(request.RequestUri!.AbsolutePath["/container/".Length..]);

    public void Dispose() => Directory.Delete(destination, recursive: true);

    private sealed class BatchService
    {
        internal Dictionary<string, byte[]> Blobs { get; }
        internal (string? Marker, string[] Names)[]? Pages { get; init; }
        internal ConcurrentQueue<string> Heads { get; } = new();
        internal int Lists { get; private set; }
        internal int ListsBeforeFirstDownload { get; private set; }

        internal BatchService(params string[] names) =>
            Blobs = names.ToDictionary(name => name, _ => new byte[] { 0, 255, 42, 128, 13, 10, 3, 4, 5 });

        internal Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            if (BlobWorkflowTests.Query(request)["comp"] == "list")
            {
                string[] names = Pages is null ? Blobs.Keys.ToArray() : Pages[Lists].Names;
                string? marker = Pages is null || Lists + 1 == Pages.Length ? null : Pages[Lists + 1].Marker;
                Lists++;
                return Task.FromResult(BlobWorkflowTests.Page(marker, names.Select(name => BlobWorkflowTests.BlobElement(name)).ToArray()));
            }
            if (ListsBeforeFirstDownload == 0) ListsBeforeFirstDownload = Lists;
            string name = Name(request);
            byte[] bytes = Blobs[name];
            if (request.Method == HttpMethod.Head)
            {
                Heads.Enqueue(name);
                var head = BlobWorkflowTests.Head(length: bytes.Length);
                head.Content.Headers.ContentMD5 = StorageWireFormat.ContentMd5(bytes);
                return Task.FromResult(head);
            }
            Assert.Equal("\"etag\"", request.Headers.IfMatch.Single().ToString());
            var range = request.Headers.Range!.Ranges.Single();
            int start = checked((int)range.From!.Value), end = checked((int)range.To!.Value);
            byte[] body = bytes[start..(end + 1)];
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(body) };
            response.Headers.ETag = new EntityTagHeaderValue("\"etag\"");
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, bytes.Length);
            if (request.Headers.Contains("x-ms-range-get-content-md5"))
                response.Content.Headers.ContentMD5 = StorageWireFormat.ContentMd5(body);
            return Task.FromResult(response);
        }
    }
}
