using Core.Abstractions;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;
using Tests.Recording;
using Tests.Common.Recording;

namespace Scenario.Tests;

/// <summary>
/// Blob and file data-plane lifecycles against one storage account, driven through the real
/// dispatcher/binder and pipeline. Each step runs the CLI command and its PowerShell index entry
/// with the same values, so record mode captures the CLI surface and playback replays the one
/// recording on both.
/// <para>
/// Auth is the account key (Shared Key). The <c>Authorization</c> header is dropped from the
/// recording and never takes part in matching, so playback signs with a placeholder key and no
/// credential reaches the committed file. SAS is avoided on purpose: its <c>sig=</c> would land in
/// recorded URIs.
/// </para>
/// </summary>
[Collection("Scenario Tests")]
public class StorageDataPlaneLifecycleScenarioTests
{
    private const string NotRecorded = "No recording yet: set AZURE_TEST_MODE=Record and AZCLIPS_TEST_STORAGE_ACCOUNT_KEY, remove Skip, and record.";

    // Recorded hosts are <account>.blob/.file.core.windows.net, so playback must use the same name.
    // The account is not created here; record against an existing one and change this to re-record.
    private const string AccountName = "azclipsdataplanetest";
    private const string AccountKeyVariable = "AZCLIPS_TEST_STORAGE_ACCOUNT_KEY";

    private const string ContainerName = "scenario-blob-container";
    private const string BlobName = "hello.txt";
    private const string CopiedBlobName = "hello-copy.txt";

    private const string ShareName = "scenario-file-share";
    private const string DirectoryPath = "dir";
    private const string CreatedFilePath = "dir/empty.txt";
    private const string UploadedFilePath = "dir/hello.txt";
    private const string CopiedFilePath = "dir/hello-copy.txt";

