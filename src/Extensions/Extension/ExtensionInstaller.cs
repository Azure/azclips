using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.Azure.Cirrus.Extensions.Extension;

/// <summary>
/// Installs Cirrus extensions from NuGet feeds or local packages using official NuGet APIs.
/// </summary>
public class ExtensionInstaller
{
    public string InstallPath { get; }
    public NuGetFramework TargetFramework { get; }
    public string? FeedUrl { get; }

    public ExtensionInstaller(string? installPath = null, string? feedUrl = null)
    {
        InstallPath = installPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cirrus", "extensions");

        TargetFramework = new NuGetFramework(".NETCoreApp", Environment.Version);
        FeedUrl = feedUrl;

        Directory.CreateDirectory(InstallPath);
    }

    /// <summary>
    /// Installs an extension from NuGet feed.
    /// </summary>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="version">The package version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the installed extension directory.</returns>
    public async Task<string> InstallExtensionAsync(
        string packageId,
        string? version,
        CancellationToken cancellationToken)
    {
        var repositories = GetSourceRepositories();

        // Resolve latest version if not specified
        if (string.IsNullOrWhiteSpace(version))
        {
            version = await GetLatestVersionAsync(packageId, repositories, cancellationToken) ?? throw new InvalidOperationException($"Could not find package {packageId} in any configured NuGet feed.");
        }

        var outputDir = Path.Combine(InstallPath, packageId, version);
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }
        Directory.CreateDirectory(outputDir);

        var rootPackage = new PackageIdentity(packageId, NuGetVersion.Parse(version));
        Console.WriteLine($"Installing {packageId}@{version}...");
        await ExtractPackageAssembliesAsync(rootPackage, repositories, outputDir, cancellationToken);

        Console.WriteLine($"Installed {packageId}@{version} to {outputDir}");
        return outputDir;
    }

    /// <summary>
    /// Installs an extension from a local .nupkg file.
    /// </summary>
    /// <param name="packagePath">Path to the .nupkg file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the installed extension directory.</returns>
    public async Task<string> InstallFromPackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException($"Package not found: {packagePath}");
        }

        using var stream = File.OpenRead(packagePath);
        using var packageReader = new PackageArchiveReader(stream);

        var nuspec = packageReader.NuspecReader;
        var packageId = nuspec.GetId();
        var version = nuspec.GetVersion();

        var outputDir = Path.Combine(InstallPath, packageId, version.ToString());
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }
        Directory.CreateDirectory(outputDir);

        // Extract the main package first
        Console.WriteLine($"Extracting {packageId}@{version}...");
        await ExtractLibItemsAsync(packageReader, outputDir, cancellationToken);

        Console.WriteLine($"Installed {packageId}@{version} from {packagePath}");
        return outputDir;
    }

    /// <summary>
    /// Gets the configured NuGet source repositories.
    /// </summary>
    /// <returns></returns>
    private IReadOnlyList<SourceRepository> GetSourceRepositories()
    {
        if (!string.IsNullOrWhiteSpace(FeedUrl))
        {
            var customSource = new PackageSource(FeedUrl, "CustomFeed");
            SourceRepository customRepository;
            customRepository = Repository.Factory.GetCoreV3(customSource);
            return [customRepository];
        }
        return GetDefaultSourceRepositories();
    }

    /// <summary>
    /// Gets default NuGet source repositories.
    /// </summary>
    private static IReadOnlyList<SourceRepository> GetDefaultSourceRepositories()
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var packageSourceProvider = new PackageSourceProvider(settings);
        var sourceRepositoryProvider = new SourceRepositoryProvider(packageSourceProvider, Repository.Provider.GetCoreV3());

        var repositories = sourceRepositoryProvider.GetRepositories().ToList();

        return repositories;
    }

    /// <summary>
    /// Gets the latest stable version of a package from configured NuGet feeds.
    /// </summary>
    private async Task<string?> GetLatestVersionAsync(
        string packageId,
        IReadOnlyList<SourceRepository> repositories,
        CancellationToken cancellationToken)
    {
        var cacheContext = new SourceCacheContext();
        var logger = NullLogger.Instance;

        foreach (var repository in repositories)
        {
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

            var packages = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: true,
                cacheContext,
                logger,
                cancellationToken);

            var latestVersion = packages
                .Select(p => p.Identity.Version)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (latestVersion != null)
            {
                return latestVersion.ToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts package assemblies from cache or downloads if needed.
    /// </summary>
    private async Task ExtractPackageAssembliesAsync(
        PackageIdentity package,
        IReadOnlyList<SourceRepository> repositories,
        string outputDir,
        CancellationToken cancellationToken)
    {
        var settings = Settings.LoadDefaultSettings(root: null);
        var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
        // Try global packages folder first (NuGet cache)
        var cachedPackagePath = Path.Combine(
            globalPackagesFolder,
            package.Id.ToLowerInvariant(),
            package.Version.ToString());

        if (Directory.Exists(cachedPackagePath))
        {
            using var folderReader = new PackageFolderReader(cachedPackagePath);
            await ExtractLibItemsAsync(folderReader, outputDir, cancellationToken);
            return;
        }

        var cacheContext = new SourceCacheContext();
        var logger = NullLogger.Instance;

        // Download from repository
        foreach (var repository in repositories)
        {
            var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken);

            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                package,
                new PackageDownloadContext(cacheContext),
                globalPackagesFolder,
                logger,
                cancellationToken);

            if (downloadResult.Status == DownloadResourceResultStatus.Available &&
                downloadResult.PackageReader is PackageReaderBase reader)
            {
                await ExtractLibItemsAsync(reader, outputDir, cancellationToken);
                return;
            }
        }

        Console.WriteLine($"Warning: Could not download package {package.Id}@{package.Version}");
    }

    /// <summary>
    /// Extracts lib items using official NuGetFrameworkUtility for framework selection.
    /// </summary>
    private async Task ExtractLibItemsAsync(
        PackageReaderBase packageReader,
        string outputDir,
        CancellationToken cancellationToken)
    {
        var libItems = (await packageReader.GetLibItemsAsync(cancellationToken)).ToList();

        if (libItems.Count == 0)
        {
            return;
        }

        // Use official NuGetFrameworkUtility for framework selection
        var nearestGroup = NuGetFrameworkUtility.GetNearest(
            libItems,
            TargetFramework,
            item => item.TargetFramework);

        if (nearestGroup == null)
        {
            return;
        }

        foreach (var item in nearestGroup.Items)
        {
            // Only extract DLLs
            if (!item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetPath = Path.Combine(outputDir, Path.GetFileName(item));

            if (File.Exists(targetPath))
            {
                continue;
            }

            using var itemStream = await packageReader.GetStreamAsync(item, cancellationToken);
            using var fileStream = File.Create(targetPath);
            await itemStream.CopyToAsync(fileStream, cancellationToken);
        }
    }
}