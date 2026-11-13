using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Core.Abstractions;
using Core.Extensions;

namespace Core.Agent.Tools.Env;

/// <summary>Builds an <see cref="EnvironmentSnapshot"/> on demand.</summary>
public sealed class EnvironmentProber
{
    private readonly IProfileService _profileService;
    private readonly string _flavor;
    private readonly string? _azclipsVersion;
    private readonly Func<CancellationToken, Task<string>>? _readCommandCatalog;
    private readonly ExtensionHintTable _extensionHints;

    /// <param name="readCommandCatalog">Returns the HelpIndex catalog JSON of the installed
    /// CLI and extensions; its root groups become <see cref="EnvironmentSnapshot.CommandGroups"/>.</param>
    /// <param name="extensionHints">Map of first-party extension commands to package ids; its
    /// verbs that are not installed become <see cref="EnvironmentSnapshot.InstallableGroups"/>.
    /// Defaults to <see cref="ExtensionHintTable.Default"/>.</param>
    public EnvironmentProber(
        IProfileService profileService,
        string flavor,
        string? azclipsVersion = null,
        Func<CancellationToken, Task<string>>? readCommandCatalog = null,
        ExtensionHintTable? extensionHints = null)
    {
        _profileService = profileService;
        _flavor = flavor;
        _azclipsVersion = azclipsVersion;
        _readCommandCatalog = readCommandCatalog;
        _extensionHints = extensionHints ?? ExtensionHintTable.Default;
    }

    public async Task<EnvironmentSnapshot> ProbeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var os = Safe(() => RuntimeInformation.OSDescription, "unknown");
        var shell = Safe(EnvironmentProbes.DetectShell, "unknown");
        // The CLI launches this agent process with its working directory set to
        // the agent install dir (so relative additionalProbingPaths resolve), and
        // forwards the directory the user actually ran `az` from via
        // AZCLIPS_CALLER_CWD. Prefer that; fall back to the process cwd for the
        // dev-loop / direct-launch case where the env var isn't set.
        var cwd = Safe(() =>
        {
            // Name must match AgentLauncher.CallerCwdEnvVar on the CLI side (kept as a
            // literal here rather than a reference, since the agent must not take
            // a dependency on the CLI project). If one side is renamed, rename both.
            var callerCwd = Environment.GetEnvironmentVariable("AZCLIPS_CALLER_CWD");
            return !string.IsNullOrEmpty(callerCwd) ? callerCwd : Directory.GetCurrentDirectory();
        }, "(unknown)");
        var sshSession = Safe(EnvironmentProbes.IsSshSession, false);
        var stdinInteractive = Safe(() => !Console.IsInputRedirected, false);
        var browserAvailable = Safe(() => EnvironmentProbes.IsBrowserAvailable(sshSession), false);

        var ctx = await _profileService.GetActiveProfileAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var commandGroups = await ReadCommandGroupsAsync(ct).ConfigureAwait(false);
        var installableGroups = commandGroups is null ? null : ListInstallableGroups(_extensionHints, commandGroups);

        var signedIn = !string.IsNullOrEmpty(ctx?.SubscriptionId);
        return new EnvironmentSnapshot(
            Os: os,
            Shell: shell,
            Cwd: cwd,
            SshSession: sshSession,
            BrowserAvailable: browserAvailable,
            StdinInteractive: stdinInteractive,
            SignedIn: signedIn,
            SubscriptionId: string.IsNullOrEmpty(ctx?.SubscriptionId) ? null : ctx!.SubscriptionId,
            TenantId: string.IsNullOrEmpty(ctx?.TenantId) ? null : ctx!.TenantId,
            Flavor: _flavor,
            AzclipsVersion: _azclipsVersion ?? Safe(GetAzclipsVersion, "unknown"),
            CommandGroups: commandGroups,
            InstallableGroups: installableGroups);
    }

    // Root group verbs from the help catalog; null when they are unknown: no reader, an error
    // payload, or a catalog that cannot be parsed. The reader returns expected read failures as
    // an error payload; an exception it throws propagates.
    private async Task<IReadOnlyList<string>?> ReadCommandGroupsAsync(CancellationToken ct)
    {
        if (_readCommandCatalog is null) return null;
        var json = await _readCommandCatalog(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        return Safe(() => ParseGroupVerbs(json), null);
    }

    // Verbs (first token of each hint key) that are not installed, with their package ids.
    private static IReadOnlyList<InstallableGroup> ListInstallableGroups(
        ExtensionHintTable hints, IReadOnlyList<string> installedGroups)
    {
        var installed = new HashSet<string>(installedGroups, StringComparer.OrdinalIgnoreCase);
        return hints.Leaves
            .Select(e => new InstallableGroup(
                e.Key.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                e.Value.PackageId))
            .Where(g => g.Group.Length > 0 && !string.IsNullOrWhiteSpace(g.PackageId))
            .Where(g => !installed.Contains(g.Group))
            .DistinctBy(g => (g.Group.ToLowerInvariant(), g.PackageId.ToLowerInvariant()))
            .OrderBy(g => g.Group, StringComparer.Ordinal)
            .ThenBy(g => g.PackageId, StringComparer.Ordinal)
            .ToArray();
    }

    // Null for an error payload or any document without a groups array.
    private static IReadOnlyList<string>? ParseGroupVerbs(string catalogJson)
    {
        using var doc = JsonDocument.Parse(catalogJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            !doc.RootElement.TryGetProperty("groups", out var groups) ||
            groups.ValueKind != JsonValueKind.Array)
            return null;
        return groups.EnumerateArray()
            .Where(g => g.ValueKind == JsonValueKind.Object)
            .Select(g => g.TryGetProperty("verb", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null)
            .OfType<string>()
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetAzclipsVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? typeof(EnvironmentProber).Assembly;
        var v = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm.GetName().Version?.ToString();
        return string.IsNullOrEmpty(v) ? "unknown" : v!;
    }

    private static T Safe<T>(Func<T> f, T fallback)
    {
        try { return f(); } catch { return fallback; }
    }
}
