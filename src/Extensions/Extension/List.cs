// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class ListExtensionsProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = false,
    Description = @"Regroup output verb-headed: one section per verb, listing every package that contributes to it.",
    SerializedName = @"ByVerb")]
    public bool ByVerb { get; set; }

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
        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        using var _ = telemetryService.StartActivity($"{nameof(ListExtensionsProcessor)}.{nameof(Process)}");

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        // Split the listing into live + stale so the user sees missing-payload entries
        // separately (e.g. when they manually deleted `~/.cirrus/extensions/<pkg>/<ver>/`). Live
        // entries are routable; stale entries are advisory until the user removes them.
        var reconciled = RegistryReconciler.Reconcile(registry, store.InstallRoot);

        var live = reconciled.Live.Extensions
            .OrderBy(kvp => kvp.Value.Verb, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new
            {
                PackageId = kvp.Key,
                Verb = kvp.Value.Verb,
                // Multi-namespace extensions surface every verb they contribute to.
                Namespaces = kvp.Value.Namespaces.Count > 0
                    ? kvp.Value.Namespaces.ToArray()
                    : (kvp.Value.Verb is null ? Array.Empty<string>() : new[] { kvp.Value.Verb }),
                Version = kvp.Value.Version,
                Pinned = kvp.Value.Pinned,
                Source = kvp.Value.Source,
                InstalledAt = kvp.Value.InstalledAt,
                Status = "live",
            })
            .ToList();

        var stale = reconciled.Stale
            .OrderBy(s => s.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(s => new
            {
                PackageId = s.PackageId,
                Version = s.Version,
                ExpectedPath = s.ExpectedPath,
                Reason = s.Reason,
                Status = "stale",
            })
            .ToList();

        if (ByVerb)
        {
            // Regroup live set verb-headed; multi-namespace extensions appear under each verb
            // they contribute to (mirrors the routing table). Stale entries stay separate — not routable.
            var contributors = live.Select(e => new LiveContributor(
                PackageId: e.PackageId,
                Version: e.Version,
                Pinned: e.Pinned,
                Source: e.Source,
                InstalledAt: e.InstalledAt,
                Namespaces: e.Namespaces));

            return new
            {
                ByVerb = PivotByVerb(contributors),
                Stale = stale,
            };
        }

        return new
        {
            Live = live,
            Stale = stale,
        };
    }

    /// <summary>
    /// Pivot helper. Pure projection from <c>Live</c>-shaped contributors to a verb-headed map.
    /// Public so unit tests can exercise it without spinning up DI + the on-disk registry.
    /// Sort order is contract: verbs alphabetical, contributors within a verb alphabetical by
    /// <c>PackageId</c>. Both comparisons are ordinal-ignore-case.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<ContributorRow>> PivotByVerb(
        IEnumerable<LiveContributor> contributors)
    {
        return contributors
            .SelectMany(c => c.Namespaces.Select(verb => new { Verb = verb, Contributor = c }))
            .GroupBy(x => x.Verb, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ContributorRow>)g
                    .Select(x => new ContributorRow(
                        x.Contributor.PackageId,
                        x.Contributor.Version,
                        x.Contributor.Pinned,
                        x.Contributor.Source,
                        x.Contributor.InstalledAt))
                    .OrderBy(r => r.PackageId, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
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

/// <summary>Input row to <see cref="ListExtensionsProcessor.PivotByVerb"/>.</summary>
public sealed record LiveContributor(
    string PackageId,
    string? Version,
    bool Pinned,
    string? Source,
    DateTimeOffset? InstalledAt,
    IReadOnlyList<string> Namespaces);

/// <summary>Output row produced by <see cref="ListExtensionsProcessor.PivotByVerb"/>.</summary>
public sealed record ContributorRow(
    string PackageId,
    string? Version,
    bool Pinned,
    string? Source,
    DateTimeOffset? InstalledAt);
