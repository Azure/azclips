using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;

namespace Core.Agent.Tools.Env;

/// <summary>Builds an <see cref="EnvironmentSnapshot"/> on demand.</summary>
public sealed class EnvironmentProber
{
    private readonly IProfileService _profileService;
    private readonly string _flavor;
    private readonly string? _azclipsVersion;

    public EnvironmentProber(IProfileService profileService, string flavor, string? azclipsVersion = null)
    {
        _profileService = profileService;
        _flavor = flavor;
        _azclipsVersion = azclipsVersion;
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
            AzclipsVersion: _azclipsVersion ?? Safe(GetAzclipsVersion, "unknown"));
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
