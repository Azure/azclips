// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Configuration.Options;
using Core.DependencyInjection;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Microsoft.Azure.Azclips.Extensions.Extension;

/// <summary>
/// Installs Azclips extensions from NuGet feeds or local packages using official NuGet APIs.
/// </summary>
public class ExtensionInstaller
{
    public string InstallPath { get; }
    public NuGetFramework TargetFramework { get; }
    public string? FeedUrl { get; }

    private readonly ILoggingService _logger;

    public ExtensionInstaller(ILoggingService logger, string? feedUrl = null)
    {
        _logger = logger;
        // Install root is a plumbing detail resolved from the same config key the loaders honor
        // (assemblyLoading.installedExtensionsRoot); callers never pass a path.
        var config = AzclipsServiceProvider.GetRequiredService<IConfigurationService>();
        InstallPath = config.GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot;

        TargetFramework = new NuGetFramework(".NETCoreApp", Environment.Version);
        FeedUrl = feedUrl;

        Directory.CreateDirectory(InstallPath);
    }

    /// <summary>
    /// Installs an extension from NuGet feed.
    /// </summary>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="version">The package version.</param>
    /// <param name="includePrerelease">Whether to include prerelease versions when resolving the latest version.</param>
    /// <param name="includeUnlisted">Whether to include unlisted versions when resolving the latest version.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the installed extension directory.</returns>
    public async Task<string> InstallExtensionAsync(
        string packageId,
        string? version,
        bool includePrerelease = true,
        bool includeUnlisted = false,
        CancellationToken cancellationToken = default)
    {
        var repositories = GetSourceRepositories();

        // Resolve latest version if not specified
        if (string.IsNullOrWhiteSpace(version))
        {
            version = await GetLatestVersionAsync(packageId, repositories, includePrerelease, includeUnlisted, cancellationToken) ?? throw new InvalidOperationException($"Could not find package {packageId} in any configured NuGet feed.");
        }
        var outputDir = InitializeExtensionFolder(packageId, version);

        var rootPackage = new PackageIdentity(packageId, NuGetVersion.Parse(version));
        _logger.Info($"Installing {packageId}@{version}...");
        await ExtractPackageAssembliesAsync(rootPackage, repositories, outputDir, cancellationToken);

        _logger.Info($"Installed {packageId}@{version} to {outputDir}");
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
        var outputDir = InitializeExtensionFolder(packageId, version.ToString());

        // Extract the main package first
        _logger.Info($"Extracting {packageId}@{version}...");
        await ExtractLibItemsAsync(packageReader, outputDir, cancellationToken);

        _logger.Info($"Installed {packageId}@{version} from {packagePath}");
        return outputDir;
    }

    private string InitializeExtensionFolder(string packageId, string version)
    {
        var extensionDir = Path.Combine(InstallPath, packageId, version);
        try
        {
            if (Directory.Exists(extensionDir))
            {
                Directory.Delete(extensionDir, recursive: true);
            }

            Directory.CreateDirectory(extensionDir);
            return extensionDir;
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"Failed to prepare installation directory '{extensionDir}' for extension '{packageId}@{version}'. Files may be in use or locked.",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Access denied while preparing installation directory '{extensionDir}' for extension '{packageId}@{version}'.",
                ex);
        }
    }

    /// <summary>
    /// Gets the configured NuGet source repositories.
    /// </summary>
    /// <returns>A read-only list of configured NuGet source repositories.</returns>
    private IReadOnlyList<SourceRepository> GetSourceRepositories()
    {
        if (!string.IsNullOrWhiteSpace(FeedUrl))
        {
            var customSource = new PackageSource(FeedUrl, "CustomFeed");
            var customRepository = Repository.Factory.GetCoreV3(customSource);
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
    /// Gets the latest version of a package from configured NuGet feeds.
    /// </summary>
    /// <param name="packageId">The NuGet package ID.</param>
    /// <param name="repositories">The list of NuGet source repositories to search.</param>
    /// <param name="includePrerelease">Whether to include prerelease versions.</param>
    /// <param name="includeUnlisted">Whether to include unlisted versions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The latest version string, or null if not found.</returns>
    private async Task<string?> GetLatestVersionAsync(
        string packageId,
        IReadOnlyList<SourceRepository> repositories,
        bool includePrerelease,
        bool includeUnlisted,
        CancellationToken cancellationToken)
    {
        using var cacheContext = new SourceCacheContext();
        var logger = NullLogger.Instance;

        foreach (var repository in repositories)
        {
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);

            var packages = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: includePrerelease,
                includeUnlisted: includeUnlisted,
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

        using var cacheContext = new SourceCacheContext();
        var logger = NullLogger.Instance;

        // Download from repository
        foreach (var repository in repositories)
        {
            var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken);

            using var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
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

        throw new InvalidOperationException($"Could not download package {package.Id}@{package.Version} from any configured repository.");
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
            // Only extract DLLs and the build-time sidecar (CLIPS-933) that ships alongside them.
            var isDll = item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var isSidecar = item.EndsWith(".commands.json", StringComparison.OrdinalIgnoreCase);
            if (!isDll && !isSidecar)
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