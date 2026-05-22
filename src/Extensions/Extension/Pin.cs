using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class PinExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id or verb of the extension to pin.",
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

    public object Process() => PinHelper.SetPinned(Name, pinned: true);

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

public class UnpinExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id or verb of the extension to unpin.",
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

    public object Process() => PinHelper.SetPinned(Name, pinned: false);

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

internal static class PinHelper
{
    public static object SetPinned(string name, bool pinned)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw CirrusErrorException.UsageError("Name is required.");
        }

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        var match = registry.Extensions
            .FirstOrDefault(kvp =>
                string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Value.Verb, name, StringComparison.OrdinalIgnoreCase));

        if (match.Key is null)
        {
            throw CirrusErrorException.ExtensionNotFound(
                $"No installed extension matches '{name}' (by package id or verb).",
                new Dictionary<string, object?> { ["name"] = name });
        }

        var packageId = match.Key;
        var existing = match.Value;

        if (existing.Pinned == pinned)
        {
            // No-op: still return the entry so callers see the current state.
            return Project(packageId, existing);
        }

        registry.Extensions[packageId] = new ExtensionRegistryEntry
        {
            Verb = existing.Verb,
            Version = existing.Version,
            Pinned = pinned,
            Source = existing.Source,
            InstalledAt = existing.InstalledAt,
        };
        store.Save(registry);

        return Project(packageId, registry.Extensions[packageId]);
    }

    private static object Project(string packageId, ExtensionRegistryEntry entry) => new
    {
        PackageId = packageId,
        Verb = entry.Verb,
        Version = entry.Version,
        Pinned = entry.Pinned,
        Source = entry.Source,
        InstalledAt = entry.InstalledAt,
    };
}
