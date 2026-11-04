using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The Storage data-plane commands. Record mode creates a run-scoped resource group and account,
/// runs the CLI surface with optional PowerShell readbacks, and deletes the group at the end;
/// playback replays that flow and an independent PowerShell body, without credentials.
/// Each scenario uses its own container so the recorded URIs stay stable.
/// </summary>
[Collection("Scenario Tests")]
public class StorageDataPlaneScenarioTests
{
    private const string Share = "scenario-files";
    private const string Location = "australiaeast";
    private const string PlaybackAccount = "azclipsdp";

    // Record mode suffixes both names with the run id; the sanitisers strip it again, so the
    // recordings and playback keep the base names. Account names allow no hyphen, hence "t".
    private static readonly RecorderMode s_mode = RecorderModeResolver.FromEnvironment();
    private static readonly string ResourceGroup = TestRun.ScopedName("scenario-storage-dp-rg", s_mode);
    private static readonly string Account = s_mode == RecorderMode.Record ? $"azclipsdpt{TestRun.RunId}" : PlaybackAccount;

    // The product rejects the all-zero placeholder subscription; the sanitisers map this one back to it.
    private const string PlaybackSubscriptionId = "22222222-2222-2222-2222-222222222222";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Storage];
    private static readonly ExtensionUnderTest[] s_setupExtensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Storage];
    private static readonly byte[] s_payload = [0x00, 0xFF, 0x80, 0x7F, 0x0A, 0x0D, 0xC3, 0x28, 0x41, 0x42];

    [Fact]
    public void BlobUploadDownload()
    {
        const string container = "scenario-blob-transfer";
        string? blockEtag = null;
        Run(nameof(BlobUploadDownload), [container], (host, scope, _) =>
        {
            byte[] block = Bytes(2500);
            byte[] page = Bytes(1536);
            string blockFile = scope.Write("block.bin", block);
            string pageFile = scope.Write("page.bin", page);
            string empty = scope.Write("empty.bin", []);

            // Block: three 1000-byte chunks on three workers, with every content setting.
            host.RunCli([.. Blob("upload", container), "--file", blockFile, "--name", "block.bin", "--chunk-size", "1000",
                "--max-connections", "3", "--validate-content", "--content-type", "application/x-scenario", "--content-encoding", "identity",
                "--content-language", "en-US", "--content-disposition", "attachment", "--content-cache-control", "no-cache",
                "--metadata", "origin=scenario"]);
            var shown = Show(host, container, "block.bin");
            var settings = shown.GetProperty("properties").GetProperty("contentSettings");
            Assert.Equal("application/x-scenario", settings.GetProperty("contentType").GetString());
            Assert.Equal("identity", settings.GetProperty("contentEncoding").GetString());
            Assert.Equal("en-US", settings.GetProperty("contentLanguage").GetString());
            Assert.Equal("attachment", settings.GetProperty("contentDisposition").GetString());
            Assert.Equal("no-cache", settings.GetProperty("cacheControl").GetString());
            Assert.Equal("BlockBlob", shown.GetProperty("properties").GetProperty("blobType").GetString());
            Assert.Equal(block.Length, shown.GetProperty("properties").GetProperty("contentLength").GetInt64());
            Assert.Equal("scenario", shown.GetProperty("metadata").GetProperty("origin").GetString());

            // Page: 512-byte pages; empty: a zero-length block blob.
            host.RunCli([.. Blob("upload", container), "--file", pageFile, "--name", "page.bin", "--type", "page",
                "--chunk-size", "512", "--max-connections", "1", "--validate-content"]);
            Assert.Equal("PageBlob", Show(host, container, "page.bin").GetProperty("properties").GetProperty("blobType").GetString());
            host.RunCli([.. Blob("upload", container), "--file", empty, "--name", "empty.bin"]);

            // Append: a missing target is created, an existing one grows, --overwrite recreates it.
            string chunk = scope.Write("chunk.txt", "abc"u8.ToArray());
            host.RunCli([.. Blob("upload", container), "--file", chunk, "--name", "log.txt", "--type", "append"]);
            Assert.Equal(3, Show(host, container, "log.txt").GetProperty("properties").GetProperty("contentLength").GetInt64());
            host.RunCli([.. Blob("upload", container), "--file", chunk, "--name", "log.txt", "--type", "append"]);
            var grown = Show(host, container, "log.txt").GetProperty("properties");
            Assert.Equal("AppendBlob", grown.GetProperty("blobType").GetString());
            Assert.Equal(6, grown.GetProperty("contentLength").GetInt64());
            host.RunCli([.. Blob("upload", container), "--file", chunk, "--name", "log.txt", "--type", "append", "--overwrite", "true"]);
            Assert.Equal(3, Show(host, container, "log.txt").GetProperty("properties").GetProperty("contentLength").GetInt64());

            // An empty page blob has zero capacity.
            host.RunCli([.. Blob("upload", container), "--file", empty, "--name", "empty-page.bin", "--type", "page"]);
            var emptyPage = Show(host, container, "empty-page.bin").GetProperty("properties");
            Assert.Equal("PageBlob", emptyPage.GetProperty("blobType").GetString());
            Assert.Equal(0, emptyPage.GetProperty("contentLength").GetInt64());

            // Without --name the blob takes the file name.
            string named = scope.Write("from-file-name.bin", s_payload);
            host.RunCli([.. Blob("upload", container), "--file", named]);
            Assert.Equal(s_payload.Length, Show(host, container, "from-file-name.bin").GetProperty("properties").GetProperty("contentLength").GetInt64());

            // Empty segments and trailing dots are preserved; dot-only segments are rejected before any request.
            foreach (string name in new[] { "nested//f", "trailing." })
            {
                host.RunCli([.. Blob("upload", container), "--file", named, "--name", name]);
                var special = Show(host, container, name);
                Assert.Equal(name, special.GetProperty("name").GetString());
                Assert.Equal(s_payload.Length, special.GetProperty("properties").GetProperty("contentLength").GetInt64());
            }
            foreach (string name in new[] { ".", "../x" })
                AssertFails(host, "Resource paths must be nonempty and cannot contain dot-only path segments.",
                    [.. Blob("upload", container), "--file", named, "--name", name]);

            // What the PowerShell part sends: a missing-blob probe, then a write that does not probe again.
            Assert.False(Exists(host, container, "ps-new.bin"));
            host.RunCli([.. Blob("upload", container), "--file", named, "--name", "ps-new.bin"]);
            Assert.False(Exists(host, container, "ps-new.log"));
            host.RunCli([.. Blob("upload", container), "--file", named, "--name", "ps-new.log", "--type", "append", "--overwrite", "true"]);
        },
        (ps, scope, context) =>
        {
            string empty = scope.Write("empty.bin", []);
            var uploaded = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<SetAzStorageBlobContentCmdlet>(new Dictionary<string, object>
            {
                ["File"] = empty, ["Container"] = container, ["Blob"] = "empty.bin", ["Context"] = context, ["Force"] = true,
            })));
            Assert.Equal(0, uploaded.Properties.ContentLength);
            Assert.Equal("BlockBlob", uploaded.Properties.BlobType);

            // Without Force a missing destination is created without a prompt, for block and append blobs.
            string named = scope.Write("from-file-name.bin", s_payload);
            foreach (var (name, type, expected) in new[] { ("ps-new.bin", "Block", "BlockBlob"), ("ps-new.log", "Append", "AppendBlob") })
            {
                var created = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<SetAzStorageBlobContentCmdlet>(new Dictionary<string, object>
                {
                    ["File"] = named, ["Container"] = container, ["Blob"] = name, ["BlobType"] = type, ["Context"] = context,
                })));
                Assert.Equal(expected, created.Properties.BlobType);
                Assert.Equal(s_payload.Length, created.Properties.ContentLength);
            }
        },
        new BlockIdRequestMatcher(), downloads: (ps, scope, context) =>
        {
            var native = new StorageBlobContainer(container, context);
            string kept = scope.Write("whatif.bin", [9]);
            Assert.Empty(ps.RunPowerShellCmdlet<GetAzStorageBlobContentCmdlet>(
                new Dictionary<string, object> { ["Path"] = kept, ["Force"] = true, ["WhatIf"] = true },
                [new StorageBlob("block.bin", native, new())]));
            Assert.Equal([9], File.ReadAllBytes(kept));

            string block = scope.PathOf("block.out");
            blockEtag = AssertDownloaded(ps, new Dictionary<string, object>
            {
                ["Destination"] = block, ["ChunkSize"] = 1000, ["ConcurrentTaskCount"] = 3,
            }, "block.bin", block, Bytes(2500), [new StorageBlob("block.bin", native, new())]).Properties.ETag;

            string folder = scope.Directory("page-download");
            AssertDownloaded(ps, new Dictionary<string, object> { ["Blob"] = "page.bin", ["Destination"] = folder },
                "page.bin", Path.Combine(folder, "page.bin"), Bytes(1536), [native]);

            string empty = scope.Write("empty.out", [9]);
            AssertDownloaded(ps, new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "empty.bin", ["Context"] = context, ["Destination"] = empty, ["Force"] = true,
            }, "empty.bin", empty, []);

            string append = scope.PathOf("append.out");
            AssertDownloaded(ps, new Dictionary<string, object>
            {
                ["Uri"] = $"https://{Account}.blob.core.windows.net/{container}/log.txt", ["Context"] = context, ["Path"] = append,
            }, "log.txt", append, "abc"u8.ToArray());
        }, assertRequests: requests =>
        {
            var blocks = requests.Where(request => request.Method == "PUT" && Query(request)["comp"] == "block" &&
                new Uri(request.Uri).AbsolutePath == $"/{container}/block.bin").ToArray();
            Assert.Equal(3, blocks.Length);
            Assert.Equal(new[] { 500, 1000, 1000 }, blocks.Select(request => Payload(request).Length).Order());
            foreach (var request in blocks)
            {
                AssertHeader(request, "Content-MD5", ContentMd5(Payload(request)));
            }
            var commit = SingleRequest(requests, request => Query(request)["comp"] == "blocklist" &&
                new Uri(request.Uri).AbsolutePath == $"/{container}/block.bin");
            AssertHeader(commit, "If-None-Match", "*");
            AssertHeader(commit, "x-ms-blob-content-type", "application/x-scenario");
            AssertHeader(commit, "x-ms-blob-content-encoding", "identity");
            AssertHeader(commit, "x-ms-blob-content-language", "en-US");
            AssertHeader(commit, "x-ms-blob-content-disposition", "attachment");
            AssertHeader(commit, "x-ms-blob-cache-control", "no-cache");
            AssertHeader(commit, "x-ms-meta-origin", "scenario");
            var staged = blocks.Select(request => Query(request)["blockid"]).ToArray();
            var committed = XDocument.Parse(commit.Body!).Root!.Elements("Uncommitted").Select(element => element.Value).ToArray();
            Assert.Equal(3, staged.Distinct().Count());
            Assert.Equal(staged.Order(), committed.Order());
            var byId = blocks.ToDictionary(request => Assert.IsType<string>(Query(request)["blockid"]), Payload);
            Assert.Equal(Bytes(2500), committed.SelectMany(id => byId[id]).ToArray());

            var pages = requests.Where(request => request.Method == "PUT" && Query(request)["comp"] == "page").ToArray();
            Assert.Equal(3, pages.Length);
            Assert.Equal(["bytes=0-511", "bytes=512-1023", "bytes=1024-1535"], pages.Select(request => Header(request, "Range")));
            for (int i = 0; i < pages.Length; i++)
            {
                Assert.Equal(Bytes(1536)[(i * 512)..((i + 1) * 512)], Payload(pages[i]));
                AssertHeader(pages[i], "x-ms-page-write", "update");
                AssertHeader(pages[i], "Content-MD5", ContentMd5(Payload(pages[i])));
            }

            var ranges = requests.Where(request => request.Method == "GET" && new Uri(request.Uri).AbsolutePath == $"/{container}/block.bin").ToArray();
            Assert.Equal(3, ranges.Length);
            Assert.Equal(["bytes=0-999", "bytes=1000-1999", "bytes=2000-2499"], ranges.Select(request => Header(request, "Range")).Order());
            foreach (var request in ranges) AssertHeader(request, "If-Match", Assert.IsType<string>(blockEtag));
            Assert.Equal(3, requests.Count(request => request.Method == "HEAD" && new Uri(request.Uri).AbsolutePath == $"/{container}/block.bin"));
        });
    }

    [Fact]
    public void BlobShowConditions()
    {
        const string container = "scenario-blob-show";
        Run(nameof(BlobShowConditions), [container], (host, scope, _) =>
        {
            host.RunCli([.. Blob("upload", container), "--file", scope.Write("c.bin", s_payload), "--name", "c.bin"]);
            var shown = Show(host, container, "c.bin");
            string etag = shown.GetProperty("properties").GetProperty("etag").GetString()!;
            Assert.Equal(JsonValueKind.Null, shown.GetProperty("snapshot").ValueKind);
            Assert.Equal("available", shown.GetProperty("properties").GetProperty("lease").GetProperty("state").GetString());

            Assert.Equal("c.bin", Show(host, container, "c.bin", "--if-match", etag).GetProperty("name").GetString());
            Assert.Equal("c.bin", Show(host, container, "c.bin", "--if-match", "*").GetProperty("name").GetString());
            Assert.Equal("c.bin", Show(host, container, "c.bin", "--if-modified-since", "2020-06-29T06:32Z").GetProperty("name").GetString());

            AssertServiceError(host, "ConditionNotMet", [.. Blob("show", container), "--name", "c.bin", "--if-none-match", etag]);
            AssertServiceError(host, "ConditionNotMet", [.. Blob("show", container), "--name", "c.bin", "--if-unmodified-since", "2020-06-29T06:32Z"]);
            AssertServiceError(host, "ConditionNotMet", [.. Blob("show", container), "--name", "c.bin", "--if-match", "\"0x8D000000000000\""]);

            // Tags are read only when asked for, through the blob endpoint as well.
            host.RunCli([.. Blob("upload", container), "--file", scope.Write("t.bin", s_payload), "--name", "t.bin", "--tags", "team=one"]);
            Assert.False(Show(host, container, "t.bin").TryGetProperty("tags", out JsonElement absent) && absent.ValueKind != JsonValueKind.Null);
            var tagged = Json(Assert.Single(host.RunCli(["storage", "blob", "show", "--blob-endpoint", $"https://{Account}.blob.core.windows.net/",
                "--container-name", container, "--name", "t.bin", "--include-tags", "true", "--auth-mode", "login"])));
            Assert.Equal("one", tagged.GetProperty("tags").GetProperty("team").GetString());
            // What the PowerShell part sends: the same lookup with tags.
            Show(host, container, "t.bin", "--include-tags", "true");
        },
        (ps, _, context) =>
        {
            var found = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "c.bin", ["Context"] = context,
            })));
            Assert.Equal(s_payload.Length, found.Properties.ContentLength);
            Assert.Null(found.Snapshot);
            var tagged = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "t.bin", ["IncludeTag"] = true, ["Context"] = context,
            })));
            Assert.Equal("one", tagged.Tags["team"]);
        });
    }

    [Fact]
    public void BlobList()
    {
        const string container = "scenario-blob-list";
        Run(nameof(BlobList), [container], (host, scope, live) =>
        {
            Assert.Empty(List(host, container));
            var marker = Assert.Single(List(host, container, "--show-next-marker"));
            Assert.Equal(JsonValueKind.Null, marker.GetProperty("nextMarker").ValueKind);

            string source = scope.Write("x.txt", "hello"u8.ToArray());
            foreach (string name in new[] { "a", "b/x", "dir/x", "dir/y" })
                host.RunCli([.. Blob("upload", container), "--file", source, "--name", name, "--metadata", "k=v"]);
            if (live) StorageBlobLiveSetup.CreateSnapshot(Account, container, "a");

            // A delimiter folds names under a shared prefix into one prefix item.
            var root = List(host, container, "--delimiter", "/");
            Assert.Equal(["a", "b/", "dir/"], root.Select(entry => entry.GetProperty("name").GetString()));
            Assert.Equal(["name"], root[1].EnumerateObject().Select(property => property.Name));

            Assert.Equal(["dir/x", "dir/y"], List(host, container, "--prefix", "dir/").Select(entry => entry.GetProperty("name").GetString()));

            // num-results caps the page; the marker continues from it.
            Assert.Single(List(host, container, "--num-results", "1"));
            var page = List(host, container, "--num-results", "1", "--show-next-marker");
            Assert.Equal(2, page.Count);
            string next = page[1].GetProperty("nextMarker").GetString()!;
            Assert.False(string.IsNullOrEmpty(next));
            Assert.Equal(["b/x", "dir/x", "dir/y"], List(host, container, "--marker", next).Select(entry => entry.GetProperty("name").GetString()));

            var withMetadata = List(host, container, "--prefix", "dir/x", "--include", "m");
            Assert.Equal("v", Assert.Single(withMetadata).GetProperty("metadata").GetProperty("k").GetString());

            // An empty delimiter lists flat; the account can be addressed by its blob endpoint.
            var flat = Json(Assert.Single(host.RunCli(["storage", "blob", "list", "--blob-endpoint", $"https://{Account}.blob.core.windows.net/",
                "--container-name", container, "--delimiter", "", "--auth-mode", "login"])));
            Assert.Equal(["a", "b/x", "dir/x", "dir/y"], flat.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()));

            var withSnapshots = List(host, container, "--prefix", "a", "--include", "s");
            Assert.Equal(2, withSnapshots.Count);
            Assert.Single(withSnapshots, entry => entry.GetProperty("snapshot").ValueKind == JsonValueKind.String);
        },
        (ps, _, context) =>
        {
            var found = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "dir/x", ["Context"] = context,
            })));
            Assert.Equal("v", found.Metadata["k"]);
        });
    }

    [Fact]
    public void BlobSetTier()
    {
        const string container = "scenario-blob-tier";
        Run(nameof(BlobSetTier), [container], (host, scope, _) =>
        {
            string source = scope.Write("t.bin", s_payload);
            host.RunCli([.. Blob("upload", container), "--file", source, "--name", "t.bin"]);

            foreach (string tier in new[] { "Cool", "Hot", "Archive" })
            {
                Assert.Empty(host.RunCli([.. Blob("set-tier", container), "--name", "t.bin", "--tier", tier]));
                Assert.Equal(tier, Show(host, container, "t.bin").GetProperty("properties").GetProperty("blobTier").GetString());
            }

            // Rehydration starts but the blob stays archived until it completes.
            Assert.Empty(host.RunCli([.. Blob("set-tier", container), "--name", "t.bin", "--tier", "Cool", "--rehydrate-priority", "High"]));
            var rehydrating = Show(host, container, "t.bin").GetProperty("properties");
            Assert.Equal("Archive", rehydrating.GetProperty("blobTier").GetString());
            Assert.Equal("rehydrate-pending-to-cool", rehydrating.GetProperty("rehydrationStatus").GetString());

            AssertFails(host, "Parameter 'tier' value 'Bogus' is not allowed.", [.. Blob("set-tier", container), "--name", "t.bin", "--tier", "Bogus"]);
            AssertFails(host, "Parameter 'rehydrate-priority' value 'Middle' is not allowed.",
                [.. Blob("set-tier", container), "--name", "t.bin", "--tier", "Cool", "--rehydrate-priority", "Middle"]);
            AssertFails(host, "The tier or rehydration priority does not apply to the selected blob type.",
                [.. Blob("set-tier", container), "--name", "t.bin", "--tier", "P10"]);

            // The tier is checked against the real blob type before any change.
            host.RunCli([.. Blob("upload", container), "--file", scope.Write("p.bin", new byte[512]), "--name", "p.bin", "--type", "page"]);
            host.RunCli([.. Blob("upload", container), "--file", source, "--name", "a.log", "--type", "append"]);
            foreach (string name in new[] { "p.bin", "a.log" })
                AssertFails(host, "The requested tier or rehydration priority does not apply to this blob type.",
                    [.. Blob("set-tier", container), "--name", name, "--tier", "Hot"]);
            Assert.Equal(JsonValueKind.Null, Show(host, container, "a.log").GetProperty("properties").GetProperty("blobTier").ValueKind);
        },
        (ps, _, context) =>
        {
            var found = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "t.bin", ["Context"] = context,
            })));
            Assert.Equal("BlockBlob", found.Properties.BlobType);
            Assert.Equal(s_payload.Length, found.Properties.ContentLength);
        });
    }

    [Fact]
    public void BlobDeleteAndExists()
    {
        const string container = "scenario-blob-delete";
        Run(nameof(BlobDeleteAndExists), [container], (host, scope, live) =>
        {
            string source = scope.Write("d.bin", s_payload);
            host.RunCli([.. Blob("upload", container), "--file", source, "--name", "only.bin"]);
            host.RunCli([.. Blob("upload", container), "--file", source, "--name", "include.bin"]);
            if (live)
            {
                StorageBlobLiveSetup.CreateSnapshot(Account, container, "only.bin");
                StorageBlobLiveSetup.CreateSnapshot(Account, container, "include.bin");
            }
            Assert.True(Exists(host, container, "only.bin"));
            Assert.False(Exists(host, container, "missing.bin"));

            // only removes the snapshots and keeps the base blob.
            Assert.Equal(2, List(host, container, "--prefix", "only.bin", "--include", "s").Count);
            Assert.Empty(host.RunCli([.. Blob("delete", container), "--name", "only.bin", "--delete-snapshots", "only"]));
            Assert.True(Exists(host, container, "only.bin"));
            var remaining = Assert.Single(List(host, container, "--prefix", "only.bin", "--include", "s"));
            Assert.Equal(JsonValueKind.Null, remaining.GetProperty("snapshot").ValueKind);

            // include removes the base blob and its snapshots.
            Assert.Empty(host.RunCli([.. Blob("delete", container), "--name", "include.bin", "--delete-snapshots", "include"]));
            Assert.False(Exists(host, container, "include.bin"));
            Assert.Empty(List(host, container, "--prefix", "include.bin", "--include", "s"));

            // A blob with snapshots cannot be deleted without choosing what to do with them.
            if (live) StorageBlobLiveSetup.CreateSnapshot(Account, container, "only.bin");
            AssertServiceError(host, "SnapshotsPresent", [.. Blob("delete", container), "--name", "only.bin"]);
        },
        (ps, _, context) =>
        {
            Assert.Empty(ps.RunPowerShellCmdlet<RemoveAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "only.bin", ["Context"] = context, ["DeleteSnapshot"] = true,
            }));
        });
    }

    [Fact]
    public void BlobBatchDownload()
    {
        const string container = "scenario-blob-batch";
        Run(nameof(BlobBatchDownload), [container], (host, scope, _) =>
        {
            byte[] content = Bytes(300);
            string source = scope.Write("s.bin", content);
            foreach (string name in new[] { "nested/a.bin", "nested/b.bin", "top.bin" })
                host.RunCli([.. Blob("upload", container), "--file", source, "--name", name]);

            // The pattern keeps the blob hierarchy under the destination.
            string batch = scope.Directory("batch");
            var names = Assert.Single(host.RunCli([.. Batch(container, batch), "--pattern", "nested/*", "--max-connections", "3", "--no-progress"]));
            Assert.Equal(["nested/a.bin", "nested/b.bin"], Assert.IsType<string[]>(names));
            Assert.Equal(content, File.ReadAllBytes(Path.Combine(batch, "nested", "a.bin")));
            Assert.Equal(content, File.ReadAllBytes(Path.Combine(batch, "nested", "b.bin")));
            Assert.False(File.Exists(Path.Combine(batch, "top.bin")));
            Assert.Empty(Assert.IsType<string[]>(Assert.Single(host.RunCli([.. Batch(container, batch), "--pattern", "nomatch*"]))));

            // A second run into the same directory conflicts unless overwrite is set.
            AssertFails(host, "A batch destination file already exists; specify overwrite or choose another directory.",
                [.. Batch(container, batch), "--pattern", "nested/*"]);
            var again = Assert.Single(host.RunCli([.. Batch(container, batch), "--pattern", "nested/*", "--overwrite", "true", "--max-connections", "1"]));
            Assert.Equal(2, Assert.IsType<string[]>(again).Length);
            Assert.Equal(content, File.ReadAllBytes(Path.Combine(batch, "nested", "a.bin")));
            Assert.Equal(content, File.ReadAllBytes(Path.Combine(batch, "nested", "b.bin")));

            AssertFails(host, "The batch destination directory must exist.", Batch(container, scope.PathOf("missing-dir")));

            // A dry run lists the selection but writes nothing.
            string dry = scope.Directory("dry");
            Assert.Empty(host.RunCli([.. Batch(container, dry), "--pattern", "nested/*", "--dryrun", "true"]));
            Assert.Empty(Directory.EnumerateFileSystemEntries(dry));

            // A failed condition skips each blob without an error.
            foreach (string[] condition in new[] { new[] { "--if-match", "\"0x8D000000000000\"" }, ["--if-modified-since", "2099-01-01T00:00Z"] })
            {
                string skipped = scope.Directory("skipped" + condition[0]);
                var none = Assert.Single(host.RunCli([.. Batch(container, skipped), "--pattern", "nested/*", "--max-connections", "1", .. condition]));
                Assert.Empty(Assert.IsType<string[]>(none));
                Assert.Empty(Directory.EnumerateFiles(skipped, "*", SearchOption.AllDirectories));
            }
        },
        (ps, scope, context) =>
        {
            var found = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "top.bin", ["Context"] = context,
            })));
            Assert.Equal(300, found.Properties.ContentLength);
            var native = new StorageBlobContainer(container, context);
            string destination = scope.Directory("ps-batch");
            var downloaded = ps.RunPowerShellCmdlet<GetAzStorageBlobContentCmdlet>(
                new Dictionary<string, object> { ["Destination"] = destination },
                [new StorageBlob("nested/a.bin", native, new()), new StorageBlob("nested/b.bin", native, new())]).Cast<StorageBlob>().ToArray();
            Assert.Equal(["nested/a.bin", "nested/b.bin"], downloaded.Select(blob => blob.Name));
            Assert.Equal(Bytes(300), File.ReadAllBytes(Path.Combine(destination, "nested", "a.bin")));
            Assert.Equal(Bytes(300), File.ReadAllBytes(Path.Combine(destination, "nested", "b.bin")));
        });
    }

    [Fact]
    public void BlobCopyStart()
    {
        const string source = "scenario-copy-src";
        const string target = "scenario-copy-dst";
        const string sourceFile = "copy-src.txt";
        byte[] blobContent = "blob copy source"u8.ToArray();
        byte[] fileContent = "file copy source"u8.ToArray();
        Run(nameof(BlobCopyStart), [source, target], (host, scope, live) =>
        {
            host.RunCli([.. Blob("upload", source), "--file", scope.Write("src.txt", blobContent), "--name", "src.txt"]);
            host.RunCli([.. Files("file upload"), "--source", scope.Write(sourceFile, fileContent), "--path", sourceFile]);

            // Same account, login: azclips signs the source with a user delegation key.
            Copy(host, target, "from-login.txt", "--source-container", source, "--source-blob", "src.txt");
            AssertCopyCompleted(host, target, "from-login.txt", blobContent.Length);

            // Azure Files source: the account key comes from an explicit ARM lookup and signs a file SAS.
            Copy(host, target, "from-file.txt", "--source-share", Share, "--source-path", sourceFile,
                "--use-source-account-key-lookup", "true", "--source-key-resource-group", ResourceGroup,
                "--source-key-subscription-id", Subscription(host, live));
            AssertCopyCompleted(host, target, "from-file.txt", fileContent.Length);

            Copy(host, target, "cool.txt", "--source-container", source, "--source-blob", "src.txt", "--tier", "Cool");
            AssertCopyCompleted(host, target, "cool.txt", blobContent.Length);
            Assert.Equal("Cool", Show(host, target, "cool.txt").GetProperty("properties").GetProperty("blobTier").GetString());

            // An explicit absent-destination condition is kept: copying onto an existing blob conflicts.
            AssertServiceError(host, "BlobAlreadyExists",
                ["storage", "blob", "copy", "start", "--account-name", Account, "--destination-container", target, "--destination-blob", "from-login.txt",
                 "--source-container", source, "--source-blob", "src.txt", "--destination-if-none-match", "*", "--auth-mode", "login"]);

            AssertFails(host, "Account-key source signing is supported for Azure Files sources only.",
                ["storage", "blob", "copy", "start", "--account-name", Account, "--destination-container", target, "--destination-blob", "x.txt",
                 "--source-container", source, "--source-blob", "src.txt", "--use-source-account-key-lookup", "true",
                 "--source-key-resource-group", ResourceGroup, "--auth-mode", "login"]);
            AssertFails(host, "Normal Files-to-Blob copy requires a source service SAS, SourceAccountKey, or explicit UseSourceAccountKeyLookup.",
                ["storage", "blob", "copy", "start", "--account-name", Account, "--destination-container", target, "--destination-blob", "x.txt",
                 "--source-share", Share, "--source-path", sourceFile, "--auth-mode", "login"]);
        },
        (ps, scope, context) =>
        {
            var copied = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<StartAzStorageBlobCopyCmdlet>(new Dictionary<string, object>
            {
                ["SrcContainer"] = source, ["SrcBlob"] = "src.txt", ["DestContainer"] = target, ["DestBlob"] = "from-login.txt",
                ["Context"] = context, ["Force"] = true,
            })));
            Assert.Equal("from-login.txt", copied.Name);
            Assert.False(string.IsNullOrEmpty(copied.Properties.CopyId));
        }, downloads: (ps, scope, context) =>
        {
            foreach (var (name, content) in new[] { ("from-login.txt", blobContent), ("from-file.txt", fileContent), ("cool.txt", blobContent) })
            {
                string path = scope.PathOf("copy-" + name);
                AssertDownloaded(ps, new Dictionary<string, object>
                {
                    ["Container"] = target, ["Blob"] = name, ["Context"] = context, ["Destination"] = path,
                }, name, path, content);
            }
        });
    }

    [Theory]
    [InlineData(ClientType.CLI, "blob-sas")]
    [InlineData(ClientType.PowerShell, "blob-sas")]
    [InlineData(ClientType.CLI, "file-sas")]
    [InlineData(ClientType.PowerShell, "file-sas")]
    [InlineData(ClientType.CLI, "file-key")]
    [InlineData(ClientType.PowerShell, "file-key")]
    [InlineData(ClientType.CLI, "signed-uri")]
    [InlineData(ClientType.PowerShell, "signed-uri")]
    [InlineData(ClientType.CLI, "sas-wins")]
    [InlineData(ClientType.PowerShell, "sas-wins")]
    public async Task BlobCopySuppliedAuthorizationPlayback(ClientType client, string mode)
    {
        bool files = mode is "file-sas" or "file-key" or "sas-wins";
        string name = mode switch
        {
            "file-key" => "from-file-key.txt",
            "file-sas" or "sas-wins" => "from-file.txt",
            "signed-uri" => "from-uri-sas.txt",
            "blob-sas" => "from-sas.txt",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown copy-source test mode."),
        };
        string sas = $"sv=2026-06-06&sp=r&sr={(files ? "f" : "b")}&sig=scenario-signature";
        string source = $"https://{PlaybackAccount}.{(files ? "file" : "blob")}.core.windows.net/{(files ? Share + "/copy-src.txt" : "scenario-copy-src/src.txt")}";
        var record = OpenCopyPlayback();
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record);
        var authentication = new CopyAuthenticationInterceptor(interceptor);
        using var scope = new Scope();
        using var host = new ScenarioHost(client, authentication, extensions: s_extensions);
        var context = new StorageContext(PlaybackAccount, Assert.IsType<Profile>(
            await AzclipsServiceProvider.GetRequiredService<IProfileService>().GetActiveProfileAsync()));
        var parameters = new Dictionary<string, object>
        {
            ["DestContainer"] = "scenario-copy-dst", ["DestBlob"] = name, ["Context"] = context, ["Force"] = true,
        };
        var args = new List<string>();
        if (mode == "signed-uri")
        {
            args.AddRange(["--source-uri", source + "?" + sas]);
            parameters["SourceUri"] = source + "?" + sas;
        }
        else
        {
            args.AddRange(files ? ["--source-share", Share, "--source-path", "copy-src.txt"] :
                ["--source-container", "scenario-copy-src", "--source-blob", "src.txt"]);
            if (files) { parameters["SrcShareName"] = Share; parameters["SrcFilePath"] = "copy-src.txt"; }
            else { parameters["SrcContainer"] = "scenario-copy-src"; parameters["SrcBlob"] = "src.txt"; }
            if (mode == "file-key")
            {
                string key = Convert.ToBase64String(new byte[32]);
                args.AddRange(["--source-account-key", key]);
                parameters["SourceAccountKey"] = key;
            }
            else
            {
                args.AddRange(["--source-sas", sas]);
                parameters["SourceSas"] = sas;
                if (mode == "sas-wins")
                {
                    args.AddRange(["--source-account-key", "not-base64", "--use-source-account-key-lookup", "true"]);
                    parameters["SourceAccountKey"] = "not-base64";
                    parameters["UseSourceAccountKeyLookup"] = true;
                }
            }
        }

        if (client == ClientType.CLI) Copy(host, PlaybackAccount, "scenario-copy-dst", name, [.. args]);
        else
        {
            var copied = Assert.IsType<StorageBlob>(Assert.Single(host.RunPowerShellCmdlet<StartAzStorageBlobCopyCmdlet>(parameters)));
            Assert.Equal(name, copied.Name);
            Assert.True(Guid.TryParse(copied.Properties.CopyId, out _));
            Assert.Contains(copied.Properties.CopyStatus, new[] { "pending", "success" });
            Assert.Equal(source, copied.Properties.CopySource);
            string path = scope.PathOf(name);
            var downloaded = AssertDownloaded(host, new Dictionary<string, object>
            {
                ["Container"] = "scenario-copy-dst", ["Blob"] = name, ["Context"] = context, ["Destination"] = path,
            }, name, path, files ? "file copy source"u8.ToArray() : "blob copy source"u8.ToArray());
            Assert.Equal("success", downloaded.Properties.CopyStatus);
        }
        var request = SingleRequest(interceptor.Sent, request => request.Method == "PUT");
        var authorized = new Uri(Header(request, "x-ms-copy-source"));
        var sourceQuery = HttpUtility.ParseQueryString(authorized.Query);
        Assert.Equal(source, authorized.GetLeftPart(UriPartial.Path));
        if (mode == "file-key")
        {
            Assert.Equal("r", sourceQuery["sp"]);
            Assert.Equal("f", sourceQuery["sr"]);
            Assert.False(string.IsNullOrEmpty(sourceQuery["sig"]));
        }
        else Assert.Equal(sas, authorized.Query.TrimStart('?'));
        Assert.Empty(interceptor.Sent.Where(request => request.Method == "POST").Select(request => request.Method));
        Assert.All(authentication.Requests, auth =>
        {
            Assert.Equal("default", auth.Profile.Name);
            Assert.Equal(["https://storage.azure.com/.default"], auth.Scopes);
        });
        foreach (var sent in interceptor.Sent) Assert.Equal("Bearer", Header(sent, "Authorization").Split(' ', 2)[0]);
    }

    [Fact]
    public async Task BlobCopySourceProfilePlayback()
    {
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, OpenCopyPlayback());
        var authentication = new CopyAuthenticationInterceptor(interceptor);
        using var host = new ScenarioHost(ClientType.PowerShell, authentication, extensions: s_extensions);
        var profiles = AzclipsServiceProvider.GetRequiredService<IProfileService>();
        var active = Assert.IsType<Profile>(await profiles.GetActiveProfileAsync());
        var sourceProfile = new Profile { Name = "source", TenantId = active.TenantId, CloudName = "AzureCloud" };
        await profiles.SaveProfileAsync(sourceProfile);
        var destination = new StorageContext(PlaybackAccount, active);
        var copied = Assert.IsType<StorageBlob>(Assert.Single(host.RunPowerShellCmdlet<StartAzStorageBlobCopyCmdlet>(new Dictionary<string, object>
        {
            ["SrcContainer"] = "scenario-copy-src", ["SrcBlob"] = "src.txt", ["DestContainer"] = "scenario-copy-dst", ["DestBlob"] = "from-login.txt",
            ["SrcContext"] = new StorageContext(PlaybackAccount, sourceProfile), ["DestContext"] = destination, ["Force"] = true,
        })));
        Assert.Equal("default", copied.Context.Profile.Name);
        Assert.Equal("source", authentication.Requests[0].Profile.Name);
        Assert.Equal(sourceProfile.TenantId, authentication.Requests[0].Profile.TenantId);
        Assert.All(authentication.Requests.Skip(1), auth => Assert.Equal("default", auth.Profile.Name));
        Assert.All(authentication.Requests, auth => Assert.Equal(["https://storage.azure.com/.default"], auth.Scopes));
        SingleRequest(interceptor.Sent, request => request.Method == "POST" && Query(request)["comp"] == "userdelegationkey");
        SingleRequest(interceptor.Sent, request => request.Method == "PUT");
    }

    [Theory]
    [InlineData(ClientType.CLI)]
    [InlineData(ClientType.PowerShell)]
    public async Task SuppliedSourceSasFailureNeverFallsBackPlayback(ClientType client)
    {
        var record = OpenCopyPlayback();
        var consumed = record.Entries.Select((entry, index) => (entry, index))
            .Where(item => item.entry.Request.Method == "PUT" && item.entry.Request.Uri.EndsWith("/from-login.txt", StringComparison.Ordinal) &&
                item.entry.Response.StatusCode is >= 200 and < 300).Select(item => item.index);
        var interceptor = new RecordPlaybackInterceptor(RecorderMode.Playback, record, alreadyConsumed: consumed);
        using var host = new ScenarioHost(client, interceptor, extensions: s_extensions);
        const string sas = "sv=2026-06-06&sp=r&sr=b&sig=scenario-signature";
        if (client == ClientType.CLI)
            AssertServiceError(host, "BlobAlreadyExists", ["storage", "blob", "copy", "start", "--account-name", PlaybackAccount,
                "--destination-container", "scenario-copy-dst", "--destination-blob", "from-login.txt", "--source-container", "scenario-copy-src",
                "--source-blob", "src.txt", "--source-sas", sas, "--source-account-key", "not-base64", "--auth-mode", "login"]);
        else
        {
            var context = new StorageContext(PlaybackAccount, Assert.IsType<Profile>(
                await AzclipsServiceProvider.GetRequiredService<IProfileService>().GetActiveProfileAsync()));
            var error = Assert.Throws<System.Management.Automation.CmdletInvocationException>(() => host.RunPowerShellCmdlet<StartAzStorageBlobCopyCmdlet>(new Dictionary<string, object>
            {
                ["SrcContainer"] = "scenario-copy-src", ["SrcBlob"] = "src.txt", ["DestContainer"] = "scenario-copy-dst",
                ["DestBlob"] = "from-login.txt", ["Context"] = context, ["SourceSas"] = sas, ["SourceAccountKey"] = "not-base64", ["Force"] = true,
            }));
            Assert.Contains("HTTP 409", error.Message);
        }
        Assert.Equal("PUT", Assert.Single(interceptor.Sent).Method);
    }

    private static SessionRecord OpenCopyPlayback()
    {
        var record = RecordingSession.Open(RecorderMode.Playback,
            SessionRecordFiles.GetPath(typeof(StorageDataPlaneScenarioTests), nameof(BlobCopyStart))).Record;
        MakePlayable(record);
        return record;
    }

    private sealed class CopyAuthenticationInterceptor(RecordPlaybackInterceptor playback) : ITracingInterceptor
    {
        public List<RequestAuthentication> Requests { get; } = [];
        public SendAsyncStep SendAsync => async (request, callback, next) =>
        {
            Assert.True(request.Options.TryGetValue(RequestAuthentication.OptionsKey, out var authentication));
            Assert.NotNull(authentication);
            Requests.Add(authentication);
            return await playback.SendAsync(request, callback, next);
        };
    }

    [Fact]
    public void FileAndDirectory()
    {
        const string directory = "scenario-dir";
        Run(nameof(FileAndDirectory), [], (host, scope, _) =>
        {
            string md5 = ContentMd5(s_payload);
            host.RunCli([.. Files("file upload"), "--source", scope.Write("ps.bin", s_payload),
                "--path", $"{directory}/sub/", "--content-md5", md5]);

            byte[] content = Bytes(1200);
            var uploaded = Json(Assert.Single(host.RunCli([.. Files("file upload"), "--source", scope.Write("f.bin", content),
                "--path", $"{directory}/f.bin", "--content-type", "application/x-scenario", "--metadata", "k=v", "--max-connections", "1"])));
            Assert.StartsWith("\"0x", uploaded.GetProperty("etag").GetString());

            var listed = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", directory])));
            Assert.Equal(["f.bin", "sub"], listed.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Order());
            var directories = Json(Assert.Single(host.RunCli([.. Files("directory list"), "--name", directory])));
            Assert.Equal(["sub"], directories.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()));

            string target = scope.PathOf("f.out");
            var downloaded = Json(Assert.Single(host.RunCli([.. Files("file download"), "--path", $"{directory}/f.bin", "--dest", target,
                "--max-connections", "1"])));
            Assert.Equal(content.Length, downloaded.GetProperty("properties").GetProperty("contentLength").GetInt64());
            Assert.Equal("application/x-scenario", downloaded.GetProperty("properties").GetProperty("contentSettings").GetProperty("contentType").GetString());
            Assert.Equal(content, File.ReadAllBytes(target));
            // A missing local parent directory is created.
            string nested = scope.PathOf(Path.Combine("missing", "parent", "f.out"));
            host.RunCli([.. Files("file download"), "--path", $"{directory}/f.bin", "--dest", nested, "--max-connections", "1"]);
            Assert.Equal(content, File.ReadAllBytes(nested));

            string range = scope.PathOf("f.range");
            host.RunCli([.. Files("file download"), "--path", $"{directory}/f.bin", "--dest", range, "--start-range", "100", "--end-range", "199"]);
            Assert.Equal(content[100..200], File.ReadAllBytes(range));

            // Files OAuth needs the explicit backup intent; nothing is sent without it.
            AssertFails(host, "Azure Files OAuth requires explicit EnableFileBackupRequestIntent.",
                ["storage", "file", "list", "--account-name", Account, "--share-name", Share, "--path", directory]);

            AssertServiceError(host, "ShareNotFound",
                ["storage", "file", "upload", "--account-name", Account, "--share-name", "scenario-noshare", "--enable-file-backup-request-intent", "true",
                 "--source", scope.Write("n.bin", s_payload), "--path", "n.bin"]);

            // Download output carries the stored metadata; ranges past the end are clamped, open-ended ranges run to the end.
            Assert.Equal("v", downloaded.GetProperty("metadata").GetProperty("k").GetString());
            foreach (var (start, end, expected) in new (string, string?, byte[])[] { ("1100", "9999", content[1100..]), ("1100", null, content[1100..]), ("0", "0", content[..1]) })
            {
                string[] bounds = end is null ? ["--start-range", start] : ["--start-range", start, "--end-range", end];
                host.RunCli([.. Files("file download"), "--path", $"{directory}/f.bin", "--dest", range, .. bounds]);
                Assert.Equal(expected, File.ReadAllBytes(range));
            }
            // A start at the file length fails after reading the size and leaves the destination untouched.
            string kept = scope.Write("kept.bin", [9]);
            AssertFails(host, "The requested range starts outside the file.",
                [.. Files("file download"), "--path", $"{directory}/f.bin", "--dest", kept, "--start-range", "1200"]);
            Assert.Equal([9], File.ReadAllBytes(kept));

            // The account can be addressed by its Files endpoint.
            var byEndpoint = Json(Assert.Single(host.RunCli(["storage", "file", "list", "--file-endpoint", $"https://{Account}.file.core.windows.net/",
                "--share-name", Share, "--enable-file-backup-request-intent", "true", "--path", directory])));
            Assert.Equal(["f.bin", "sub"], byEndpoint.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Order());

            // A listing limit returns a marker that resumes the listing.
            var page = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", directory, "--num-results", "1", "--show-next-marker"]))).EnumerateArray().ToList();
            Assert.Equal(2, page.Count);
            string next = page[1].GetProperty("nextMarker").GetString()!;
            var rest = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", directory, "--marker", next])));
            Assert.Equal(["f.bin", "sub"], new[] { page[0], rest.EnumerateArray().Single() }.Select(entry => entry.GetProperty("name").GetString()).Order());

            // A zero-byte upload infers its content type; a download into a directory keeps the remote file name.
            host.RunCli([.. Files("file upload"), "--source", scope.Write("empty.txt", []), "--path", $"{directory}/empty.txt", "--no-progress", "true"]);
            string folder = scope.Directory("into");
            var empty = Json(Assert.Single(host.RunCli([.. Files("file download"), "--path", $"{directory}/empty.txt", "--dest", folder, "--no-progress", "true"])));
            Assert.Equal("empty.txt", empty.GetProperty("name").GetString());
            Assert.Equal(0, empty.GetProperty("properties").GetProperty("contentLength").GetInt64());
            Assert.Equal("text/plain", empty.GetProperty("properties").GetProperty("contentSettings").GetProperty("contentType").GetString());
            Assert.Empty(File.ReadAllBytes(Path.Combine(folder, "empty.txt")));

            // A name with a percent sign and a plus is listed literally.
            host.RunCli([.. Files("file upload"), "--source", scope.Write("literal.bin", s_payload), "--path", $"{directory}/sub/literal%2F+name"]);
            var literal = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", $"{directory}/sub"])));
            Assert.Equal(["literal%2F+name"], literal.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Where(name => name != "ps.bin"));

            // A path naming an existing directory takes the source file name.
            host.RunCli([.. Files("file upload"), "--source", scope.Write("into.bin", s_payload), "--path", $"{directory}/sub"]);
            var into = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", $"{directory}/sub"])));
            Assert.Equal(["into.bin", "literal%2F+name"], into.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Where(name => name != "ps.bin").Order());

            // Missing parent directories are created; trailing dots are kept unless --disallow-trailing-dot is given.
            string[] backupIntent = ["storage", "file", "upload", "--account-name", Account, "--share-name", Share, "--backup-intent", "true"];
            host.RunCli([.. backupIntent, "--source", scope.Write("t.bin", s_payload), "--path", $"{directory}/new/deep/trail."]);
            host.RunCli([.. backupIntent, "--source", scope.Write("d.bin", s_payload), "--path", $"{directory}/new/deep/dot.", "--disallow-trailing-dot", "true"]);
            var deep = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", $"{directory}/new/deep"])));
            Assert.Equal(["dot", "trail."], deep.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Order());

            host.RunCli([.. Files("file upload"), "--source", scope.Write("p.bin", s_payload),
                "--path", $"{directory}/ps/deep/p.bin", "--content-md5", md5]);
            string copy = scope.PathOf("p.out");
            host.RunCli([.. Files("file download"), "--path", $"{directory}/ps/deep/p.bin", "--dest", copy]);
            Assert.Equal(s_payload, File.ReadAllBytes(copy));
        },
        (ps, _, _) =>
        {
            var context = Assert.IsType<StorageContext>(Assert.Single(ps.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                new Dictionary<string, object> { ["StorageAccountName"] = Account, ["EnableFileBackupRequestIntent"] = true })));
            Assert.Equal(Account, context.StorageAccountName);
            Assert.True(context.EnableFileBackupRequestIntent);
            var created = Assert.IsType<StorageFileDirectory>(Assert.Single(ps.RunPowerShellCmdlet<NewAzStorageDirectoryCmdlet>(
                new Dictionary<string, object> { ["ShareName"] = Share, ["Path"] = directory, ["Context"] = context, ["Confirm"] = false })));
            Assert.Equal(directory, created.Path);
            Assert.Same(context, created.Context);
            var subdirectory = Assert.IsType<StorageFileDirectory>(Assert.Single(ps.RunPowerShellCmdlet<NewAzStorageDirectoryCmdlet>(
                new Dictionary<string, object> { ["Path"] = "sub", ["Confirm"] = false }, [created])));
            Assert.Equal($"{directory}/sub", subdirectory.Path);

            var entries = ps.RunPowerShellCmdlet<GetAzStorageFileCmdlet>(new Dictionary<string, object>(), [created]);
            var file = Assert.Single(entries.OfType<StorageFile>());
            Assert.Equal($"{directory}/f.bin", file.Path);
            Assert.Equal(1200, file.Properties.ContentLength);
            Assert.Equal($"{directory}/sub", Assert.Single(entries.OfType<StorageFileDirectory>()).Path);

            var exact = Assert.IsType<StorageFile>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageFileCmdlet>(
                new Dictionary<string, object> { ["ShareName"] = Share, ["Path"] = $"{directory}/f.bin", ["Context"] = context })));
            Assert.Equal(file.Path, exact.Path);
            Assert.Equal("v", exact.Properties.Metadata["k"]);
            Assert.Equal("application/x-scenario", exact.Properties.ContentType);
            var foundDirectory = Assert.IsType<StorageFileDirectory>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageFileCmdlet>(
                new Dictionary<string, object> { ["ShareName"] = Share, ["Path"] = subdirectory.Path, ["Context"] = context })));
            Assert.Equal(subdirectory.Path, foundDirectory.Path);
            Assert.Empty(ps.RunPowerShellCmdlet<NewAzStorageDirectoryCmdlet>(
                new Dictionary<string, object> { ["Path"] = "what-if", ["WhatIf"] = true }, [created]));
        }, assertRequests: requests =>
        {
            foreach (var request in requests.Where(request => new Uri(request.Uri).Host.EndsWith(".file.core.windows.net", StringComparison.Ordinal)))
                AssertHeader(request, "x-ms-file-request-intent", "backup");
            var create = SingleRequest(requests, request => request.Method == "PUT" && string.IsNullOrEmpty(new Uri(request.Uri).Query) &&
                new Uri(request.Uri).AbsolutePath == $"/{Share}/{directory}/f.bin");
            AssertHeader(create, "x-ms-content-type", "application/x-scenario");
            AssertHeader(create, "x-ms-meta-k", "v");
            var upload = SingleRequest(requests, request => request.Method == "PUT" && Query(request)["comp"] == "range" &&
                new Uri(request.Uri).AbsolutePath == $"/{Share}/{directory}/f.bin");
            Assert.Equal(Bytes(1200), Payload(upload));
            AssertHeader(upload, "Range", "bytes=0-1199");
            AssertHeader(upload, "x-ms-write", "update");
            var ranges = requests.Where(request => request.Method == "GET" && string.IsNullOrEmpty(new Uri(request.Uri).Query) &&
                new Uri(request.Uri).AbsolutePath == $"/{Share}/{directory}/f.bin");
            Assert.Equal(["bytes=0-1199", "bytes=0-1199", "bytes=100-199", "bytes=1100-1199", "bytes=1100-1199", "bytes=0-0"],
                ranges.Select(request => Header(request, "Range")));
            var trailing = requests.Where(request => new Uri(request.Uri).AbsolutePath.EndsWith("/trail.", StringComparison.Ordinal)).ToArray();
            Assert.True(trailing.Length > 0, "Expected requests for the trailing-dot file.");
            foreach (var request in trailing) AssertHeader(request, "x-ms-allow-trailing-dot", "true");
            var disallowed = requests.Where(request => new Uri(request.Uri).AbsolutePath.EndsWith("/dot.", StringComparison.Ordinal)).ToArray();
            Assert.True(disallowed.Length > 0, "Expected requests for the disallowed trailing-dot file.");
            foreach (var request in disallowed) AssertHeader(request, "x-ms-allow-trailing-dot", "false");
        });
    }

    [Fact]
    public void ServiceAndPreflightErrors()
    {
        const string container = "scenario-blob-errors";
        Run(nameof(ServiceAndPreflightErrors), [container], (host, scope, _) =>
        {
            string source = scope.Write("e.txt", "hello"u8.ToArray());
            host.RunCli([.. Blob("upload", container), "--file", source, "--name", "e.txt"]);

            AssertServiceError(host, "BlobNotFound", [.. Blob("show", container), "--name", "missing.txt"]);
            AssertServiceError(host, "BlobNotFound", [.. Blob("delete", container), "--name", "missing.txt"]);
            AssertServiceError(host, "ContainerNotFound", [.. Blob("show", "scenario-blob-nocontainer"), "--name", "e.txt"]);
            AssertServiceError(host, "BlobAlreadyExists", [.. Blob("upload", container), "--file", source, "--name", "e.txt"]);
            AssertServiceError(host, "ConditionNotMet", [.. Blob("delete", container), "--name", "e.txt", "--if-match", "\"0x8D000000000000\""]);
            AssertServiceError(host, "BlobAlreadyExists",
                [.. Blob("upload", container), "--file", source, "--name", "e.txt", "--overwrite", "true", "--if-none-match", "*"]);

            // A missing container makes exists false; append fails and never creates it.
            Assert.False(Exists(host, "scenario-blob-nocontainer", "e.txt"));
            AssertServiceError(host, "ContainerNotFound", [.. Blob("upload", "scenario-blob-nocontainer"), "--file", source, "--name", "e.txt", "--type", "append"]);
            // A page blob without overwrite fails at create when it already exists.
            string aligned = scope.Write("p.bin", new byte[512]);
            host.RunCli([.. Blob("upload", container), "--file", aligned, "--name", "p.bin", "--type", "page"]);
            AssertServiceError(host, "BlobAlreadyExists", [.. Blob("upload", container), "--file", aligned, "--name", "p.bin", "--type", "page"]);

            // What the PowerShell part sends: a probe of a blob in the missing container and a delete of a missing blob.
            Assert.False(Exists(host, "scenario-blob-nocontainer", "ps.txt"));
            AssertServiceError(host, "BlobNotFound", [.. Blob("delete", container), "--name", "missing.txt"]);

            // These are rejected before any request is sent.
            string unaligned = scope.Write("odd.bin", new byte[1000]);
            AssertFails(host, "Page Blob source length must be a multiple of 512 bytes and at most 8 TiB.",
                [.. Blob("upload", container), "--file", unaligned, "--type", "page"]);
            AssertFails(host, "Container/share names must contain 3-63 lowercase letters, digits or nonconsecutive internal hyphens.",
                [.. Blob("upload", "Bad_Name"), "--file", source]);
            AssertFails(host, "Resource paths must be nonempty and cannot contain dot-only path segments.",
                [.. Blob("show", container), "--name", "a/../b"]);
            AssertFails(host, "Parameter 'auth-mode' value 'key' is not allowed.",
                ["storage", "blob", "show", "--account-name", Account, "--container-name", container, "--name", "e.txt", "--auth-mode", "key"]);
            foreach (var option in new[] { new[] { "--max-connections", "0" }, ["--max-connections", "5"], ["--chunk-size", "0"] })
                AssertFails(host, "Use 1-4 workers and chunks of 1 through 8388608 bytes.",
                    [.. Blob("upload", container), "--file", source, "--name", "e2.txt", .. option]);
        },
        (ps, scope, context) =>
        {
            var missing = Assert.ThrowsAny<Exception>(() => ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "missing.txt", ["Context"] = context,
            }));
            Assert.Contains("HTTP 404", missing.Message);

            // Upload into a missing container fails at the probe; removing a missing blob is not success.
            var noContainer = Assert.ThrowsAny<Exception>(() => ps.RunPowerShellCmdlet<SetAzStorageBlobContentCmdlet>(new Dictionary<string, object>
            {
                ["File"] = scope.Write("ps.txt", s_payload), ["Container"] = "scenario-blob-nocontainer", ["Blob"] = "ps.txt", ["Context"] = context,
            }));
            Assert.Contains("HTTP 404", noContainer.Message);
            var removed = Assert.ThrowsAny<Exception>(() => ps.RunPowerShellCmdlet<RemoveAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "missing.txt", ["Context"] = context, ["PassThru"] = true, ["Confirm"] = false,
            }));
            Assert.Contains("HTTP 404", removed.Message);
        });
    }

    /// <summary>
    /// The PowerShell blob cmdlets with pipeline input. PowerShell lists with its own include set
    /// (metadata,copy,snapshots), which no CLI request sends, so this scenario records PowerShell itself.
    /// </summary>
    [Fact]
    public void BlobPowerShellPipeline()
    {
        const string container = "scenario-blob-ps";
        RunPowerShell(nameof(BlobPowerShellPipeline), container, (ps, scope, context, live) =>
        {
            string source = scope.Write("p.bin", s_payload);
            foreach (string name in new[] { "a", "b/x", "dir/x", "dir/y" })
                ps.RunPowerShellCmdlet<SetAzStorageBlobContentCmdlet>(new Dictionary<string, object>
                {
                    ["File"] = source, ["Container"] = container, ["Blob"] = name, ["Context"] = context, ["Force"] = true,
                    ["Metadata"] = new Hashtable { ["k"] = "v" },
                });
            if (live) StorageBlobLiveSetup.CreateSnapshot(Account, container, "a");
            var native = new StorageBlobContainer(container, context);

            // A container from the pipeline selects an exact name, a prefix listing, or a snapshot.
            var exact = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(
                new Dictionary<string, object> { ["Blob"] = "a" }, [native])));
            Assert.Equal("a", exact.Name);
            Assert.Null(exact.Snapshot);
            var prefixed = ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object> { ["Prefix"] = "dir/" }, [native])
                .Cast<StorageBlob>().ToList();
            Assert.Equal(["dir/x", "dir/y"], prefixed.Select(blob => blob.Name));
            var snapshot = Assert.Single(Blobs(ps, context, container, "a"), blob => blob.Snapshot is not null);
            var selected = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Blob"] = "a", ["SnapshotTime"] = DateTimeOffset.Parse(snapshot.Snapshot!, System.Globalization.CultureInfo.InvariantCulture),
            }, [native])));
            Assert.Equal(snapshot.Snapshot, selected.Snapshot);

            // Blobs from the pipeline are looked up one record at a time and keep their selector.
            var looked = ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>(), [prefixed[0], prefixed[1], snapshot])
                .Cast<StorageBlob>().ToList();
            Assert.Equal(["dir/x", "dir/y", "a"], looked.Select(blob => blob.Name));
            Assert.All(looked, blob => Assert.Equal(s_payload.Length, blob.Properties.ContentLength));
            Assert.Equal(snapshot.Snapshot, looked[2].Snapshot);

            // A listing returns native details and a continuation that resumes the same prefix.
            var first = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Prefix"] = "dir/", ["MaxCount"] = 1, ["Context"] = context,
            })));
            Assert.Equal("dir/x", first.Name);
            Assert.Equal("v", first.Metadata["k"]);
            Assert.NotNull(first.ContinuationToken);
            var rest = ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Prefix"] = "dir/", ["ContinuationToken"] = first.ContinuationToken!, ["Context"] = context,
            }).Cast<StorageBlob>();
            Assert.Equal(["dir/y"], rest.Select(blob => blob.Name));

            // MaxCount counts listed entries before the wildcard filter: the first three are a, its snapshot and b/x.
            var filtered = ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "*x", ["MaxCount"] = 3, ["Context"] = context,
            }).Cast<StorageBlob>();
            Assert.Equal(["b/x"], filtered.Select(blob => blob.Name));

            // Upload to a blob from the pipeline uses that blob's address.
            var uploaded = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<SetAzStorageBlobContentCmdlet>(
                new Dictionary<string, object> { ["File"] = source, ["Force"] = true },
                [new StorageBlob("native/item", native, new())])));
            Assert.Equal("native/item", uploaded.Name);
            Assert.Equal(s_payload.Length, uploaded.Properties.ContentLength);

            // Removal from the pipeline: a container with Force also removes snapshots; a snapshot removes only itself.
            Assert.True(Assert.IsType<bool>(Assert.Single(ps.RunPowerShellCmdlet<RemoveAzStorageBlobCmdlet>(
                new Dictionary<string, object> { ["Blob"] = "dir/y", ["Force"] = true, ["PassThru"] = true, ["Confirm"] = false }, [native]))));
            Assert.True(Assert.IsType<bool>(Assert.Single(ps.RunPowerShellCmdlet<RemoveAzStorageBlobCmdlet>(
                new Dictionary<string, object> { ["Force"] = true, ["PassThru"] = true, ["Confirm"] = false }, [snapshot]))));
            Assert.Null(Assert.Single(Blobs(ps, context, container, "a")).Snapshot);

            // Each piped blob is removed in its own record; without PassThru nothing is written.
            Assert.Empty(ps.RunPowerShellCmdlet<RemoveAzStorageBlobCmdlet>(new Dictionary<string, object> { ["Confirm"] = false },
                [new StorageBlob("b/x", native, new()), uploaded]));
            Assert.Equal(["a", "dir/x"], Blobs(ps, context, container, "").Select(blob => blob.Name));

            // A FileInfo from the pipeline binds its FullName to File.
            var piped = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<SetAzStorageBlobContentCmdlet>(
                new Dictionary<string, object> { ["Container"] = container, ["Blob"] = "piped.bin", ["Context"] = context, ["Force"] = true },
                [new FileInfo(source)])));
            Assert.Equal("piped.bin", piped.Name);
            Assert.Equal(s_payload.Length, piped.Properties.ContentLength);
        });
    }

    private static List<StorageBlob> Blobs(ScenarioHost ps, StorageContext context, string container, string prefix) =>
        [.. ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
        {
            ["Container"] = container, ["Prefix"] = prefix, ["Context"] = context,
        }).Cast<StorageBlob>()];

    /// <summary>
    /// Records or replays one PowerShell body. Record mode runs it against fresh resources from
    /// <see cref="CreateResources"/>; playback runs the same body without credentials.
    /// </summary>
    private static void RunPowerShell(string test, string container, Action<ScenarioHost, Scope, StorageContext, bool> body)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneScenarioTests), test);
        bool live = recording.Mode == RecorderMode.Record;
        RunWithCleanup(() =>
        {
            if (live) CreateResources([container]);
            using var scope = new Scope();
            using var host = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), useLiveAuth: live, extensions: s_extensions);
            var context = Assert.IsType<StorageContext>(Assert.Single(host.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                new Dictionary<string, object> { ["StorageAccountName"] = Account })));
            body(host, scope, context, live);
            recording.Save();
        }, () =>
        {
            if (live) DeleteResources();
        });
    }

    /// <summary>
    /// Records the CLI body and optional PowerShell readbacks against fresh resources, or replays
    /// that flow followed by the independent PowerShell body.
    /// </summary>
    private static void Run(string test, string[] containers, Action<ScenarioHost, Scope, bool> cli,
        Action<ScenarioHost, Scope, StorageContext> ps, IRequestMatcher? matcher = null,
        Action<ScenarioHost, Scope, StorageContext>? downloads = null, Action<IReadOnlyList<CapturedHttpRequest>>? assertRequests = null)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneScenarioTests), test, matcher ?? new BlockIdRequestMatcher());
        void RunPrimary(bool live)
        {
            using var scope = new Scope();
            var interceptor = recording.CreateInterceptor();
            using (var host = new ScenarioHost(ClientType.CLI, interceptor, useLiveAuth: live, extensions: s_extensions))
                cli(host, scope, live);
            if (downloads is not null)
            {
                using var host = new ScenarioHost(ClientType.PowerShell, interceptor, useLiveAuth: live, extensions: s_extensions);
                var context = Assert.IsType<StorageContext>(Assert.Single(host.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                    new Dictionary<string, object> { ["StorageAccountName"] = Account })));
                downloads(host, scope, context);
            }
            assertRequests?.Invoke(interceptor.Sent);
        }
        if (recording.Mode == RecorderMode.Record)
        {
            RunWithCleanup(() =>
            {
                CreateResources(containers);
                RunPrimary(true);
                recording.Save();
            }, DeleteResources);
            return;
        }

        MakePlayable(recording.Record);
        RunPrimary(false);
        using (var scope = new Scope())
        using (var host = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            var context = Assert.IsType<StorageContext>(Assert.Single(host.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                new Dictionary<string, object> { ["StorageAccountName"] = Account })));
            ps(host, scope, context);
        }
    }

    /// <summary>
    /// Creates the run-scoped group, an account that allows shared keys (the key scenarios sign
    /// with them), the share and the containers. The setup host records into a throwaway record, so
    /// none of it reaches the scenario recording.
    /// </summary>
    private static void CreateResources(string[] containers)
    {
        using var host = new ScenarioHost(ClientType.CLI, SetupInterceptor(), useLiveAuth: true, extensions: s_setupExtensions);
        string subscriptionId = host.LiveSubscriptionId();
        host.RunCli("group", "create", "--subscription-id", subscriptionId, "--name", ResourceGroup, "--location", Location);
        host.RunCli("storage", "account", "create", "--subscription-id", subscriptionId, "-g", ResourceGroup, "-n", Account,
            "-l", Location, "--kind", "StorageV2", "--sku", "Standard_LRS", "--min-tls-version", "TLS1_2", "--https-only", "true",
            "--allow-shared-key-access", "true");
        StorageBlobLiveSetup.CreateShare(subscriptionId, ResourceGroup, Account, Share);
        foreach (string container in containers) StorageBlobLiveSetup.EnsureContainer(Account, container);
    }

    /// <summary>A live interceptor over a throwaway record that is never saved.</summary>
    private static RecordPlaybackInterceptor SetupInterceptor() => new(RecorderMode.Record, new SessionRecord());

    /// <summary>Deletes the run-scoped group and the account in it.</summary>
    private static void DeleteResources()
    {
        using var host = new ScenarioHost(ClientType.CLI, SetupInterceptor(), useLiveAuth: true, extensions: s_setupExtensions);
        host.RunCli("group", "delete", "--subscription-id", host.LiveSubscriptionId(), "--name", ResourceGroup);
    }

    private static void RunWithCleanup(Action workflow, Action cleanup)
    {
        try
        {
            workflow();
        }
        catch (Exception workflowException)
        {
            try
            {
                cleanup();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("The Storage scenario failed and cleanup also failed.", workflowException, cleanupException);
            }
            throw;
        }
        cleanup();
    }

    [Fact]
    public void BlockPlaybackMatcherChecksPayloadWhileNormalizingRandomIds()
    {
        var recorded = new RecordedRequest
        {
            Method = "PUT", Uri = "https://account.blob.core.windows.net/container/blob?comp=block&blockid=old",
            BodyBase64 = Convert.ToBase64String(s_payload),
        };
        var outgoing = new CapturedHttpRequest("PUT",
            "https://account.blob.core.windows.net/container/blob?comp=block&blockid=new", new Dictionary<string, string[]>(), null)
        {
            BinaryBody = s_payload.ToArray(),
        };
        var matcher = new BlockIdRequestMatcher();
        Assert.True(matcher.Matches(recorded, outgoing));
        Assert.False(matcher.Matches(recorded, outgoing with { BinaryBody = [9] }));
    }

    [Theory]
    [InlineData("Range")]
    [InlineData("x-ms-range")]
    public void BlobPlaybackMatcherDistinguishesConcurrentRanges(string header)
    {
        const string uri = "https://account.blob.core.windows.net/container/blob";
        var recorded = new RecordedRequest
        {
            Method = "GET", Uri = uri, Headers = new Dictionary<string, string[]> { ["Range"] = ["bytes=0-9"] },
        };
        var outgoing = new CapturedHttpRequest("GET", uri, new Dictionary<string, string[]> { [header] = ["bytes=0-9"] }, null);
        var matcher = new BlockIdRequestMatcher();
        Assert.True(matcher.Matches(recorded, outgoing));
        Assert.False(matcher.Matches(recorded, outgoing with { Headers = new Dictionary<string, string[]> { [header] = ["bytes=10-19"] } }));
    }

    [Fact]
    public void CleanupRunsAfterSuccessfulWorkflow()
    {
        var calls = new List<string>();
        RunWithCleanup(() => calls.Add("workflow"), () => calls.Add("cleanup"));
        Assert.Equal(["workflow", "cleanup"], calls);
    }

    [Fact]
    public void CleanupFailureAfterSuccessIsReported()
    {
        var failure = new InvalidOperationException("cleanup failed");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => RunWithCleanup(() => { }, () => throw failure)));
    }

    [Fact]
    public void WorkflowFailureIsPreservedAfterCleanup()
    {
        var failure = new InvalidOperationException("workflow failed");
        bool cleaned = false;
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => RunWithCleanup(() => throw failure, () => cleaned = true)));
        Assert.True(cleaned);
    }

    [Fact]
    public void WorkflowAndCleanupFailuresAreBothReported()
    {
        var workflow = new InvalidOperationException("workflow failed");
        var cleanup = new InvalidOperationException("cleanup failed");
        var failures = Assert.Throws<AggregateException>(() => RunWithCleanup(() => throw workflow, () => throw cleanup));
        Assert.Equal([workflow, cleanup], failures.InnerExceptions);
    }

    /// <summary>
    /// The product checks that the user delegation key's validity window covers the current time,
    /// so playback moves the recorded window around now. Everything else replays as recorded.
    /// </summary>
    private static void MakePlayable(SessionRecord record)
    {
        for (int i = 0; i < record.Entries.Count; i++)
        {
            var entry = record.Entries[i];
            if (entry.Response.Body is not { } body ||
                !entry.Request.Uri.Contains("comp=userdelegationkey", StringComparison.OrdinalIgnoreCase)) continue;
            var now = DateTimeOffset.UtcNow;
            body = Regex.Replace(body, "<SignedStart>[^<]*</SignedStart>", $"<SignedStart>{StorageTime(now.AddHours(-1))}</SignedStart>");
            body = Regex.Replace(body, "<SignedExpiry>[^<]*</SignedExpiry>", $"<SignedExpiry>{StorageTime(now.AddDays(2))}</SignedExpiry>");
            record.Entries[i] = new SessionRecordEntry
            {
                Request = entry.Request,
                Response = new RecordedResponse
                {
                    StatusCode = entry.Response.StatusCode,
                    Headers = entry.Response.Headers,
                    ContentHeaders = entry.Response.ContentHeaders,
                    Body = body,
                },
            };
        }
    }

    private static string StorageTime(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static StorageBlob AssertDownloaded(ScenarioHost ps, Dictionary<string, object> parameters, string name, string path,
        byte[] expected, IEnumerable<object>? pipelineInput = null)
    {
        var downloaded = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobContentCmdlet>(parameters, pipelineInput)));
        Assert.Equal(name, downloaded.Name);
        Assert.Equal(expected.Length, downloaded.Properties.ContentLength);
        Assert.Equal(expected, File.ReadAllBytes(path));
        return downloaded;
    }

    private static byte[] Payload(CapturedHttpRequest request) => request.BinaryBody ?? Encoding.UTF8.GetBytes(request.Body ?? string.Empty);

