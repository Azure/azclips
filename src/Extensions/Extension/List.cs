// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Configuration.Options;
using Core.DependencyInjection;
using Core.Extensions;
using NuGet.Versioning;

namespace Microsoft.Azure.Azclips.Extensions.Extension.Processors;

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
        var telemetryService = AzclipsServiceProvider.GetRequiredService<ITelemetryService>();
        using var _ = telemetryService.StartActivity($"{nameof(ListExtensionsProcessor)}.{nameof(Process)}");

        var store = AzclipsServiceProvider.GetRequiredService<IExtensionRegistryStore>();
        var fileRegistry = store.Load();
        var (discoveredRegistry, selections) = DiscoveryBackedExtensionRegistry.BuildDiscoveredWithCandidates(fileRegistry);
        var installedRoot = AzclipsServiceProvider.GetRequiredService<IConfigurationService>()
            .GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot;

        var discoveredExtensions = discoveredRegistry.Extensions
            .OrderBy(kvp => kvp.Value.Verb, StringComparer.OrdinalIgnoreCase)
            .Select(kvp =>
            {
                var packageId = kvp.Key;
                var entry = kvp.Value;
                selections.TryGetValue(packageId, out var selection);
                var candidates = BuildCandidates(selection, entry, installedRoot);

                return new
                {
                    PackageId = packageId,
                    Verb = entry.Verb,
                    // Multi-namespace extensions surface every verb they contribute to.
                    Namespaces = entry.Namespaces.Count > 0
                        ? entry.Namespaces.ToArray()
                        : (entry.Verb is null ? Array.Empty<string>() : new[] { entry.Verb }),
                    Version = entry.Version,
                    SelectedVersion = entry.Version,
                    Pinned = entry.Pinned,
                    Source = entry.Source,
                    InstalledAt = entry.InstalledAt,
                    Location = entry.Location,
                    Candidates = candidates,
                };
            })
            .ToList();

        if (ByVerb)
        {
            // Regroup discovered contributors verb-headed for stable --by-verb output.
            var contributors = discoveredExtensions.Select(e => new LiveContributor(
                PackageId: e.PackageId,
                Version: e.Version,
                SelectedVersion: e.SelectedVersion,
                Pinned: e.Pinned,
                Source: e.Source,
                InstalledAt: e.InstalledAt,
                Location: e.Location,
                Namespaces: e.Namespaces,
                Candidates: e.Candidates));

            return PivotByVerb(contributors);
        }

        return discoveredExtensions;
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
                        x.Contributor.SelectedVersion,
                        x.Contributor.Pinned,
                        x.Contributor.Source,
                        x.Contributor.InstalledAt,
                        x.Contributor.Location,
                        x.Contributor.Candidates))
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

    /// <summary>
    /// Projects the Selected + Ignored candidates from <paramref name="selection"/> into the
    /// list-output shape and sorts them by SemVer descending. Falls back to a single-element
    /// array derived from <paramref name="entry"/> when the selection is unavailable.
    /// </summary>
    public static IReadOnlyList<CandidateInfo> BuildCandidates(
        SelectedExtensionCandidate? selection,
        ExtensionRegistryEntry entry,
        string installedRoot)
    {
        if (selection is null)
        {
            return [new CandidateInfo(entry.Version, InferTier(entry.Location, installedRoot), entry.Location)];
        }

        return selection.Ignored
            .Prepend(selection.Selected)
            .Select(info => new CandidateInfo(info.ResolvedVersion, InferTier(info.Location, installedRoot), info.Location))
            .OrderByDescending(c => c, CandidateInfoVersionComparer.Instance)
            .ToList();
    }

    /// <summary>
    /// Classifies a candidate's tier by comparing its <paramref name="location"/> against the
    /// configured Installed root. Anything not under the Installed root is treated as Bundled
    /// (the only other loader in play after CLIPS-622).
    /// </summary>
    public static string InferTier(string? location, string installedRoot)
    {
        if (string.IsNullOrEmpty(location) || string.IsNullOrEmpty(installedRoot))
        {
            return "Bundled";
        }

        try
        {
            var normalizedLocation = Path.GetFullPath(location);
            var normalizedRoot = Path.GetFullPath(installedRoot);
            // Append a separator so the prefix check treats the root as a directory boundary and
            // does not conflate '.../extensions' with '.../extensions-old'.
            if (!normalizedRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                normalizedRoot += Path.DirectorySeparatorChar;
            }
            return normalizedLocation.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? "Installed"
                : "Bundled";
        }
        catch
        {
            return "Bundled";
        }
    }

    private sealed class CandidateInfoVersionComparer : IComparer<CandidateInfo>
    {
        public static readonly CandidateInfoVersionComparer Instance = new();

        public int Compare(CandidateInfo? x, CandidateInfo? y)
        {
            if (ReferenceEquals(x, y)) { return 0; }
            if (x is null) { return -1; }
            if (y is null) { return 1; }

            var xParsed = NuGetVersion.TryParse(x.Version, out var xv);
            var yParsed = NuGetVersion.TryParse(y.Version, out var yv);

            if (xParsed && yParsed) { return xv!.CompareTo(yv); }
            if (xParsed) { return 1; }
            if (yParsed) { return -1; }
            // Both unparseable (e.g. Bundled entries with no .nupkg and no attribute); fall back
            // to ordinal string compare so ordering stays deterministic.
            return string.Compare(x.Version, y.Version, StringComparison.Ordinal);
        }
    }
}

/// <summary>Input row to <see cref="ListExtensionsProcessor.PivotByVerb"/>.</summary>
public sealed record LiveContributor(
    string PackageId,
    string? Version,
    string? SelectedVersion,
    bool Pinned,
    string? Source,
    DateTimeOffset? InstalledAt,
    string? Location,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<CandidateInfo> Candidates);

/// <summary>Output row produced by <see cref="ListExtensionsProcessor.PivotByVerb"/>.</summary>
public sealed record ContributorRow(
    string PackageId,
    string? Version,
    string? SelectedVersion,
    bool Pinned,
    string? Source,
    DateTimeOffset? InstalledAt,
    string? Location,
    IReadOnlyList<CandidateInfo> Candidates);

/// <summary>
/// One discovered candidate for a package. Multiple candidates surface when the same package
/// appears in both the Bundled and Installed tiers, or across multiple installed versions.
/// </summary>
public sealed record CandidateInfo(
    string? Version,
    string Tier,
    string? Location);
