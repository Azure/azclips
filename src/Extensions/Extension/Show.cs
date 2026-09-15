// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

public class ShowExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id (full or short suffix) of the extension to show. Verb and namespace tokens are not accepted.",
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
            throw AzclipsErrorException.UsageError("Name is required to show an extension.");
        }

        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var _ = telemetryService.StartActivity($"{nameof(ShowExtensionProcessor)}.{nameof(Process)}");

        var store = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
        var fileRegistry = store.Load();
        var registry = DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences(fileRegistry);

        // Match on package identifier only (full package id or short suffix). Verb/namespace
        // tokens are intentionally rejected — see RegistryLookup remarks.
        if (!RegistryLookup.TryFind(registry, Name, out var match))
        {
            var message = $"No installed extension matches '{Name}' (by package id or short name).";
            if (RegistryLookup.SuggestPackageForToken(registry, Name, out var suggested))
            {
                message += $" '{Name}' is a verb contributed by package '{suggested}'; " +
                           $"to act on that package use '--name {RegistryLookup.ToShortName(suggested)}'.";
            }
            throw AzclipsErrorException.ExtensionNotFound(
                message,
                new Dictionary<string, object?> { ["name"] = Name });
        }

        var entry = match.Value;
        return ShowExtensionProcessor.BuildResult(match.Key, entry);
    }

    /// <summary>
    /// Pure projection helper for <c>extension show</c>. Surfaces v2
    /// <see cref="ExtensionRegistryEntry.Namespaces"/> alongside the legacy single
    /// <see cref="ExtensionRegistryEntry.Verb"/>; falls back to the singleton verb when
    /// <c>Namespaces</c> is empty (legacy v1 entries) so the field is always populated.
    /// </summary>
    public static object BuildResult(string packageId, ExtensionRegistryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        ArgumentNullException.ThrowIfNull(entry);

        var namespaces = entry.Namespaces.Count > 0
            ? (IReadOnlyList<string>)entry.Namespaces
            : (entry.Verb is null ? Array.Empty<string>() : new[] { entry.Verb });
        return new
        {
            PackageId = packageId,
            Verb = entry.Verb,
            Namespaces = namespaces,
            Version = entry.Version,
            Pinned = entry.Pinned,
            Source = entry.Source,
            InstalledAt = entry.InstalledAt,
            Location = entry.Location,
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
