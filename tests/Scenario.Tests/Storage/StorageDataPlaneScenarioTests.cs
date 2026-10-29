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
/// The Storage data-plane commands. Record mode runs the CLI surface against the live account with
/// the signed-in profile; playback replays that one recording on the CLI and then on PowerShell,
/// without credentials. Each scenario uses its own container so the recorded URIs stay stable.
/// </summary>
[Collection("Scenario Tests")]
public class StorageDataPlaneScenarioTests
{
    private const string Account = "azclipsdpr58392";
    private const string ResourceGroup = "azclips-storage-dataplane-rec";
    private const string Share = "scenario-files";

    // The product rejects the all-zero placeholder subscription; the sanitisers map this one back to it.
    private const string PlaybackSubscriptionId = "22222222-2222-2222-2222-222222222222";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Storage];
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

            // Ranges are inclusive; an end past the blob is clamped to the last byte.
            string range = scope.PathOf("range.bin");
            host.RunCli([.. Blob("download", container), "--name", "block.bin", "--file", range, "--start-range", "3", "--end-range", "7"]);
            Assert.Equal(block[3..8], File.ReadAllBytes(range));
            host.RunCli([.. Blob("download", container), "--name", "block.bin", "--file", range, "--start-range", "2490", "--end-range", "9999"]);
            Assert.Equal(block[2490..], File.ReadAllBytes(range));

            // Without --file the blob streams to stdout as binary chunks.
            var chunks = host.RunCli([.. Blob("download", container), "--name", "log.txt"]);
            Assert.Equal("abc"u8.ToArray(), chunks.Cast<BinaryOutputChunk>().SelectMany(item => item.Bytes.ToArray()).ToArray());
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
        },
        (ps, _, context) =>
        {
            var found = Assert.IsType<StorageBlob>(Assert.Single(ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "c.bin", ["Context"] = context,
            })));
            Assert.Equal(s_payload.Length, found.Properties.ContentLength);
            Assert.Null(found.Snapshot);
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

            string range = scope.PathOf("f.range");
            host.RunCli([.. Files("file download"), "--path", $"{directory}/f.bin", "--dest", range, "--start-range", "100", "--end-range", "199"]);
            Assert.Equal(content[100..200], File.ReadAllBytes(range));

            // Files OAuth needs the explicit backup intent; nothing is sent without it.
            AssertFails(host, "Azure Files OAuth requires explicit EnableFileBackupRequestIntent.",
                ["storage", "file", "list", "--account-name", Account, "--share-name", Share, "--path", directory]);
        },
        (ps, scope, _) =>
        {
            var context = Assert.IsType<StorageContext>(Assert.Single(ps.RunPowerShellCmdlet<NewAzStorageContextCmdlet>(
                new Dictionary<string, object> { ["StorageAccountName"] = Account, ["EnableFileBackupRequestIntent"] = true })));
            string target = scope.PathOf("f.out");
            ps.RunPowerShellCmdlet<GetAzStorageFileContentCmdlet>(new Dictionary<string, object>
            {
                ["ShareName"] = Share, ["Path"] = $"{directory}/f.bin", ["Destination"] = target, ["Context"] = context,
                ["ConcurrentTaskCount"] = 1, ["Force"] = true,
            });
            Assert.Equal(Bytes(1200), File.ReadAllBytes(target));
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
        (ps, _, context) =>
        {
            var missing = Assert.ThrowsAny<Exception>(() => ps.RunPowerShellCmdlet<GetAzStorageBlobCmdlet>(new Dictionary<string, object>
            {
                ["Container"] = container, ["Blob"] = "missing.txt", ["Context"] = context,
            }));
            Assert.Contains("HTTP 404", missing.Message);
        });
    }

    /// <summary>
    /// Records the CLI body against the live account, or replays the one recording on the CLI body
    /// and then on the PowerShell body. Record mode empties each container first so a re-record
    /// starts from the same state.
    /// </summary>
    private static void Run(string test, string[] containers, Action<ScenarioHost, Scope, bool> cli,
        Action<ScenarioHost, Scope, StorageContext> ps, IRequestMatcher? matcher = null, Action<SessionRecord>? assertRecording = null)
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneScenarioTests), test, matcher);
        if (recording.Mode == RecorderMode.Record)
        {
            using var scope = new Scope();
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), useLiveAuth: true, extensions: s_extensions);
            foreach (string container in containers)
            {
                StorageBlobLiveSetup.EnsureContainer(Account, container);
                StorageBlobLiveSetup.EmptyContainer(Account, container);
            }
            cli(host, scope, true);
            recording.Save();
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
            body = Regex.Replace(body, "<SignedExpiry>[^<]*</SignedExpiry>", $"<SignedExpiry>{StorageTime(now.AddHours(2))}</SignedExpiry>");
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
