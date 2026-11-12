using Core.Abstractions;
using Microsoft.Azure.AzClips.Extensions.StorageBlob.Models;
using Microsoft.Azure.AzClips.Extensions.StorageFile.Models;
using Tests.Recording;
using Tests.Common.Recording;
using Tests.Common.Utilities;

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
    // Recorded hosts are <account>.blob/.file.core.windows.net, so playback must use the same name.
    // The account is not created here; record against an existing one and change this to re-record.
    private const string AccountName = "20261110testaccount";
    private const string AccountKeyVariable = "AZCLIPS_TEST_STORAGE_ACCOUNT_KEY";

    private const string ResourceName = "20261112testxxx";

    private const string ContainerName = ResourceName;
    private const string BlobName = ResourceName + ".txt";
    private const string CopiedBlobName = ResourceName + "-copy.md";
    private const string FromUrlBlobName = ResourceName + "-fromurl.md";

    private const string ShareName = ResourceName;
    private const string DirectoryPath = ResourceName;
    private const string CreatedFilePath = DirectoryPath + "/" + ResourceName + "-empty.txt";
    private const string UploadedFilePath = DirectoryPath + "/" + ResourceName + ".txt";
    private const string CopySourceFilePath = DirectoryPath + "/" + ResourceName + "-copysource.txt";
    private const string CopiedFilePath = DirectoryPath + "/" + ResourceName + "-copy.txt";

    // A public source, so copy and upload-from-url need no source SAS in the recording.
    private const string ExternalSourceUrl = "https://raw.githubusercontent.com/Azure/azure-rest-api-specs/refs/heads/main/specification/storage/data-plane/BlobStorage/readme.md";

    private static readonly string StorageTestDirectory = Path.Combine(RepoLocator.GetRepoRoot(), "tests", "Scenario.Tests", "Storage");
    private static readonly string UploadFile = Path.Combine(StorageTestDirectory, "uploadtestfile.txt");
    private static readonly string CopySourceFile = Path.Combine(StorageTestDirectory, "uploadtestfile4copy.txt");

    private static readonly ExtensionUnderTest[] s_extensions = [ExtensionUnderTest.Storage];

    [Fact]
    public void ContainerBlobLifecycle()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneLifecycleScenarioTests), nameof(ContainerBlobLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            // Shared Key needs no ARM token, so the stubbed profile/auth stay in place.
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions);
            var accountKey = LiveAccountKey();
            try
            {
                RunBlobLifecycle(host, ClientType.CLI, accountKey);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording; deleting the container removes its blobs.
                try { host.RunCli("storage", "container", "delete", "--name", ContainerName, "--account-name", AccountName, "--account-key", accountKey); } catch { }
            }
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

    [Fact]
    public void ShareDirectoryFileLifecycle()
    {
        var recording = SessionRecordFiles.OpenSession(typeof(StorageDataPlaneLifecycleScenarioTests), nameof(ShareDirectoryFileLifecycle));

        if (recording.Mode == RecorderMode.Record)
        {
            using var host = new ScenarioHost(ClientType.CLI, recording.CreateInterceptor(), extensions: s_extensions);
            var accountKey = LiveAccountKey();
            try
            {
                RunFileLifecycle(host, ClientType.CLI, accountKey);
                recording.Save();
            }
            finally
            {
                // After the save, so the delete never lands in the recording; deleting the share removes its files.
                try { host.RunCli("storage", "share", "delete", "--name", ShareName, "--account-name", AccountName, "--account-key", accountKey); } catch { }
            }
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
        var source = UploadFile;
        var downloaded = local.PathOf("download.txt");
        var steps = new Steps(host, clientType, accountKey);

        Assert.IsType<ContainerCreateResult>(Assert.Single(steps.Run(
            ["storage", "container", "create", "--name", ContainerName],
            "New-AzStorageContainer", new() { ["ContainerName"] = ContainerName })));

        Assert.IsType<BlobUploadResult>(Assert.Single(steps.Run(
            ["storage", "blob", "upload", "--container-name", ContainerName, "--name", BlobName, "--file", source],
            "Set-AzStorageBlobContent", new() { ["ContainerName"] = ContainerName, ["BlobName"] = BlobName, ["FilePath"] = source })));

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
        AssertDownloaded(source, downloaded, download.ContentLength);

        var copy = Assert.IsType<BlobCopyResult>(Assert.Single(steps.Run(
            ["storage", "blob", "copy", "start", "--destination-container", ContainerName, "--destination-blob", CopiedBlobName, "--source-uri", ExternalSourceUrl],
            "Start-AzStorageBlobCopy", new() { ["ContainerName"] = ContainerName, ["BlobName"] = CopiedBlobName, ["SourceUrl"] = ExternalSourceUrl })));
        Assert.False(string.IsNullOrEmpty(copy.CopyId));

        Assert.IsType<BlobUploadResult>(Assert.Single(steps.Run(
            ["storage", "blob", "upload-from-url", "--container-name", ContainerName, "--name", FromUrlBlobName, "--source-uri", ExternalSourceUrl],
            "Copy-AzStorageBlob", new() { ["ContainerName"] = ContainerName, ["BlobName"] = FromUrlBlobName, ["SourceUrl"] = ExternalSourceUrl })));

        foreach (var blob in new[] { FromUrlBlobName, CopiedBlobName, BlobName })
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
        var source = UploadFile;
        var downloaded = local.PathOf("download.txt");
        var steps = new Steps(host, clientType, accountKey);

        var created = Assert.IsType<ShareCreateResult>(Assert.Single(steps.Run(
            ["storage", "share", "create", "--name", ShareName],
            "New-AzStorageShare", new() { ["ShareName"] = ShareName })));
        Assert.False(string.IsNullOrEmpty(created.ETag));

        Assert.NotEmpty(steps.Run(
            ["storage", "share", "show", "--name", ShareName],
            "Get-AzStorageShare_Get", new() { ["ShareName"] = ShareName }));

        Assert.NotEmpty(steps.Run(
            ["storage", "share", "list"],
            "Get-AzStorageShare_List", new()));

        Assert.IsType<ShareDirectory>(Assert.Single(steps.Run(
            ["storage", "directory", "create", "--share-name", ShareName, "--name", DirectoryPath],
            "New-AzStorageDirectory", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath })));

        var directory = Assert.IsType<ShareDirectory>(Assert.Single(steps.Run(
            ["storage", "directory", "show", "--share-name", ShareName, "--name", DirectoryPath],
            "Get-AzStorageDirectory", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath })));
        Assert.Equal(DirectoryPath, directory.Name);

        Assert.IsType<ShareFile>(Assert.Single(steps.Run(
            ["storage", "file", "create", "--share-name", ShareName, "--path", CreatedFilePath],
            "New-AzStorageFile", new() { ["ShareName"] = ShareName, ["FilePath"] = CreatedFilePath })));

        Assert.IsType<FileUploadResult>(Assert.Single(steps.Run(
            ["storage", "file", "upload", "--share-name", ShareName, "--path", UploadedFilePath, "--source", source],
            "Set-AzStorageFileContent", new() { ["ShareName"] = ShareName, ["FilePath"] = UploadedFilePath, ["Source"] = source })));

        var page = Assert.IsType<ListFilesAndDirectoriesSegmentResponse>(Assert.Single(steps.Run(
            ["storage", "file", "list", "--share-name", ShareName, "--name", DirectoryPath],
            "Get-AzStorageFile_List", new() { ["ShareName"] = ShareName, ["DirectoryPath"] = DirectoryPath })));
        Assert.Equal([Path.GetFileName(CreatedFilePath), Path.GetFileName(UploadedFilePath)], page.SegmentFileItems.Select(file => file.NameContent).Order());

        var shown = Assert.IsType<ShareFile>(Assert.Single(steps.Run(
            ["storage", "file", "show", "--share-name", ShareName, "--path", UploadedFilePath],
            "Get-AzStorageFile_Get", new() { ["ShareName"] = ShareName, ["FilePath"] = UploadedFilePath })));

        var download = Assert.IsType<FileDownloadResult>(Assert.Single(steps.Run(
            ["storage", "file", "download", "--share-name", ShareName, "--path", UploadedFilePath, "--dest", downloaded],
            "Get-AzStorageFileContent", new() { ["ShareName"] = ShareName, ["FilePath"] = UploadedFilePath, ["Destination"] = downloaded })));
        Assert.Equal(shown.ContentLength, download.ContentLength);
        AssertDownloaded(source, downloaded, download.ContentLength);

        // Copy File only accepts an Azure Files or Blob source, so the external URL can't be used here;
        // a same-account source is authorized by the destination's Shared Key.
        Assert.IsType<FileUploadResult>(Assert.Single(steps.Run(
            ["storage", "file", "upload", "--share-name", ShareName, "--path", CopySourceFilePath, "--source", CopySourceFile],
            "Set-AzStorageFileContent", new() { ["ShareName"] = ShareName, ["FilePath"] = CopySourceFilePath, ["Source"] = CopySourceFile })));

        var sourceUrl = $"https://{AccountName}.file.core.windows.net/{ShareName}/{CopySourceFilePath}";
        var copy = Assert.IsType<FileCopyResult>(Assert.Single(steps.Run(
            ["storage", "file", "copy", "start", "--share-name", ShareName, "--path", CopiedFilePath, "--source-uri", sourceUrl],
            "Start-AzStorageFileCopy", new() { ["ShareName"] = ShareName, ["FilePath"] = CopiedFilePath, ["SourceUrl"] = sourceUrl })));
        Assert.False(string.IsNullOrEmpty(copy.CopyId));

        foreach (var file in new[] { CopiedFilePath, CopySourceFilePath, UploadedFilePath, CreatedFilePath })
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

    // The upload file's line endings depend on the checkout, while the recording keeps the recorder's,
    // so compare text with normalised line endings and the length against what was written.
    private static void AssertDownloaded(string source, string downloaded, long? contentLength)
    {
        Assert.Equal(new FileInfo(downloaded).Length, contentLength);
        Assert.Equal(File.ReadAllText(source).ReplaceLineEndings(), File.ReadAllText(downloaded).ReplaceLineEndings());
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

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
