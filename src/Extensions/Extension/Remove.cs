using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

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
            throw CirrusErrorException.UsageError("Name is required to remove an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        var loggingService = CirrusServiceProvider.GetRequiredService<ILoggingService>();
        using var activity = telemetryService.StartActivity($"{nameof(RemoveExtensionProcessor)}.{nameof(Process)}");

        // Built-in verbs aren't in the registry; give a clear error rather than "not found".
        if (BuiltInExtensions.IsBuiltInVerb(Name))
        {
            throw CirrusErrorException.VerbConflict(
                $"'{Name}' is a built-in verb and cannot be removed. Built-ins are part of the host.",
                new Dictionary<string, object?> { ["verb"] = Name });
        }

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        if (!RegistryLookup.TryFind(registry, Name, out var match))
        {
            var message = $"No installed extension matches '{Name}' (by package id or short name).";
            if (RegistryLookup.SuggestPackageForToken(registry, Name, out var suggested))
            {
                message += $" '{Name}' is a verb contributed by package '{suggested}'; " +
                           $"to remove that package use '--name {RegistryLookup.ToShortName(suggested)}'.";
            }
            throw CirrusErrorException.ExtensionNotFound(
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
        var installer = new ExtensionInstaller(loggingService);
        var packageDir = Path.Combine(installer.InstallPath, packageId);

        // Registry-first ordering: a partial file-delete leaves us with no orphan registry entry.
        registry.Extensions.Remove(packageId);
        store.Save(registry);

        try
        {
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, recursive: true);
            }
        }
        catch (IOException ex)
        {
            loggingService.Warn($"Removed registry entry for '{packageId}' but failed to delete '{packageDir}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            loggingService.Warn($"Removed registry entry for '{packageId}' but failed to delete '{packageDir}': {ex.Message}");
        }

        return new
        {
            PackageId = packageId,
            Verb = entry.Verb,
            Version = entry.Version,
            Removed = true,
        };
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
}
