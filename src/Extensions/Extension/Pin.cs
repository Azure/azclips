// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

public class PinExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id (full or short suffix) of the extension to pin. Verb and namespace tokens are not accepted.",
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
    Description = @"Package id (full or short suffix) of the extension to unpin. Verb and namespace tokens are not accepted.",
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

public static class PinHelper
{
    public static object SetPinned(string name, bool pinned)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw AzclipsErrorException.UsageError("Name is required.");
        }

        var store = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
        var registry = store.Load();
        var discoveredRegistry = DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences(registry);

        // Resolve against discovered state first. This allows pin/unpin to work even when the
        // package exists on disk but has no preference entry in registry.json.
        if (!RegistryLookup.TryFind(discoveredRegistry, name, out var discoveredMatch))
        {
            var message = $"No installed extension matches '{name}' (by package id or short name).";
            if (RegistryLookup.SuggestPackageForToken(discoveredRegistry, name, out var suggested))
            {
                var verb = pinned ? "pin" : "unpin";
                message += $" '{name}' is a verb contributed by package '{suggested}'; " +
                           $"to {verb} that package use '--name {RegistryLookup.ToShortName(suggested)}'.";
            }
            throw AzclipsErrorException.ExtensionNotFound(
                message,
                new Dictionary<string, object?> { ["name"] = name });
        }

        if (!registry.Extensions.ContainsKey(discoveredMatch.Key))
        {
            registry.Extensions[discoveredMatch.Key] = new ExtensionRegistryEntry
            {
                Version = discoveredMatch.Value.Version,
                Pinned = false,
                Source = discoveredMatch.Value.Source,
                InstalledAt = discoveredMatch.Value.InstalledAt,
            };
        }

        var (packageId, entry, mutated) = ApplyPinChange(registry, discoveredMatch.Key, pinned);
        if (mutated)
        {
            store.Save(registry);
        }
        return Project(packageId, entry, discoveredMatch.Value.Verb);
    }

    /// <summary>
    /// Pure helper: locates the entry matching <paramref name="name"/>, applies the pin change
    /// in place on <paramref name="registry"/>, and returns whether the registry was mutated.
    /// Extracted for unit testing — IO lives in <see cref="SetPinned"/>.
    /// </summary>
    /// <remarks>
    /// The rewritten entry copies <see cref="ExtensionRegistryEntry.Contributions"/> and
    /// <see cref="ExtensionRegistryEntry.Namespaces"/> from the existing entry. Otherwise
    /// <c>NormalizeEntry</c> would synthesize a single <c>(verb, "")</c> contribution from
    /// the legacy <c>Verb</c> field, erasing every non-root leaf.
    /// </remarks>
    public static (string PackageId, ExtensionRegistryEntry Entry, bool Mutated) ApplyPinChange(
        ExtensionRegistry registry,
        string name,
        bool pinned)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!RegistryLookup.TryFind(registry, name, out var match))
        {
            var message = $"No installed extension matches '{name}' (by package id or short name).";
            if (RegistryLookup.SuggestPackageForToken(registry, name, out var suggested))
            {
                var verb = pinned ? "pin" : "unpin";
                message += $" '{name}' is a verb contributed by package '{suggested}'; " +
                           $"to {verb} that package use '--name {RegistryLookup.ToShortName(suggested)}'.";
            }
            throw AzclipsErrorException.ExtensionNotFound(
                message,
                new Dictionary<string, object?> { ["name"] = name });
        }

        var existing = match.Value;
        if (existing.Pinned == pinned)
        {
            // No-op: still return the entry so callers see the current state.
            return (match.Key, existing, false);
        }

        registry.Extensions[match.Key] = new ExtensionRegistryEntry
        {
            Verb = existing.Verb,
            Version = existing.Version,
            Pinned = pinned,
            Source = existing.Source,
            InstalledAt = existing.InstalledAt,
            Contributions = new List<Contribution>(existing.Contributions),
            Namespaces = new List<string>(existing.Namespaces),
        };
        return (match.Key, registry.Extensions[match.Key], true);
    }

    private static object Project(string packageId, ExtensionRegistryEntry entry, string? discoveredVerb = null) => new
    {
        PackageId = packageId,
        Verb = entry.Verb ?? discoveredVerb,
        Version = entry.Version,
        Pinned = entry.Pinned,
        Source = entry.Source,
        InstalledAt = entry.InstalledAt,
    };
}
