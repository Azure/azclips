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

public sealed record SelectedRoutingExtensionCandidate(
    ExtensionRoutingInfo Selected,
    IReadOnlyList<ExtensionRoutingInfo> Ignored,
    ExtensionSelectionReason Reason);

/// <summary>
/// Chooses one extension candidate from those supplied by discovery.
/// </summary>
/// <remarks>
/// Selection rules, in order:
/// <list type="number">
/// <item><description>Use the pinned version if an available candidate matches its version string
/// (ignoring case). If the pin is unavailable, continue with normal selection; callers use
/// <see cref="ReportUnresolvablePin(string, SelectedRoutingExtensionCandidate, ExtensionRegistryEntry?, ILoggingService?)"/>
/// to report the missing pin.</description></item>
/// <item><description>Otherwise, choose the highest valid NuGet version, including prereleases.
/// Build metadata does not affect version precedence. Valid versions take precedence over
/// missing or unparseable versions.</description></item>
/// <item><description>For matching pins or equivalent versions, prefer a candidate with an
/// assembly reference. Among installation references, prefer Bundled before user-installed,
/// then lower root priority, then DLL path and cache identity for a stable tie-break.
/// Installation references take precedence over other assembly references, which are
/// ordered by opaque cache identity.</description></item>
/// </list>
/// Candidates without assembly references are deduplicated and tied by their package, version,
/// and command metadata. Command spelling does not decide between candidates that both have
/// assembly references.
/// <para>
/// These rules apply only to the candidates supplied here. The Bundled loader exposes the first
/// available copy in its ordered fallback roots; a later root does not compete with that copy.
/// Execution uses the selected reference rather than independently selecting another copy.
/// </para>
/// </remarks>
public static class ExtensionCandidateSelector
{
    public static string BuildDiagnostics(
        string context,
        SelectedExtensionCandidate selection,
        ExtensionRegistryEntry? persistedPreference = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return BuildDiagnostics(context, new SelectedRoutingExtensionCandidate(
            ExtensionRoutingInfo.FromDiscovery(selection.Selected),
            selection.Ignored.Select(ExtensionRoutingInfo.FromDiscovery).ToArray(),
            selection.Reason), persistedPreference);
    }

