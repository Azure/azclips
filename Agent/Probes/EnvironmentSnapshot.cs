namespace Core.Agent.Probes;

/// <summary>Read-only snapshot of host facts injected as system context every turn.</summary>
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
    string CirrusVersion)
{
    public string Format()
    {
        var sb = new System.Text.StringBuilder(512);
        sb.Append("runtime\n");
        sb.Append("  os: ").Append(Os).Append('\n');
        sb.Append("  shell: ").Append(Shell).Append('\n');
        sb.Append("  cwd: ").Append(Cwd).Append('\n');

        sb.Append("interactivity\n");
        sb.Append("  ssh-session: ").Append(SshSession ? "true" : "false").Append('\n');
        sb.Append("  browser-available: ").Append(BrowserAvailable ? "true" : "false").Append('\n');
        sb.Append("  stdin-interactive: ").Append(StdinInteractive ? "true" : "false").Append('\n');

        sb.Append("auth\n");
        sb.Append("  signed-in: ").Append(SignedIn ? "true" : "false").Append('\n');
        sb.Append("  subscription-id: ").Append(SubscriptionId ?? "(none)").Append('\n');
        sb.Append("  tenant-id: ").Append(TenantId ?? "(none)").Append('\n');

        sb.Append("cirrus\n");
        sb.Append("  version: ").Append(CirrusVersion).Append('\n');
        sb.Append("  flavor: ").Append(Flavor).Append('\n');

        return sb.ToString().TrimEnd();
    }

    public string FormatForSystemPrompt()
    {
        return
            "<environment>\n" +
            "Runtime facts the harness probed at the start of this turn. Trust them; do not " +
            "ask the user for anything listed here. Refresh happens automatically each turn — " +
            "after a state-changing command (e.g. `azv2 login`) the next turn's snapshot will " +
            "reflect the new state.\n" +
            Format() + "\n" +
            "</environment>";
    }
}
