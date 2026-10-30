using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Core.Abstractions;
using Microsoft.Azure.Azclips.Cmdlets.Storage;
using Microsoft.Azure.Azclips.Extensions.Storage.Customization;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// The Storage data-plane commands. Record mode creates a run-scoped resource group and account,
/// runs the CLI surface against them with the signed-in profile, and deletes the group at the end;
/// playback replays that one recording on the CLI and then on PowerShell, without credentials.
/// Each scenario uses its own container so the recorded URIs stay stable.
/// </summary>
[Collection("Scenario Tests")]
public class StorageDataPlaneScenarioTests
{
    private const string Share = "scenario-files";
    private const string Location = "westus2";

    // Record mode suffixes both names with the run id; the sanitisers strip it again, so the
    // recordings and playback keep the base names. Account names allow no hyphen, hence "t".
    private static readonly RecorderMode s_mode = RecorderModeResolver.FromEnvironment();
    private static readonly string ResourceGroup = TestRun.ScopedName("scenario-storage-dp-rg", s_mode);
    private static readonly string Account = s_mode == RecorderMode.Record ? $"azclipsdpt{TestRun.RunId}" : "azclipsdp";

    // The product rejects the all-zero placeholder subscription; the sanitisers map this one back to it.
    private const string PlaybackSubscriptionId = "22222222-2222-2222-2222-222222222222";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Storage];
    private static readonly ExtensionUnderTest[] s_setupExtensions = [ExtensionUnderTest.Resources, ExtensionUnderTest.Storage];
    private static readonly byte[] s_payload = [0x00, 0xFF, 0x80, 0x7F, 0x0A, 0x0D, 0xC3, 0x28, 0x41, 0x42];

    [Fact]
    public void BlobUploadDownload()
    {
        const string container = "scenario-blob-transfer";
        Run(nameof(BlobUploadDownload), [container], (host, scope, _) =>
        {
            byte[] block = Bytes(2500);
            byte[] page = Bytes(1536);
            string blockFile = scope.Write("block.bin", block);
            string pageFile = scope.Write("page.bin", page);
            string empty = scope.Write("empty.bin", []);

            // Block: three 1000-byte chunks on one worker, with every content setting.
            host.RunCli([.. Blob("upload", container), "--file", blockFile, "--name", "block.bin", "--chunk-size", "1000",
                "--max-connections", "1", "--validate-content", "--content-type", "application/x-scenario", "--content-encoding", "identity",
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

            string blockOut = scope.PathOf("block.out");
            host.RunCli([.. Blob("download", container), "--name", "block.bin", "--file", blockOut, "--chunk-size", "1000", "--max-connections", "1"]);
            Assert.Equal(block, File.ReadAllBytes(blockOut));
            string pageOut = scope.PathOf("page.out");
            host.RunCli([.. Blob("download", container), "--name", "page.bin", "--file", pageOut, "--max-connections", "1"]);
            Assert.Equal(page, File.ReadAllBytes(pageOut));
            string emptyOut = scope.PathOf("empty.out");
            var emptyShown = Json(Assert.Single(host.RunCli([.. Blob("download", container), "--name", "empty.bin", "--file", emptyOut])));
            Assert.Equal(0, emptyShown.GetProperty("properties").GetProperty("contentLength").GetInt64());
            Assert.Empty(File.ReadAllBytes(emptyOut));

            // Ranges are inclusive and pass --check-md5; an end past the blob is clamped to the last byte.
            string range = scope.PathOf("range.bin");
            foreach (var (start, end, expected) in new (string, string?, byte[])[] { ("3", "7", block[3..8]), ("2490", "9999", block[2490..]), ("2490", null, block[2490..]) })
            {
                string[] bounds = end is null ? ["--start-range", start] : ["--start-range", start, "--end-range", end];
                var ranged = Json(Assert.Single(host.RunCli([.. Blob("download", container), "--name", "block.bin", "--file", range,
                    "--check-md5", "true", "--chunk-size", "2", .. bounds])));
                Assert.Equal(expected, File.ReadAllBytes(range));
                Assert.Equal(block.Length, ranged.GetProperty("properties").GetProperty("contentLength").GetInt64());
            }

            // Without --file the blob streams to stdout as binary chunks.
            var chunks = host.RunCli([.. Blob("download", container), "--name", "log.txt"]);
            Assert.Equal("abc"u8.ToArray(), chunks.Cast<BinaryOutputChunk>().SelectMany(item => item.Bytes.ToArray()).ToArray());


            // An existing destination is replaced by default, and the output carries no native context.
            File.WriteAllText(blockOut, "old data that must be replaced");
            var replaced = Json(Assert.Single(host.RunCli([.. Blob("download", container), "--name", "block.bin", "--file", blockOut, "--max-connections", "1"])));
            Assert.Equal(block, File.ReadAllBytes(blockOut));
            Assert.Equal("block.bin", replaced.GetProperty("name").GetString());
            Assert.False(replaced.TryGetProperty("context", out JsonElement _));

            // An empty page blob has zero capacity.
            host.RunCli([.. Blob("upload", container), "--file", empty, "--name", "empty-page.bin", "--type", "page"]);
            var emptyPage = Show(host, container, "empty-page.bin").GetProperty("properties");
            Assert.Equal("PageBlob", emptyPage.GetProperty("blobType").GetString());
            Assert.Equal(0, emptyPage.GetProperty("contentLength").GetInt64());

            // Without --name the blob takes the file name.
            string named = scope.Write("from-file-name.bin", s_payload);
            host.RunCli([.. Blob("upload", container), "--file", named]);
            Assert.Equal(s_payload.Length, Show(host, container, "from-file-name.bin").GetProperty("properties").GetProperty("contentLength").GetInt64());

            // Special names: empty segments and a trailing dot round-trip; dot-only segments are rejected before any request.
            foreach (string name in new[] { "nested//f", "trailing." })
            {
                host.RunCli([.. Blob("upload", container), "--file", named, "--name", name]);
                string target = scope.PathOf("special.out");
                var special = Json(Assert.Single(host.RunCli([.. Blob("download", container), "--name", name, "--file", target])));
                Assert.Equal(name, special.GetProperty("name").GetString());
                Assert.Equal(s_payload, File.ReadAllBytes(target));
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

            string target = scope.PathOf("page.out");
            var downloaded = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobContentCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "page.bin", ["Destination"] = target, ["Context"] = context,
                ["ConcurrentTaskCount"] = 1, ["Force"] = true,
            })));
            Assert.Equal("PageBlob", downloaded.Properties.BlobType);
            Assert.Equal(Bytes(1536), File.ReadAllBytes(target));

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
        new BlockIdRequestMatcher());
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

            AssertFails(host, "Parameter 'Tier' value 'Bogus' is not allowed.", [.. Blob("set-tier", container), "--name", "t.bin", "--tier", "Bogus"]);
            AssertFails(host, "Parameter 'RehydratePriority' value 'Middle' is not allowed.",
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
            var names = Assert.Single(host.RunCli([.. Batch(container, batch), "--pattern", "nested/*", "--max-connections", "1", "--no-progress"]));
            Assert.Equal(["nested/a.bin", "nested/b.bin"], Assert.IsType<string[]>(names));
            Assert.Equal(content, File.ReadAllBytes(Path.Combine(batch, "nested", "a.bin")));
            Assert.False(File.Exists(Path.Combine(batch, "top.bin")));
            Assert.Empty(Assert.IsType<string[]>(Assert.Single(host.RunCli([.. Batch(container, batch), "--pattern", "nomatch*"]))));

            // A second run into the same directory conflicts unless overwrite is set.
            AssertFails(host, "A batch destination file already exists; specify overwrite or choose another directory.",
                [.. Batch(container, batch), "--pattern", "nested/*"]);
            var again = Assert.Single(host.RunCli([.. Batch(container, batch), "--pattern", "nested/*", "--overwrite", "true", "--max-connections", "1"]));
            Assert.Equal(2, Assert.IsType<string[]>(again).Length);

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
            AssertCopied(host, scope, target, "from-login.txt", blobContent);

            // Explicit source SAS, signed here with key1.
            string key = Keys(host, live).Single(k => k.KeyName == "key1").Value;
            Copy(host, target, "from-sas.txt", "--source-container", source, "--source-blob", "src.txt",
                "--source-sas", BlobReadSas(key, source, "src.txt"));
            AssertCopied(host, scope, target, "from-sas.txt", blobContent);

            // Azure Files source: the account key comes from an explicit ARM lookup and signs a file SAS.
            Copy(host, target, "from-file.txt", "--source-share", Share, "--source-path", sourceFile,
                "--use-source-account-key-lookup", "true", "--source-key-resource-group", ResourceGroup,
                "--source-key-subscription-id", Subscription(host, live));
            AssertCopied(host, scope, target, "from-file.txt", fileContent);

            // A source URI with the SAS embedded or given separately; a special source name; a requested tier.
            string sourceUri = $"https://{Account}.blob.core.windows.net/{source}/src.txt";
            Copy(host, target, "from-uri.txt", "--source-uri", sourceUri + "?" + BlobReadSas(key, source, "src.txt"));
            AssertCopied(host, scope, target, "from-uri.txt", blobContent);
            Copy(host, target, "from-uri-sas.txt", "--source-uri", sourceUri, "--source-sas", BlobReadSas(key, source, "src.txt"));
            AssertCopied(host, scope, target, "from-uri-sas.txt", blobContent);
            const string special = "dir/sp ace%2F+.txt";
            host.RunCli([.. Blob("upload", source), "--file", scope.Write("special.txt", blobContent), "--name", special]);
            Copy(host, target, "from-special.txt", "--source-container", source, "--source-blob", special,
                "--source-sas", BlobReadSas(key, source, special));
            AssertCopied(host, scope, target, "from-special.txt", blobContent);
            Copy(host, target, "cool.txt", "--source-container", source, "--source-blob", "src.txt", "--tier", "Cool");
            AssertCopied(host, scope, target, "cool.txt", blobContent);
            Assert.Equal("Cool", Show(host, target, "cool.txt").GetProperty("properties").GetProperty("blobTier").GetString());

            // A supplied Files account key signs the source without an ARM lookup.
            Copy(host, target, "from-file-key.txt", "--source-share", Share, "--source-path", sourceFile, "--source-account-key", key);
            AssertCopied(host, scope, target, "from-file-key.txt", fileContent);

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
        });
    }

    [Fact]
    public void FileAndDirectory()
    {
        const string directory = "scenario-dir";
        Run(nameof(FileAndDirectory), [], (host, scope, _) =>
        {
            var created = Json(Assert.Single(host.RunCli([.. Files("directory create"), "--name", directory])));
            Assert.True(created.GetProperty("created").GetBoolean());
            var again = Json(Assert.Single(host.RunCli([.. Files("directory create"), "--name", directory])));
            Assert.False(again.GetProperty("created").GetBoolean());
            host.RunCli([.. Files("directory create"), "--name", $"{directory}/sub"]);

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
            // Playback matches requests in recorded order, so the second whole-file download the PowerShell
            // part sends must be recorded before any ranged download of the same file.
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

            // fail-on-exist turns an existing directory into the service error; a missing parent or share is never created.
            AssertServiceError(host, "ResourceAlreadyExists", [.. Files("directory create"), "--name", directory, "--fail-on-exist", "true"]);
            AssertServiceError(host, "ParentNotFound", [.. Files("directory create"), "--name", $"{directory}/missing/new"]);
            AssertServiceError(host, "ShareNotFound",
                ["storage", "directory", "create", "--account-name", Account, "--share-name", "scenario-noshare", "--enable-file-backup-request-intent", "true", "--name", "d"]);
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
            Assert.Equal("literal%2F+name", Assert.Single(literal.EnumerateArray()).GetProperty("name").GetString());

            // A path naming an existing directory takes the source file name.
            host.RunCli([.. Files("file upload"), "--source", scope.Write("into.bin", s_payload), "--path", $"{directory}/sub"]);
            var into = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", $"{directory}/sub"])));
            Assert.Equal(["into.bin", "literal%2F+name"], into.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Order());

            // Missing parent directories are created; trailing dots are kept unless --disallow-trailing-dot is given.
            string[] backupIntent = ["storage", "file", "upload", "--account-name", Account, "--share-name", Share, "--backup-intent", "true"];
            host.RunCli([.. backupIntent, "--source", scope.Write("t.bin", s_payload), "--path", $"{directory}/new/deep/trail."]);
            host.RunCli([.. backupIntent, "--source", scope.Write("d.bin", s_payload), "--path", $"{directory}/new/deep/dot.", "--disallow-trailing-dot", "true"]);
            var deep = Json(Assert.Single(host.RunCli([.. Files("file list"), "--path", $"{directory}/new/deep"])));
            Assert.Equal(["dot", "trail."], deep.EnumerateArray().Select(entry => entry.GetProperty("name").GetString()).Order());

            // The PowerShell part replays these two uploads. Its upload stores the whole-file MD5, so --content-md5
            // records the same properties request, and the download records the properties read of its PassThru.
            string md5 = Convert.ToBase64String(MD5.HashData(s_payload));
            string psSource = scope.Write("ps.bin", s_payload);
            foreach (string path in new[] { $"{directory}/sub", $"{directory}/ps/deep/p.bin" })
                host.RunCli([.. Files("file upload"), "--source", psSource, "--path", path, "--content-md5", md5]);
            foreach (string path in new[] { $"{directory}/sub/ps.bin", $"{directory}/ps/deep/p.bin" })
            {
                string copy = scope.PathOf(Path.GetFileName(path) + ".out");
                host.RunCli([.. Files("file download"), "--path", path, "--dest", copy]);
                Assert.Equal(s_payload, File.ReadAllBytes(copy));
            }
        },
        (ps, scope, _) =>
        {
            var context = Assert.IsType<StorageContext>(Assert.Single(ps.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                new Dictionary<string, object> { ["StorageAccountName"] = Account, ["EnableFileBackupRequestIntent"] = true })));
            string target = scope.PathOf(Path.Combine("missing", "f.out"));
            ps.RunPowerShellCmdlet<GetAzStorageFileContentCmdlet>(new Dictionary<string, object>
            {
                ["ShareName"] = Share, ["Path"] = $"{directory}/f.bin", ["Destination"] = target, ["Context"] = context,
                ["ConcurrentTaskCount"] = 1, ["Force"] = true,
            });
            Assert.Equal(Bytes(1200), File.ReadAllBytes(target));

            // A file from the pipeline downloads into an existing directory under its own name.
            string folder = scope.Directory("piped");
            var piped = Assert.IsType<StorageFile>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageFileContentCmdlet>(new Dictionary<string, object>
            {
                ["Destination"] = folder, ["Force"] = true, ["PassThru"] = true, ["ConcurrentTaskCount"] = 1, ["Confirm"] = false,
            }, [new StorageFile($"{directory}/f.bin", new StorageFileShare(Share, context))])));
            Assert.Equal($"{directory}/f.bin", piped.Path);
            Assert.Equal("v", piped.Properties.Metadata["k"]);
            Assert.Equal(Bytes(1200), File.ReadAllBytes(Path.Combine(folder, "f.bin")));

            // An upload to an existing directory takes the source file name; missing parent directories are created.
            string psSource = scope.Write("ps.bin", s_payload);
            foreach (var (path, expected) in new[] { ($"{directory}/sub", $"{directory}/sub/ps.bin"), ($"{directory}/ps/deep/p.bin", $"{directory}/ps/deep/p.bin") })
            {
                var uploaded = Assert.IsType<StorageFile>(Assert.Single(ps.RunPowerShellCmdlet<SetAzStorageFileContentCmdlet>(new Dictionary<string, object>
                {
                    ["ShareName"] = Share, ["Source"] = psSource, ["Path"] = path, ["Context"] = context,
                    ["Force"] = true, ["PassThru"] = true, ["ConcurrentTaskCount"] = 1, ["Confirm"] = false,
                })));
                Assert.Equal(expected, uploaded.Path);
                Assert.Equal(s_payload.Length, uploaded.Properties.ContentLength);
            }
        });
    }

    [Fact]
    public void AccountKeysList()
    {
        Run(nameof(AccountKeysList), [], (host, _, live) =>
        {
            var keys = Keys(host, live);
            Assert.Equal(["key1", "key2"], keys.Select(key => key.KeyName));
            Assert.All(keys, key => Assert.Equal("Full", key.Permissions, ignoreCase: true));
            if (!live) Assert.All(keys, key => Assert.Equal(RecordingSanitizers.FakeStorageKey, key.Value));

            AssertFails(host, "Storage account names must contain 3-24 lowercase letters or digits.",
                ["storage", "account", "keys", "list", "-g", ResourceGroup, "-n", "Bad_Name", "--subscription-id", Subscription(host, live)]);
        },
        (ps, _, _) =>
        {
            var keys = ps.RunPowerShellCmdlet<GetAzStorageAccountKeyCmdlet>(new Dictionary<string, object>
            {
                ["ResourceGroupName"] = ResourceGroup, ["Name"] = Account, ["SubscriptionId"] = PlaybackSubscriptionId,
            }).Cast<StorageAccountKey>().ToList();
            Assert.Equal(["key1", "key2"], keys.Select(key => key.KeyName));
            Assert.All(keys, key => Assert.Equal(RecordingSanitizers.FakeStorageKey, key.Value));
        },
        assertRecording: record => Assert.All(
            record.Entries.Where(entry => entry.Request.Uri.Contains("/listKeys", StringComparison.OrdinalIgnoreCase)),
            entry => Assert.Equal(2, Regex.Matches(entry.Response.Body ?? "", $"\"value\":\"{Regex.Escape(RecordingSanitizers.FakeStorageKey)}\"").Count)));
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
            AssertServiceError(host, "BlobNotFound", [.. Blob("download", container), "--name", "missing.txt", "--file", scope.PathOf("m.bin")]);
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
            string existing = scope.Write("existing.bin", [1]);
            AssertFails(host, "The destination already exists; specify overwrite or choose another file.",
                [.. Blob("download", container), "--name", "e.txt", "--file", existing, "--overwrite", "false"]);
            AssertFails(host, "Use nonnegative inclusive range bounds; an end requires a start and cannot precede it.",
                [.. Blob("download", container), "--name", "e.txt", "--file", scope.PathOf("r.bin"), "--start-range", "7", "--end-range", "3"]);
            AssertFails(host, "Use nonnegative inclusive range bounds; an end requires a start and cannot precede it.",
                [.. Blob("download", container), "--name", "e.txt", "--file", scope.PathOf("r.bin"), "--end-range", "100"]);
            string unaligned = scope.Write("odd.bin", new byte[1000]);
            AssertFails(host, "Page Blob source length must be a multiple of 512 bytes and at most 8 TiB.",
                [.. Blob("upload", container), "--file", unaligned, "--type", "page"]);
            AssertFails(host, "Container/share names must contain 3-63 lowercase letters, digits or nonconsecutive internal hyphens.",
                [.. Blob("upload", "Bad_Name"), "--file", source]);
            AssertFails(host, "Resource paths must be nonempty and cannot contain dot-only path segments.",
                [.. Blob("show", container), "--name", "a/../b"]);
            AssertFails(host, "Parameter 'AuthMode' value 'key' is not allowed.",
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
        try
        {
            if (live) CreateResources([container]);
            using var scope = new Scope();
            using var host = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), useLiveAuth: live, extensions: s_extensions);
            var context = Assert.IsType<StorageContext>(Assert.Single(host.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                new Dictionary<string, object> { ["StorageAccountName"] = Account })));
            body(host, scope, context, live);
            recording.Save();
        }
        finally
        {
            if (live) DeleteResources();
        }
    }

    /// <summary>
    /// Records the CLI body against fresh resources from <see cref="CreateResources"/>, or replays
    /// the one recording on the CLI body and then on the PowerShell body.
    /// </summary>
    private static void Run(string test, string[] containers, Action<ScenarioHost, Scope, bool> cli,
        Action<ScenarioHost, Scope, StorageContext> ps, IRequestMatcher? matcher = null, Action<SessionRecord>? assertRecording = null)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneScenarioTests), test, matcher);
        if (recording.Mode == RecorderMode.Record)
        {
            try
            {
                CreateResources(containers);
                using var scope = new Scope();
                using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);
                cli(host, scope, true);
                recording.Save();
            }
            finally
            {
                DeleteResources();
            }
            return;
        }

        assertRecording?.Invoke(recording.Record);
        MakePlayable(recording.Record);
        using (var scope = new Scope())
        using (var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            cli(host, scope, false);
        }
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
        try { host.RunCli("group", "delete", "--subscription-id", host.LiveSubscriptionId(), "--name", ResourceGroup); } catch { }
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

    private static StorageAccountKey[] Keys(ScenarioHost host, bool live) =>
        Assert.IsType<StorageAccountKey[]>(Assert.Single(host.RunCli("storage", "account", "keys", "list", "-g", ResourceGroup, "-n", Account,
            "--subscription-id", Subscription(host, live))));

    private static void Copy(ScenarioHost host, string container, string name, params string[] source)
    {
        var started = Json(Assert.Single(host.RunCli(["storage", "blob", "copy", "start", "--account-name", Account,
            "--destination-container", container, "--destination-blob", name, .. source, "--auth-mode", "login"])));
        Assert.True(Guid.TryParse(started.GetProperty("copyId").GetString(), out _));
        Assert.Contains(started.GetProperty("copyStatus").GetString(), new[] { "pending", "success" });
    }

    /// <summary>Polls show until the copy succeeds, then checks the copied bytes.</summary>
    private static void AssertCopied(ScenarioHost host, Scope scope, string container, string name, byte[] expected)
    {
        for (int attempt = 0; ; attempt++)
        {
            var copy = Show(host, container, name).GetProperty("properties").GetProperty("copy");
            string? status = copy.GetProperty("status").GetString();
            if (status == "success")
            {
                Assert.DoesNotContain("sig=", copy.GetProperty("source").GetString());
                break;
            }
            Assert.Equal("pending", status);
            Assert.True(attempt < 30, "The copy did not finish.");
            Thread.Sleep(1000);
        }
        string target = scope.PathOf(name);
        host.RunCli([.. Blob("download", container), "--name", name, "--file", target]);
        Assert.Equal(expected, File.ReadAllBytes(target));
    }

    /// <summary>A read-only blob service SAS (version 2022-11-02) signed with an account key.</summary>
    private static string BlobReadSas(string accountKey, string container, string blob)
    {
        string start = StorageTime(DateTimeOffset.UtcNow.AddMinutes(-5)), expiry = StorageTime(DateTimeOffset.UtcNow.AddHours(1));
        const string version = "2022-11-02";
        string[] fields = ["r", start, expiry, $"/blob/{Account}/{container}/{blob}", "", "", "https", version, "b", "", "", "", "", "", "", ""];
        string signature = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(accountKey), Encoding.UTF8.GetBytes(string.Join("\n", fields))));
        return $"sp=r&st={start}&se={expiry}&spr=https&sv={version}&sr=b&sig={Uri.EscapeDataString(signature)}";
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