    public static string BuildDiagnostics(
        string context,
        SelectedRoutingExtensionCandidate selection,
        ExtensionRegistryEntry? persistedPreference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        ArgumentNullException.ThrowIfNull(selection);

        var lines = new List<string>
        {
            $"[ExtensionCandidateSelection] context={context} package={selection.Selected.PackageId} reason={selection.Reason} " +
            $"selectedVersion={SafeValue(selection.Selected.ResolvedVersion)} ignored={selection.Ignored.Count} " +
            $"pinned={(persistedPreference?.Pinned == true ? "true" : "false")} requestedVersion={SafeValue(persistedPreference?.Version)}",
            $"  selected: verbs={FormatVerbs(selection.Selected.Verbs)} commands={selection.Selected.Leaves.Count}" +
            $" location={SafeValue(selection.Selected.Location)}",
        };

        foreach (var ignored in selection.Ignored)
        {
            lines.Add($"  ignored: version={SafeValue(ignored.ResolvedVersion)} verbs={FormatVerbs(ignored.Verbs)} commands={ignored.Leaves.Count}" +
                $" location={SafeValue(ignored.Location)}");
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
        ArgumentNullException.ThrowIfNull(selection);
        ReportUnresolvablePin(packageId, new SelectedRoutingExtensionCandidate(
            ExtensionRoutingInfo.FromDiscovery(selection.Selected), [], selection.Reason),
            persistedPreference, loggingService);
    }

    public static void ReportUnresolvablePin(
        string packageId,
        SelectedRoutingExtensionCandidate selection,
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
        var routing = candidates.Select(ExtensionRoutingInfo.FromDiscovery).ToArray();
        var selection = Select(routing, persistedPreference);
        DiscoveredExtensionInfo Original(ExtensionRoutingInfo info)
            => candidates[Array.FindIndex(routing, candidate => ReferenceEquals(candidate, info))];
        return new SelectedExtensionCandidate(
            Original(selection.Selected), selection.Ignored.Select(Original).ToArray(), selection.Reason);
    }

    public static SelectedRoutingExtensionCandidate Select(
        IReadOnlyList<ExtensionRoutingInfo> candidates,
        ExtensionRegistryEntry? persistedPreference = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one candidate is required.", nameof(candidates));
        }

        // Keep distinct references even when they advertise identical commands.
        // Only entries without references are deduplicated by metadata.
        var metadataFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var uniqueCandidates = candidates
            .Where(candidate => candidate.AssemblyReference is not null ||
                metadataFingerprints.Add(BuildDeterministicFingerprint(candidate)))
            .ToList();
        var tieComparer = Comparer<ExtensionRoutingInfo>.Create(CompareReferences);

        if (uniqueCandidates.Count == 1)
        {
            return new SelectedRoutingExtensionCandidate(
                uniqueCandidates[0],
                Array.Empty<ExtensionRoutingInfo>(),
                ExtensionSelectionReason.SingleCandidate);
        }

        // A matching pin takes precedence over newer versions.
        if (persistedPreference?.Pinned == true &&
            !string.IsNullOrWhiteSpace(persistedPreference.Version))
        {
            var pinnedMatches = uniqueCandidates
                .Where(candidate => string.Equals(candidate.ResolvedVersion, persistedPreference.Version, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate, tieComparer)
                .ToList();

            if (pinnedMatches.Count > 0)
            {
                var selected = pinnedMatches[0];
                return new SelectedRoutingExtensionCandidate(
                    selected,
                    GetOrderedIgnored(uniqueCandidates, selected, tieComparer),
                    ExtensionSelectionReason.Pinned);
            }
        }

        // With no matching pin, sort valid versions highest first, then break installation ties.
        var ordered = uniqueCandidates
            .Select(candidate => new CandidateOrdering(
                Candidate: candidate,
                ParsedVersion: NuGetVersion.TryParse(candidate.ResolvedVersion, out var parsedVersion) ? parsedVersion : null))
            .ToList();

        ordered.Sort((left, right) =>
        {
            if (left.ParsedVersion is null && right.ParsedVersion is null)
            {
                return tieComparer.Compare(left.Candidate, right.Candidate);
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

            return tieComparer.Compare(left.Candidate, right.Candidate);
        });

        var highestVersion = ordered[0].ParsedVersion;
        if (highestVersion is not null)
        {
            var highestMatches = ordered
                .Where(item => item.ParsedVersion is not null && VersionComparer.VersionRelease.Compare(item.ParsedVersion, highestVersion) == 0)
                .ToList();

            var selected = highestMatches[0].Candidate;
            return new SelectedRoutingExtensionCandidate(
                selected,
                GetOrderedIgnored(uniqueCandidates, selected, tieComparer),
                highestMatches.Count == 1
                    ? ExtensionSelectionReason.HigherVersion
                    : ExtensionSelectionReason.EquivalentVersion);
        }

        // No candidate has a valid version, so choose using reference tie-breaks alone.
        var equivalent = uniqueCandidates
            .OrderBy(candidate => candidate, tieComparer)
            .ToList();
        return new SelectedRoutingExtensionCandidate(
            equivalent[0],
            GetOrderedIgnored(uniqueCandidates, equivalent[0], tieComparer),
            ExtensionSelectionReason.EquivalentVersion);
    }

    private static IReadOnlyList<ExtensionRoutingInfo> GetOrderedIgnored(
        IEnumerable<ExtensionRoutingInfo> candidates,
        ExtensionRoutingInfo selected,
        IComparer<ExtensionRoutingInfo> tieComparer)
    {
        return candidates
            .Where(candidate => !ReferenceEquals(candidate, selected))
            .OrderBy(candidate => candidate, tieComparer)
            .ToList();
    }

    private static int CompareReferences(ExtensionRoutingInfo left, ExtensionRoutingInfo right)
    {
        var leftReference = left.AssemblyReference;
        var rightReference = right.AssemblyReference;
        if (leftReference is null && rightReference is null)
        {
            return StringComparer.Ordinal.Compare(BuildDeterministicFingerprint(left), BuildDeterministicFingerprint(right));
        }
        if (leftReference is null)
        {
            return 1;
        }
        if (rightReference is null)
        {
            return -1;
        }

        if (leftReference is ExtensionInstallation leftInstallation &&
            rightReference is ExtensionInstallation rightInstallation)
        {
            var comparison = leftInstallation.SourcePriority.CompareTo(rightInstallation.SourcePriority);
            if (comparison != 0)
            {
                return comparison;
            }
            comparison = leftInstallation.RootPriority.CompareTo(rightInstallation.RootPriority);
            if (comparison != 0)
            {
                return comparison;
            }
            var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            comparison = pathComparer.Compare(leftInstallation.Location, rightInstallation.Location);
            if (comparison != 0)
            {
                return comparison;
            }
        }
        else if (leftReference is ExtensionInstallation)
        {
            return -1;
        }
        else if (rightReference is ExtensionInstallation)
        {
            return 1;
        }
        return StringComparer.Ordinal.Compare(leftReference.CacheKey, rightReference.CacheKey);
    }

    private static string BuildDeterministicFingerprint(ExtensionRoutingInfo candidate)
    {
        var verbs = candidate.Verbs
            .Where(static verb => !string.IsNullOrWhiteSpace(verb))
            .Select(static verb => verb.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static verb => verb, StringComparer.Ordinal);

        var commands = candidate.Leaves
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
        ExtensionRoutingInfo Candidate,
        NuGetVersion? ParsedVersion);
}