using System.Reflection;
using System.Runtime.InteropServices;
using Core.Abstractions;

namespace Core.Agent.Probes;

/// <summary>Builds an <see cref="EnvironmentSnapshot"/> on demand.</summary>
public sealed class EnvironmentProber
{
    private readonly IAuthContextService _authContext;
    private readonly string _flavor;

    public EnvironmentProber(IAuthContextService authContext, string flavor)
    {
        _authContext = authContext;
        _flavor = flavor;
    }

    public async Task<EnvironmentSnapshot> ProbeAsync(CancellationToken ct = default)
    {
        var os = Safe(() => RuntimeInformation.OSDescription, "unknown");
        var shell = Safe(EnvironmentProbes.DetectShell, "unknown");
        var cwd = Safe(Directory.GetCurrentDirectory, "(unknown)");
        var sshSession = Safe(EnvironmentProbes.IsSshSession, false);
        var stdinInteractive = Safe(() => !Console.IsInputRedirected, false);
        var browserAvailable = Safe(() => EnvironmentProbes.IsBrowserAvailable(sshSession), false);

        AuthContext? ctx = null;
        try { ctx = await _authContext.GetCurrentContextAsync(ct).ConfigureAwait(false); } catch { }

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
            CirrusVersion: Safe(GetCirrusVersion, "unknown"));
    }

    private static string GetCirrusVersion()
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
