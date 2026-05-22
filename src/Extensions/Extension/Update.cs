using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class UpdateExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id or verb of the extension to update.",
    SerializedName = @"Name")]
    public string Name { get; set; } = string.Empty;

    [ArgumentInfo(
    Required = false,
    Description = @"Target version. Defaults to the latest available from the configured feed.",
    SerializedName = @"Version")]
    public string? Version { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Feed URL to install the new version from.",
    SerializedName = @"FeedUrl")]
    public string? FeedUrl { get; set; }

    [ArgumentInfo(
    Required = false,
    Description = @"Update even if the extension is pinned.",
    SerializedName = @"Force")]
    public bool Force { get; set; }

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
            throw CirrusErrorException.UsageError("Name is required to update an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(UpdateExtensionProcessor)}.{nameof(Process)}");

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        var match = registry.Extensions
            .FirstOrDefault(kvp =>
                string.Equals(kvp.Key, Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Value.Verb, Name, StringComparison.OrdinalIgnoreCase));

        if (match.Key is null)
        {
            throw CirrusErrorException.ExtensionNotFound(
                $"No installed extension matches '{Name}' (by package id or verb). Use 'extension add' first.",
                new Dictionary<string, object?> { ["name"] = Name });
        }

        var packageId = match.Key;
        var existing = match.Value;

        // Per spec Q5: pinning blocks update unless --force is passed.
        if (existing.Pinned && !Force)
        {
            throw CirrusErrorException.PinViolation(
                $"Extension '{packageId}' is pinned at version '{existing.Version}'. Pass --force to override the pin.",
                new Dictionary<string, object?>
                {
                    ["packageId"] = packageId,
                    ["pinnedVersion"] = existing.Version,
                });
        }

        var installer = new ExtensionInstaller(loggingService, feedUrl: FeedUrl ?? existing.Source);
        var installPath = installer.InstallExtensionAsync(
            packageId,
            Version,
            includePrerelease: true,
            includeUnlisted: false,
            CancellationToken.None
        ).GetAwaiter().GetResult();

        try
        {
            var verb = ExtensionVerbReader.ReadVerb(installPath)
                ?? throw CirrusErrorException.VerbConflict(
                    $"Updated package '{packageId}' does not declare an [ExtensionVerb] attribute.",
                    new Dictionary<string, object?> { ["packageId"] = packageId });

            // A package changing its verb across versions would silently rebind the command surface.
            // Block it: the user can `remove` then `add` if they really want that.
            if (!string.Equals(verb, existing.Verb, StringComparison.OrdinalIgnoreCase))
            {
                throw CirrusErrorException.VerbConflict(
                    $"Updated package '{packageId}' declares verb '{verb}', but the previously installed version owned '{existing.Verb}'. " +
                    $"Refusing to silently rebind. Run 'extension remove {packageId}' then 'extension add' to change verbs.",
                    new Dictionary<string, object?>
                    {
                        ["packageId"] = packageId,
                        ["newVerb"] = verb,
                        ["existingVerb"] = existing.Verb,
                    });
            }

            // Recover the resolved version from the install path layout: {root}/{pkgId}/{version}/
            var resolvedVersion = new DirectoryInfo(installPath).Name;

            // Q6 strict single-version: only the just-installed version stays on disk.
            PurgeSiblingVersions(installer.InstallPath, packageId, keepVersion: resolvedVersion);

            registry.Extensions[packageId] = new ExtensionRegistryEntry
            {
                Verb = existing.Verb,
                Version = resolvedVersion,
                // Forced update breaks the pin (the version it pinned no longer exists on disk).
                Pinned = existing.Pinned && !Force,
                Source = FeedUrl ?? existing.Source,
                InstalledAt = DateTimeOffset.UtcNow,
            };
            store.Save(registry);

            return new
            {
                PackageId = packageId,
                Verb = existing.Verb,
                PreviousVersion = existing.Version,
                Version = resolvedVersion,
                Pinned = existing.Pinned && !Force,
                Updated = true,
            };
        }
        catch
        {
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

    private static void PurgeSiblingVersions(string installRoot, string packageId, string keepVersion)
    {
        var packageDir = Path.Combine(installRoot, packageId);
        if (!Directory.Exists(packageDir))
        {
            return;
        }

        foreach (var versionDir in Directory.GetDirectories(packageDir))
        {
            var versionName = Path.GetFileName(versionDir);
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
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
