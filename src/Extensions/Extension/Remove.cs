// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;
using NuGet.Versioning;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

public class RemoveExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id (full or short suffix) of the extension to remove. Verb and namespace tokens are not accepted.",
    SerializedName = @"Name")]
    public string Name { get; set; } = string.Empty;

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
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw AzclipsErrorException.UsageError("Name is required to remove an extension.");
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
        using var activity = telemetryService.StartActivity($"{nameof(RemoveExtensionProcessor)}.{nameof(Process)}");

        var store = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
        var fileRegistry = store.Load();

        // Metadata-only install lookup for remove: enumerate package/version directories without
        // loading extension assemblies. Loading assemblies here can lock DLLs on Windows and make
        // in-process delete fail.
        var installer = new ExtensionInstaller(loggingService);
        var registry = BuildInstalledRegistryFromFilesystem(installer.InstallPath, fileRegistry);

        if (!RegistryLookup.TryFind(registry, Name, out var match))
        {
            var message = $"No installed extension matches '{Name}' (by package id or short name).";
            if (RegistryLookup.SuggestPackageForToken(registry, Name, out var suggested))
            {
                message += $" '{Name}' is a verb contributed by package '{suggested}'; " +
                           $"to remove that package use '--name {RegistryLookup.ToShortName(suggested)}'.";
            }
            throw AzclipsErrorException.ExtensionNotFound(
                message,
                new Dictionary<string, object?> { ["name"] = Name });
        }

        var packageId = match.Key;
        var entry = match.Value;

        // Tag activity with extension dimensions; namespace_count prefers v2 Namespaces.
        activity?.SetTag("extension.package_id", packageId);
        activity?.SetTag("extension.version", entry.Version);
        activity?.SetTag("extension.source", entry.Source ?? "path");
        activity?.SetTag("extension.namespace_count",
            entry.Namespaces.Count > 0 ? entry.Namespaces.Count : (entry.Verb is null ? 0 : 1));
        activity?.SetTag("extension.contribution_count", entry.Contributions.Count);

        // Always wipe the whole package directory (strict single-version means at most one).
        var packageDir = Path.Combine(installer.InstallPath, packageId);

        EnsurePackageDirectoryDeleted(packageDir, packageId);

        // Remove preference entry only after payload delete succeeds so the command does not
        // report success while the extension remains discoverable on disk.
        fileRegistry.Extensions.Remove(packageId);
        store.Save(fileRegistry);

        return new
        {
            PackageId = packageId,
            Verb = entry.Verb,
            Version = entry.Version,
            Removed = true,
        };
    }

    public static ExtensionRegistry BuildInstalledRegistryFromFilesystem(string installRoot, ExtensionRegistry fileRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        ArgumentNullException.ThrowIfNull(fileRegistry);

        var discovered = new ExtensionRegistry { SchemaVersion = ExtensionRegistry.CurrentSchemaVersion };

        if (!Directory.Exists(installRoot))
        {
            return discovered;
        }

        foreach (var packageDir in Directory.GetDirectories(installRoot))
        {
            var packageId = Path.GetFileName(packageDir);
            if (string.IsNullOrWhiteSpace(packageId))
            {
                continue;
            }

            var entry = new ExtensionRegistryEntry
            {
                Version = ResolveLatestVersion(packageDir),
                Pinned = false,
                Source = null,
                InstalledAt = DateTimeOffset.UtcNow,
            };

            // Ignore directories that don't contain a valid semantic-version child folder.
            // They are not install roots for a removable extension payload.
            if (string.IsNullOrEmpty(entry.Version))
            {
                continue;
            }

            if (fileRegistry.Extensions.TryGetValue(packageId, out var fileEntry))
            {
                entry.Pinned = fileEntry.Pinned;
                entry.Source = fileEntry.Source;
                entry.InstalledAt = fileEntry.InstalledAt;
            }

            discovered.Extensions[packageId] = entry;
        }

        return discovered;
    }

    private static string ResolveLatestVersion(string packageDir)
    {
        var versionDirs = Directory.GetDirectories(packageDir);
        if (versionDirs.Length == 0)
        {
            return string.Empty;
        }

        var latest = versionDirs
            .Select(dir => (dir, parsed: NuGetVersion.TryParse(Path.GetFileName(dir), out var parsed) ? parsed : null))
            .Where(x => x.parsed is not null)
            .OrderByDescending(x => x.parsed)
            .Select(x => x.parsed!.ToNormalizedString())
            .FirstOrDefault();

        return latest ?? string.Empty;
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

    public static void EnsurePackageDirectoryDeleted(
        string packageDir,
        string packageId,
        Action<string>? deleteDirectory = null)
    {
        if (!Directory.Exists(packageDir))
        {
            return;
        }

        deleteDirectory ??= path => Directory.Delete(path, recursive: true);

        try
        {
            deleteDirectory(packageDir);
        }
        catch (DirectoryNotFoundException)
        {
            // Idempotent success: if the directory vanished between existence check and delete,
            // the desired postcondition (package directory absent) is already true.
            return;
        }
        catch (PathTooLongException ex)
        {
            throw new AzclipsErrorException(
                "ExtensionRemoveFailed",
                1,
                $"Failed to remove extension '{packageId}'. Path too long deleting '{packageDir}': {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["path"] = packageDir,
                });
        }
        catch (IOException ex)
        {
            throw new AzclipsErrorException(
                "ExtensionRemoveFailed",
                1,
                $"Failed to remove extension '{packageId}'. Could not delete '{packageDir}': {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["path"] = packageDir,
                });
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AzclipsErrorException(
                "ExtensionRemoveFailed",
                1,
                $"Failed to remove extension '{packageId}'. Access denied deleting '{packageDir}': {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["path"] = packageDir,
                });
        }
        catch (System.Security.SecurityException ex)
        {
            throw new AzclipsErrorException(
                "ExtensionRemoveFailed",
                1,
                $"Failed to remove extension '{packageId}'. Security policy blocked delete for '{packageDir}': {ex.Message}",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["path"] = packageDir,
                });
        }

        if (Directory.Exists(packageDir))
        {
            throw new AzclipsErrorException(
                "ExtensionRemoveFailed",
                1,
                $"Failed to remove extension '{packageId}'. Directory '{packageDir}' still exists after delete.",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["path"] = packageDir,
                });
        }
    }
}
