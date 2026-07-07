// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            var pinnedMatches = candidates
                .Where(candidate => string.Equals(candidate.ResolvedVersion, persistedPreference.Version, StringComparison.OrdinalIgnoreCase))
                .OrderBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
                .ToList();

            if (pinnedMatches.Count > 0)
            {
                var selected = pinnedMatches[0];
                return new SelectedExtensionCandidate(
                    selected,
                    uniqueCandidates.Where(candidate => !ReferenceEquals(candidate, selected)).ToList(),
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
                uniqueCandidates.Where(candidate => !ReferenceEquals(candidate, selected)).ToList(),
                highestMatches.Count == 1
                    ? ExtensionSelectionReason.HigherVersion
                    : ExtensionSelectionReason.EquivalentVersion);
        }

        var equivalent = uniqueCandidates
            .OrderBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
            .ToList();
        return new SelectedExtensionCandidate(
            equivalent[0],
            uniqueCandidates.Where(candidate => !ReferenceEquals(candidate, equivalent[0])).ToList(),
            ExtensionSelectionReason.EquivalentVersion);
    }

    private static string BuildDeterministicFingerprint(DiscoveredExtensionInfo candidate)
    {
        var verbs = candidate.Verbs
            .Where(static verb => !string.IsNullOrWhiteSpace(verb))
            .Select(static verb => verb.Trim())
            .OrderBy(static verb => verb, StringComparer.OrdinalIgnoreCase);

        var commands = candidate.Commands.Keys
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .Select(static command => command.Trim())
            .OrderBy(static command => command, StringComparer.OrdinalIgnoreCase);

        return string.Join("\n", [candidate.PackageId, candidate.ResolvedVersion, string.Join("|", verbs), string.Join("|", commands)]);
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