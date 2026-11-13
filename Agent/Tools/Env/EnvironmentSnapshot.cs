namespace Core.Agent.Tools.Env;

/// <summary>Read-only snapshot of host facts. Serialized by <c>az agent tool env show</c>
/// as JSON; the agent host reads that JSON and reformats it into the
/// <c>&lt;environment&gt;</c> block via
/// <c>Core.Agent.Common.EnvSnapshotFormatter</c>.</summary>
public sealed record EnvironmentSnapshot(
    string Os,
    string Shell,
    string Cwd,
    bool SshSession,
    bool BrowserAvailable,
    bool StdinInteractive,
    bool SignedIn,
    string? SubscriptionId,
    string? TenantId,
    string Flavor,
    string AzclipsVersion);
