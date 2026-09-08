// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using NuGet.Versioning;

namespace Core.Extensions;

public enum ExtensionSelectionReason
{
    SingleCandidate,
    Pinned,
    HigherVersion,
    EquivalentVersion,
}

public sealed record SelectedExtensionCandidate(
    DiscoveredExtensionInfo Selected,
    IReadOnlyList<DiscoveredExtensionInfo> Ignored,
    ExtensionSelectionReason Reason);

public static class ExtensionCandidateSelector
{
    public static string BuildDiagnostics(
        string context,
        SelectedExtensionCandidate selection,
        ExtensionRegistryEntry? persistedPreference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentNullException.ThrowIfNull(selection);

        var lines = new List<string>
        {
            $"[ExtensionCandidateSelection] context={context} package={selection.Selected.PackageId} reason={selection.Reason} " +
            $"selectedVersion={SafeValue(selection.Selected.ResolvedVersion)} ignored={selection.Ignored.Count} " +
            $"pinned={(persistedPreference?.Pinned == true ? "true" : "false")} requestedVersion={SafeValue(persistedPreference?.Version)}",
            $"  selected: verbs={FormatVerbs(selection.Selected.Verbs)} commands={selection.Selected.Commands.Count}",
        };

        foreach (var ignored in selection.Ignored)
        {
            lines.Add($"  ignored: version={SafeValue(ignored.ResolvedVersion)} verbs={FormatVerbs(ignored.Verbs)} commands={ignored.Commands.Count}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Surfaces a user-visible diagnostic when the file registry pins a specific version of an
    /// extension but that version is not present in the discovered candidate set. The selector
    /// silently falls back to the highest available version
    /// (see <c>ExtensionCandidateSelectorTests.P9_UnresolvedPinFallsBackToEquivalentSelection</c>);
    /// without this diagnostic the drift is invisible to the user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Called from both selector consumers: <c>CliExtensionServiceRegistration.SelectInstalledCandidates</c>
    /// (drives dispatch's <c>LeafIndex</c>) and <c>DiscoveryBackedExtensionRegistry.BuildDiscoveredWithPreferences</c>
    /// (drives <c>az extension list</c>/<c>show</c>/<c>pin</c>). Keeping the diagnostic in one place ensures
    /// both surfaces report the same drift.
    /// </para>
    /// </remarks>
    public static void ReportUnresolvablePin(
        string packageId,
        SelectedExtensionCandidate selection,
        ExtensionRegistryEntry? persistedPreference,
        ILoggingService? loggingService)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(selection);

        if (persistedPreference is not { Pinned: true } ||
            string.IsNullOrWhiteSpace(persistedPreference.Version) ||
            string.Equals(persistedPreference.Version, selection.Selected.ResolvedVersion, StringComparison.OrdinalIgnoreCase))
        {
            // Not pinned, no pin target, OR the selected candidate already matches the pin —
            // the latter can happen via `SingleCandidate` (the sole candidate happens to be at
            // the pinned version) as well as via the explicit `Pinned` reason.
            return;
        }

        var requestedVersion = persistedPreference.Version;
        var selectedVersion = string.IsNullOrWhiteSpace(selection.Selected.ResolvedVersion)
            ? "(unversioned)"
            : selection.Selected.ResolvedVersion;

        loggingService?.Error(
            $"[Pinned extension] Requested version '{requestedVersion}' of '{packageId}' is not " +
            $"installed. Falling back to '{selectedVersion}'. Run 'az extension unpin {packageId}' " +
            "or reinstall the pinned version to restore.");

        Activity.Current?.AddEvent(new ActivityEvent(
            "extension.pin.unresolvable",
            tags: new ActivityTagsCollection
            {
                ["extension.package_id"] = packageId,
                ["extension.pinned_version"] = requestedVersion,
                ["extension.selected_version"] = selection.Selected.ResolvedVersion ?? string.Empty,
                ["extension.selection_reason"] = selection.Reason.ToString(),
            }));
    }

    public static SelectedExtensionCandidate Select(
        IReadOnlyList<DiscoveredExtensionInfo> candidates,
        ExtensionRegistryEntry? persistedPreference = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one candidate is required.", nameof(candidates));
        }

        var uniqueCandidates = candidates
            .GroupBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToList();

        if (uniqueCandidates.Count == 1)
        {
            return new SelectedExtensionCandidate(
                uniqueCandidates[0],
                Array.Empty<DiscoveredExtensionInfo>(),
                ExtensionSelectionReason.SingleCandidate);
        }

        if (persistedPreference?.Pinned == true &&
            !string.IsNullOrWhiteSpace(persistedPreference.Version))
        {
            var pinnedMatches = uniqueCandidates
                .Where(candidate => string.Equals(candidate.ResolvedVersion, persistedPreference.Version, StringComparison.OrdinalIgnoreCase))
                .OrderBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
                .ToList();

            if (pinnedMatches.Count > 0)
            {
                var selected = pinnedMatches[0];
                return new SelectedExtensionCandidate(
                    selected,
                    GetOrderedIgnored(uniqueCandidates, selected),
                    ExtensionSelectionReason.Pinned);
            }
        }

        var ordered = uniqueCandidates
            .Select(candidate => new CandidateOrdering(
                Candidate: candidate,
                ParsedVersion: NuGetVersion.TryParse(candidate.ResolvedVersion, out var parsedVersion) ? parsedVersion : null,
                Fingerprint: BuildDeterministicFingerprint(candidate)))
            .ToList();

        ordered.Sort(static (left, right) =>
        {
            if (left.ParsedVersion is null && right.ParsedVersion is null)
            {
                return StringComparer.Ordinal.Compare(left.Fingerprint, right.Fingerprint);
            }

            if (left.ParsedVersion is null)
            {
                return 1;
            }

            if (right.ParsedVersion is null)
            {
                return -1;
            }

            var versionComparison = VersionComparer.VersionRelease.Compare(right.ParsedVersion, left.ParsedVersion);
            if (versionComparison != 0)
            {
                return versionComparison;
            }

            return StringComparer.Ordinal.Compare(left.Fingerprint, right.Fingerprint);
        });

        var highestVersion = ordered[0].ParsedVersion;
        if (highestVersion is not null)
        {
            var highestMatches = ordered
                .Where(item => item.ParsedVersion is not null && VersionComparer.VersionRelease.Compare(item.ParsedVersion, highestVersion) == 0)
                .ToList();

            var selected = highestMatches[0].Candidate;
            return new SelectedExtensionCandidate(
                selected,
                GetOrderedIgnored(uniqueCandidates, selected),
                highestMatches.Count == 1
                    ? ExtensionSelectionReason.HigherVersion
                    : ExtensionSelectionReason.EquivalentVersion);
        }

        var equivalent = uniqueCandidates
            .OrderBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
            .ToList();
        return new SelectedExtensionCandidate(
            equivalent[0],
            GetOrderedIgnored(uniqueCandidates, equivalent[0]),
            ExtensionSelectionReason.EquivalentVersion);
    }

    private static IReadOnlyList<DiscoveredExtensionInfo> GetOrderedIgnored(
        IEnumerable<DiscoveredExtensionInfo> candidates,
        DiscoveredExtensionInfo selected)
    {
        return candidates
            .Where(candidate => !ReferenceEquals(candidate, selected))
            .OrderBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildDeterministicFingerprint(DiscoveredExtensionInfo candidate)
    {
        var verbs = candidate.Verbs
            .Where(static verb => !string.IsNullOrWhiteSpace(verb))
            .Select(static verb => verb.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static verb => verb, StringComparer.Ordinal);

        var commands = candidate.Commands.Keys
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .Select(static command => command.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static command => command, StringComparer.Ordinal);

        return string.Join("\n", [
            NormalizeToken(candidate.PackageId),
            NormalizeVersion(candidate.ResolvedVersion),
            string.Join("|", verbs),
            string.Join("|", commands)]);
    }

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return string.Empty;
        }

        var trimmed = version.Trim();
        if (!NuGetVersion.TryParse(trimmed, out var parsedVersion))
        {
            return trimmed.ToLowerInvariant();
        }

        return parsedVersion.ToNormalizedString().ToLowerInvariant();
    }

    private static string NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static string FormatVerbs(IReadOnlyList<string> verbs)
    {
        var normalized = verbs
            .Where(static verb => !string.IsNullOrWhiteSpace(verb))
            .Select(static verb => verb.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static verb => verb, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? "-" : string.Join(",", normalized);
    }

    private static string SafeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private sealed record CandidateOrdering(
        DiscoveredExtensionInfo Candidate,
        NuGetVersion? ParsedVersion,
        string Fingerprint);
}