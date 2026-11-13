using System.Text;
using System.Text.Json;

namespace Core.Agent.Common;

/// <summary>
/// Formats the JSON emitted by <c>az agent tool env show</c> into the
/// <c>&lt;environment&gt;...&lt;/environment&gt;</c> block injected as system
/// context every turn. Kept alongside the SDK-hook wire-up (not in the shared
/// tool library) because only the agent host consumes it — the CLI processor
/// emits the JSON verbatim so scripts / other tooling can parse it structurally.
/// </summary>
internal static class EnvSnapshotFormatter
{
    public static string Format(string envJson)
    {
        // A provider failure surfaces as a JSON payload with an "error" field.
        // Injecting that as "environment" would confuse the
        // model; emit an empty block and let the agent proceed without env facts.
        try
        {
            using var doc = JsonDocument.Parse(envJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return string.Empty;
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) return string.Empty;

            var sb = new StringBuilder(512);
            sb.Append("<environment>\n");
            sb.Append("Runtime facts the harness probed at the start of this turn. Trust them; do not ");
            sb.Append("ask the user for anything listed here. Refresh happens automatically each turn — ");
            sb.Append("after a state-changing command (e.g. `az login`) the next turn's snapshot will ");
            sb.Append("reflect the new state.\n");

            sb.Append("runtime\n");
            sb.Append("  os: ").Append(GetString(root, "os", "unknown")).Append('\n');
            sb.Append("  shell: ").Append(GetString(root, "shell", "unknown")).Append('\n');
            sb.Append("  cwd: ").Append(GetString(root, "cwd", "(unknown)")).Append('\n');

            sb.Append("interactivity\n");
            sb.Append("  ssh-session: ").Append(GetBool(root, "sshSession") ? "true" : "false").Append('\n');
            sb.Append("  browser-available: ").Append(GetBool(root, "browserAvailable") ? "true" : "false").Append('\n');
            sb.Append("  stdin-interactive: ").Append(GetBool(root, "stdinInteractive") ? "true" : "false").Append('\n');

            sb.Append("auth\n");
            sb.Append("  signed-in: ").Append(GetBool(root, "signedIn") ? "true" : "false").Append('\n');
            sb.Append("  subscription-id: ").Append(GetString(root, "subscriptionId", "(none)")).Append('\n');
            sb.Append("  tenant-id: ").Append(GetString(root, "tenantId", "(none)")).Append('\n');

            sb.Append("azclips\n");
            sb.Append("  version: ").Append(GetString(root, "azclipsVersion", "unknown")).Append('\n');
            sb.Append("  flavor: ").Append(GetString(root, "flavor", "unknown")).Append('\n');

            var groups = GetStrings(root, "commandGroups");
            if (groups.Count > 0)
            {
                sb.Append("command-groups (installed; not runnable by themselves)\n");
                sb.Append("  ").Append(string.Join(", ", groups)).Append('\n');
            }

            if (root.TryGetProperty("installableGroups", out var installableElement)
                && installableElement.ValueKind == JsonValueKind.Array)
            {
                var installable = GetInstallableGroups(root);
                sb.Append("installable-command-groups (not installed, not runnable by themselves; group (extension package))\n");
                sb.Append("  ").Append(installable.Count > 0 ? string.Join(", ", installable) : "none").Append('\n');
            }

            sb.Append("</environment>");
            return sb.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    // True when the snapshot does not clearly report signed-in: a missing/false
    // signedIn field counts as signed-out so the login-help pre-fetch still fires.
    // Only a parse failure or an explicit error payload returns false (skip the
    // pre-fetch) — it's an optimization, not a correctness gate.
    public static bool IsSignedOut(string envJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(envJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out _)) return false;
            return !GetBool(root, "signedIn");
        }
        catch
        {
            return false;
        }
    }

    private static string GetString(JsonElement root, string name, string fallback)
    {
        if (!root.TryGetProperty(name, out var el)) return fallback;
        if (el.ValueKind == JsonValueKind.Null) return fallback;
        return el.ValueKind == JsonValueKind.String ? (el.GetString() ?? fallback) : fallback;
    }

    private static List<string> GetStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return el.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .OfType<string>()
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    // Entries of installableGroups as "group (package)".
    private static List<string> GetInstallableGroups(JsonElement root)
    {
        if (!root.TryGetProperty("installableGroups", out var el) || el.ValueKind != JsonValueKind.Array) return [];
        return el.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.Object)
            .Select(e => (Group: GetString(e, "group", ""), Package: GetString(e, "packageId", "")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Group) && !string.IsNullOrWhiteSpace(x.Package))
            .Select(x => $"{x.Group} ({x.Package})")
            .ToList();
    }

    private static bool GetBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.True;
}
