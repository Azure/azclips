using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class AddExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = false,
    Description = @"The name of the extension to add.",
    SerializedName = @"Name")]
    public string? Name { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"The version of the extension to add.",
    SerializedName = @"Version")]
    public string? Version { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Feed URL to install the extension from.",
    SerializedName = @"FeedUrl")]
    public string? FeedUrl { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Path to the extension .nupkg file to add.",
    SerializedName = @"Path")]
    public string? Path { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Pin the installed version so future 'update' commands skip it without --force.",
    SerializedName = @"Pin")]
    public bool Pin { get; set; }

    public IProcessor Init(ProcessorContext context)
    {
        _writeOutput = context.OutputHandler.WriteOutput;
        return this;
    }

    public async Task ProcessAsync()
    {
        var result = Process();
        await _writeOutput(result);
    }

    public object Process()
    {
        if (string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Path))
        {
            throw CirrusErrorException.UsageError("Either Name or Path must be provided to add an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(AddExtensionProcessor)}.{nameof(Process)}");

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl);
        string installPath;
        if (!string.IsNullOrWhiteSpace(Name))
        {
            installPath = installer.InstallExtensionAsync(
                $"Microsoft.Azure.Cirrus.Extensions.{Name}",
                Version,
                includePrerelease: true,
                includeUnlisted: false,
                CancellationToken.None
            ).GetAwaiter().GetResult();
        }
        else
        {
            installPath = installer.InstallFromPackageAsync(Path!, CancellationToken.None).GetAwaiter().GetResult();
        }

        // Recover packageId and version from the install path.
        // Layout (set by ExtensionInstaller.InitializeExtensionFolder): {InstallPath}/{packageId}/{version}/
        var (packageId, version) = ParseInstallPath(installer.InstallPath, installPath);

        try
        {
            // Per docs/spec/extension-management.md §5.2: read the declared verb from the installed
            // DLLs *without* executing extension code, then validate against built-ins + registry
            // before committing the registry write.
            var verb = ExtensionVerbReader.ReadVerb(installPath)
                ?? throw CirrusErrorException.VerbConflict(
                    $"Extension '{packageId}' does not declare an [ExtensionVerb] attribute. " +
                    $"Cirrus extensions must declare exactly one top-level verb.",
                    new Dictionary<string, object?> { ["packageId"] = packageId });

            if (BuiltInExtensions.IsBuiltInVerb(verb))
            {
                throw CirrusErrorException.VerbConflict(
                    $"Extension '{packageId}' wants to register verb '{verb}', but '{verb}' is reserved for a built-in extension and cannot be claimed by an installed extension.",
                    new Dictionary<string, object?> { ["packageId"] = packageId, ["verb"] = verb });
            }

            var store = new FileExtensionRegistryStore();
            var registry = store.Load();

            // Conflict check: another extension already claims this verb?
            // Re-installing the same package is fine (upsert).
            foreach (var (existingPackageId, existingEntry) in registry.Extensions)
            {
                if (string.Equals(existingPackageId, packageId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(existingEntry.Verb, verb, StringComparison.OrdinalIgnoreCase))
                {
                    throw CirrusErrorException.VerbConflict(
                        $"Extension '{packageId}' wants to register verb '{verb}', but it is already owned by '{existingPackageId}'. " +
                        $"Remove the existing extension first: azv2 extension remove {existingPackageId}",
                        new Dictionary<string, object?>
                        {
                            ["packageId"] = packageId,
                            ["verb"] = verb,
                            ["existingPackageId"] = existingPackageId,
                        });
                }
            }

            // Per Q6 (strict single-version): purge any sibling version directories under the
            // package's parent dir so only the just-installed version remains on disk.
            PurgeSiblingVersions(installer.InstallPath, packageId, keepVersion: version);

            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Verb = verb,
                Version = version,
                Pinned = Pin,
                Source = FeedUrl,
                InstalledAt = DateTimeOffset.UtcNow,
            };
            store.Save(registry);

            return new
            {
                PackageId = packageId,
                Version = version,
                Verb = verb,
                Pinned = Pin,
                InstalledExtensionPath = installPath,
            };
        }
        catch
        {
            // Roll back the file-system side of the install. The registry was either not touched
            // or its old contents are unchanged (Save throws before partial writes are visible).
            TryDeleteDirectory(installPath);
            throw;
        }
    }

    public string Process(bool Serialize)
    {
        var result = Process();
        if (Serialize)
        {
            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            return result.ToString() ?? "";
        }
    }

    private static (string PackageId, string Version) ParseInstallPath(string installRoot, string installPath)
    {
        var relative = System.IO.Path.GetRelativePath(installRoot, installPath);
        var parts = relative.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException(
                $"Installed path '{installPath}' does not match the expected '{{installRoot}}/{{packageId}}/{{version}}' layout.");
        }
        return (parts[0], parts[1]);
    }

    private static void PurgeSiblingVersions(string installRoot, string packageId, string keepVersion)
    {
        var packageDir = System.IO.Path.Combine(installRoot, packageId);
        if (!Directory.Exists(packageDir))
        {
            return;
        }

        foreach (var versionDir in Directory.GetDirectories(packageDir))
        {
            var versionName = System.IO.Path.GetFileName(versionDir);
            if (string.Equals(versionName, keepVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            TryDeleteDirectory(versionDir);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; surfacing here would mask the original error from the caller.
        }
        catch (UnauthorizedAccessException)
        {
            // Same — best-effort.
        }
    }
}