#pragma warning disable CA5351 // Azure Storage requires MD5 as a transport checksum, not a security primitive.
    private static string ContentMd5(byte[] bytes) => Convert.ToBase64String(MD5.HashData(bytes));
#pragma warning restore CA5351

    private static System.Collections.Specialized.NameValueCollection Query(CapturedHttpRequest request) => HttpUtility.ParseQueryString(new Uri(request.Uri).Query);

    private static CapturedHttpRequest SingleRequest(IEnumerable<CapturedHttpRequest> requests, Func<CapturedHttpRequest, bool> predicate)
    {
        var matches = requests.Where(predicate).ToArray();
        Assert.True(matches.Length == 1, $"Expected one matching Storage request, got {matches.Length}.");
        return matches[0];
    }

    private static string Header(CapturedHttpRequest request, string name)
    {
        var values = request.Headers.FirstOrDefault(header => header.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
        Assert.NotNull(values);
        Assert.True(values.Length == 1, $"Expected one {name} request header.");
        return values[0];
    }

    private static void AssertHeader(CapturedHttpRequest request, string name, string value) => Assert.Equal(value, Header(request, name));

    private static string[] Blob(string verb, string container) =>
        ["storage", "blob", verb, "--account-name", Account, "--container-name", container, "--auth-mode", "login"];

    private static string[] Batch(string container, string destination) =>
        ["storage", "blob", "download-batch", "--account-name", Account, "--source", container, "--destination", destination, "--auth-mode", "login"];

    private static string[] Files(string command) =>
        ["storage", .. command.Split(' '), "--account-name", Account, "--share-name", Share, "--enable-file-backup-request-intent", "true"];

    private static JsonElement Show(ScenarioHost host, string container, string name, params string[] extra) =>
        Json(Assert.Single(host.RunCli([.. Blob("show", container), "--name", name, .. extra])));

    private static List<JsonElement> List(ScenarioHost host, string container, params string[] extra) =>
        [.. Json(Assert.Single(host.RunCli([.. Blob("list", container), .. extra]))).EnumerateArray()];

    private static bool Exists(ScenarioHost host, string container, string name) =>
        Assert.IsType<StorageBlobExistence>(Assert.Single(host.RunCli([.. Blob("exists", container), "--name", name]))).Exists;

    private static string Subscription(ScenarioHost host, bool live) => live ? host.LiveSubscriptionId() : PlaybackSubscriptionId;

    private static void Copy(ScenarioHost host, string container, string name, params string[] source)
        => Copy(host, Account, container, name, source);

    private static void Copy(ScenarioHost host, string account, string container, string name, string[] source)
    {
        var started = Json(Assert.Single(host.RunCli(["storage", "blob", "copy", "start", "--account-name", account,
            "--destination-container", container, "--destination-blob", name, .. source, "--auth-mode", "login"])));
        Assert.True(Guid.TryParse(started.GetProperty("copyId").GetString(), out _));
        Assert.Contains(started.GetProperty("copyStatus").GetString(), new[] { "pending", "success" });
    }

    private static void AssertCopyCompleted(ScenarioHost host, string container, string name, long expectedLength)
    {
        for (int attempt = 0; ; attempt++)
        {
            var properties = Show(host, container, name).GetProperty("properties");
            var copy = properties.GetProperty("copy");
            string? status = copy.GetProperty("status").GetString();
            if (status == "success")
            {
                Assert.DoesNotContain("sig=", copy.GetProperty("source").GetString());
                Assert.Equal(expectedLength, properties.GetProperty("contentLength").GetInt64());
                break;
            }
            Assert.Equal("pending", status);
            Assert.True(attempt < 30, "The copy did not finish.");
            Thread.Sleep(1000);
        }
    }

    private static void AssertFails(ScenarioHost host, string message, string[] args)
    {
        var error = Assert.ThrowsAny<Exception>(() => host.RunCli(args));
        Assert.Contains(message, error.Message);
    }

    private static void AssertServiceError(ScenarioHost host, string code, string[] args)
    {
        var error = Assert.ThrowsAny<HttpRequestException>(() => host.RunCli(args));
        Assert.Equal(code, error.Data["ServiceErrorCode"]);
    }

    private static JsonElement Json(object output) => output switch
    {
        JsonElement element => element,
        JsonElement[] elements => JsonSerializer.SerializeToElement(elements),
        _ => JsonSerializer.SerializeToElement(output),
    };

    private static byte[] Bytes(int length) => [.. Enumerable.Range(0, length).Select(index => (byte)(index * 31 + 7))];

    /// <summary>A temporary directory for local source and destination files.</summary>
    private sealed class Scope : IDisposable
    {
        private readonly string _root = System.IO.Directory.CreateTempSubdirectory("azclips-storage-").FullName;

        public string PathOf(string name) => Path.Combine(_root, name);

        public string Write(string name, byte[] bytes)
        {
            string path = PathOf(name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public string Directory(string name) => System.IO.Directory.CreateDirectory(PathOf(name)).FullName;

        public void Dispose() => System.IO.Directory.Delete(_root, recursive: true);
    }
}
