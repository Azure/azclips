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
    public static SelectedExtensionCandidate Select(
        IReadOnlyList<DiscoveredExtensionInfo> candidates,
        ExtensionRegistryEntry? persistedPreference = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one candidate is required.", nameof(candidates));
        }

        if (candidates.Count == 1)
        {
            return new SelectedExtensionCandidate(
                candidates[0],
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
                    candidates.Where(candidate => !ReferenceEquals(candidate, selected)).ToList(),
                    ExtensionSelectionReason.Pinned);
            }
        }

        var ordered = candidates
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
                candidates.Where(candidate => !ReferenceEquals(candidate, selected)).ToList(),
                highestMatches.Count == 1
                    ? ExtensionSelectionReason.HigherVersion
                    : ExtensionSelectionReason.EquivalentVersion);
        }

        var equivalent = candidates
            .OrderBy(BuildDeterministicFingerprint, StringComparer.Ordinal)
            .ToList();
        return new SelectedExtensionCandidate(
            equivalent[0],
            candidates.Where(candidate => !ReferenceEquals(candidate, equivalent[0])).ToList(),
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

    private sealed record CandidateOrdering(
        DiscoveredExtensionInfo Candidate,
        NuGetVersion? ParsedVersion,
        string Fingerprint);
}