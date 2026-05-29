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
    Description = @"Package id or verb of the extension to remove.",
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
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(RemoveExtensionProcessor)}.{nameof(Process)}");

        // Per spec Q7: built-in verbs are not removable. They aren't in the registry, but the user
        // might still type `extension remove vm` expecting it to work — give a clear error.
        if (BuiltInExtensions.IsBuiltInVerb(Name))
        {
            throw CirrusErrorException.VerbConflict(
                $"'{Name}' is a built-in verb and cannot be removed. Built-ins are part of the host.",
                new Dictionary<string, object?> { ["verb"] = Name });
        }

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        var match = registry.Extensions
            .FirstOrDefault(kvp =>
                string.Equals(kvp.Key, Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Value.Verb, Name, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.EndsWith("." + Name, StringComparison.OrdinalIgnoreCase));

        if (match.Key is null)
        {
            throw CirrusErrorException.ExtensionNotFound(
                $"No installed extension matches '{Name}' (by package id, short name, or verb).",
                new Dictionary<string, object?> { ["name"] = Name });
        }

        var packageId = match.Key;
        var entry = match.Value;

        // Per Q6 strict single-version: there's at most one version on disk, but we always wipe
        // the whole package directory to be safe.
        var installer = new ExtensionInstaller(loggingService);
        var packageDir = Path.Combine(installer.InstallPath, packageId);

        // Update the registry first, then delete files. The reverse order would leave the system
        // with a registry entry pointing at a non-existent folder if the file delete fails midway.
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