    private const string Content = "hello azclips";

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Storage];

    [Fact(Skip = NotRecorded)]
    public void ContainerBlobLifecycle()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneLifecycleScenarioTests), nameof(ContainerBlobLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            // Shared Key needs no ARM token, so the stubbed profile/auth stay in place.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions);
            RunBlobLifecycle(host, ClientType.CLI, LiveAccountKey());
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunBlobLifecycle(cli, ClientType.CLI, PlaybackAccountKey);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunBlobLifecycle(ps, ClientType.PowerShell, PlaybackAccountKey);
        }
    }

    [Fact(Skip = NotRecorded)]
    public void ShareDirectoryFileLifecycle()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneLifecycleScenarioTests), nameof(ShareDirectoryFileLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions);
            RunFileLifecycle(host, ClientType.CLI, LiveAccountKey());
            recording.Save();
            return;
        }

        using (var cli = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunFileLifecycle(cli, ClientType.CLI, PlaybackAccountKey);
        }

        using (var ps = new ScenarioHost(ClientType.PowerShell, recording.CreateInterceptor(), extensions: s_extensions))
        {
            RunFileLifecycle(ps, ClientType.PowerShell, PlaybackAccountKey);
        }
    }

    private static void RunBlobLifecycle(ScenarioHost host, ClientType clientType, string accountKey)
    {
        using var local = new LocalFiles();
        var source = local.Write("upload.txt", Content);
        var downloaded = local.PathOf("download.txt");
        var steps = new Steps(host, clientType, accountKey);

        Assert.IsType<ContainerCreateResult>(Assert.Single(steps.Run(
            ["storage", "container", "create", "--name", ContainerName],
            "New-AzStorageContainer", new() { ["ContainerName"] = ContainerName })));

        Assert.IsType<BlobUploadResult>(Assert.Single(steps.Run(
            ["storage", "blob", "upload", "--container-name", ContainerName, "--name", BlobName, "--file", source, "--content-type", "text/plain"],
            "Set-AzStorageBlobContent", new() { ["ContainerName"] = ContainerName, ["BlobName"] = BlobName, ["FilePath"] = source, ["BlobContentType"] = "text/plain" })));

        var listed = Flatten<BlobItem>(steps.Run(
            ["storage", "blob", "list", "--container-name", ContainerName],
            "Get-AzStorageBlob_List", new() { ["ContainerName"] = ContainerName }));
        Assert.Contains(listed, blob => blob.NameContent == BlobName);

        var shown = Assert.IsType<BlobItem>(Assert.Single(steps.Run(
            ["storage", "blob", "show", "--container-name", ContainerName, "--name", BlobName],
            "Get-AzStorageBlob_Get", new() { ["ContainerName"] = ContainerName, ["BlobName"] = BlobName })));
        Assert.Equal(BlobName, shown.NameContent);

        var download = Assert.IsType<BlobDownloadResult>(Assert.Single(steps.Run(
            ["storage", "blob", "download", "--container-name", ContainerName, "--name", BlobName, "--file", downloaded],
            "Get-AzStorageBlobContent", new() { ["ContainerName"] = ContainerName, ["BlobName"] = BlobName, ["FilePath"] = downloaded })));
        Assert.Equal(Content.Length, download.ContentLength);
        Assert.Equal(Content, File.ReadAllText(downloaded));

        // Same-account source, so the destination's Shared Key authorizes the read.
        var sourceUrl = $"https://{AccountName}.blob.core.windows.net/{ContainerName}/{BlobName}";
        var copy = Assert.IsType<BlobCopyResult>(Assert.Single(steps.Run(
            ["storage", "blob", "copy", "start", "--destination-container", ContainerName, "--destination-blob", CopiedBlobName, "--source-uri", sourceUrl],
            "Start-AzStorageBlobCopy", new() { ["ContainerName"] = ContainerName, ["BlobName"] = CopiedBlobName, ["SourceUrl"] = sourceUrl })));
        Assert.False(string.IsNullOrEmpty(copy.CopyId));

        // TODO: blob upload-from-url (Copy-AzStorageBlob). Put Blob From URL needs the source
        // authorized on its own (SAS or public), which this key-only recording can't carry yet.

        foreach (var blob in new[] { CopiedBlobName, BlobName })
        {
            Assert.Empty(steps.Run(
                ["storage", "blob", "delete", "--container-name", ContainerName, "--name", blob],
                "Remove-AzStorageBlob", new() { ["ContainerName"] = ContainerName, ["BlobName"] = blob }));
        }

        Assert.Empty(steps.Run(
            ["storage", "container", "delete", "--name", ContainerName],
            "Remove-AzStorageContainer", new() { ["ContainerName"] = ContainerName }));
    }

    private static void RunFileLifecycle(ScenarioHost host, ClientType clientType, string accountKey)
    {
        using var local = new LocalFiles();
        var source = local.Write("upload.txt", Content);
        var downloaded = local.PathOf("download.txt");
        var steps = new Steps(host, clientType, accountKey);

        var created = Assert.IsType<ShareCreateResult>(Assert.Single(steps.Run(
            ["storage", "share", "create", "--name", ShareName, "--quota", "1"],
            "New-AzStorageShare", new() { ["ShareName"] = ShareName, ["Quota"] = 1 })));
        Assert.False(string.IsNullOrEmpty(created.ETag));

        Assert.NotEmpty(steps.Run(
            ["storage", "share", "show", "--name", ShareName],
            "Get-AzStorageShare_Get", new() { ["ShareName"] = ShareName }));

        // The prefix keeps other shares in the account out of the recording.
        Assert.NotEmpty(steps.Run(
            ["storage", "share", "list", "--prefix", ShareName],
            "Get-AzStorageShare_List", new() { ["Prefix"] = ShareName }));

        steps.Run(
            ["storage", "share", "update", "--name", ShareName, "--quota", "2"],
            "Update-AzStorageShare", new() { ["ShareName"] = ShareName, ["Quota"] = 2 });

        Assert.IsType<ShareDirectory>(Assert.Single(steps.Run(
            ["storage", "directory", "create", "--share-name", ShareName, "--name", DirectoryPath],
            "New-AzStorageDirectory", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath })));

        var directory = Assert.IsType<ShareDirectory>(Assert.Single(steps.Run(
            ["storage", "directory", "show", "--share-name", ShareName, "--name", DirectoryPath],
            "Get-AzStorageDirectory", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath })));
        Assert.Equal(DirectoryPath, directory.Name);

        Assert.IsType<ShareFile>(Assert.Single(steps.Run(
            ["storage", "file", "create", "--share-name", ShareName, "--path", CreatedFilePath, "--size", "16"],
            "New-AzStorageFile", new() { ["ShareName"] = ShareName, ["FilePath"] = CreatedFilePath, ["Size"] = 16L })));

        Assert.IsType<FileUploadResult>(Assert.Single(steps.Run(
            ["storage", "file", "upload", "--share-name", ShareName, "--path", UploadedFilePath, "--source", source],
            "Set-AzStorageFileContent", new() { ["ShareName"] = ShareName, ["FilePath"] = UploadedFilePath, ["Source"] = source })));

        var page = Assert.IsType<ListFilesAndDirectoriesSegmentResponse>(Assert.Single(steps.Run(
            ["storage", "file", "list", "--share-name", ShareName, "--name", DirectoryPath],
            "Get-AzStorageFile_List", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath })));
        Assert.Equal(["empty.txt", "hello.txt"], page.SegmentFileItems.Select(file => file.NameContent).Order());

        var shown = Assert.IsType<ShareFile>(Assert.Single(steps.Run(
            ["storage", "file", "show", "--share-name", ShareName, "--path", UploadedFilePath],
            "Get-AzStorageFile_Get", new() { ["ShareName"] = ShareName, ["FilePath"] = UploadedFilePath })));
        Assert.Equal(Content.Length, shown.ContentLength);

        var download = Assert.IsType<FileDownloadResult>(Assert.Single(steps.Run(
            ["storage", "file", "download", "--share-name", ShareName, "--path", UploadedFilePath, "--dest", downloaded],
            "Get-AzStorageFileContent", new() { ["ShareName"] = ShareName, ["FilePath"] = UploadedFilePath, ["Destination"] = downloaded })));
        Assert.Equal(Content.Length, download.ContentLength);
        Assert.Equal(Content, File.ReadAllText(downloaded));

        var sourceUrl = $"https://{AccountName}.file.core.windows.net/{ShareName}/{UploadedFilePath}";
        var copy = Assert.IsType<FileCopyResult>(Assert.Single(steps.Run(
            ["storage", "file", "copy", "start", "--share-name", ShareName, "--path", CopiedFilePath, "--source-uri", sourceUrl],
            "Start-AzStorageFileCopy", new() { ["ShareName"] = ShareName, ["FilePath"] = CopiedFilePath, ["SourceUrl"] = sourceUrl })));
        Assert.False(string.IsNullOrEmpty(copy.CopyId));

        foreach (var file in new[] { CopiedFilePath, UploadedFilePath, CreatedFilePath })
        {
            Assert.Empty(steps.Run(
                ["storage", "file", "delete", "--share-name", ShareName, "--path", file],
                "Remove-AzStorageFile", new() { ["ShareName"] = ShareName, ["FilePath"] = file }));
        }

        Assert.Empty(steps.Run(
            ["storage", "directory", "delete", "--share-name", ShareName, "--name", DirectoryPath],
            "Remove-AzStorageDirectory", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath }));

        Assert.Empty(steps.Run(
            ["storage", "share", "delete", "--name", ShareName],
            "Remove-AzStorageShare", new() { ["ShareName"] = ShareName }));
    }

    // Any well-formed base64 key works on playback: the signature it produces is never matched.
    private static readonly string PlaybackAccountKey = Convert.ToBase64String(new byte[64]);

    private static string LiveAccountKey() =>
        Environment.GetEnvironmentVariable(AccountKeyVariable)
        ?? throw new InvalidOperationException($"Set {AccountKeyVariable} to the key of storage account '{AccountName}' before recording.");

    /// <summary>
    /// Lists write either items or one batch per page; collect the items either way.
    /// </summary>
    private static List<T> Flatten<T>(IReadOnlyList<object> outputs) =>
        [.. outputs.SelectMany(output => output is IEnumerable<T> batch ? batch : output is T item ? [item] : [])];

    /// <summary>
    /// Runs one step on the host's surface, adding the account and key both surfaces share.
    /// </summary>
    private sealed class Steps(ScenarioHost host, ClientType clientType, string accountKey)
    {
        public IReadOnlyList<object> Run(string[] cliArgs, string psCommand, Dictionary<string, object> psParameters)
        {
            if (clientType == ClientType.CLI)
            {
                return host.RunCli([.. cliArgs, "--account-name", AccountName, "--account-key", accountKey]);
            }

            psParameters["StorageAccountName"] = AccountName;
            psParameters["StorageAccountKey"] = accountKey;
            return host.RunPowerShell(psCommand, psParameters, extension: "Storage");
        }
    }

    private sealed class LocalFiles : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("azclips-storage-scenario-").FullName;

        public string PathOf(string name) => Path.Combine(_root, name);

        public string Write(string name, string content)
        {
            var path = PathOf(name);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
