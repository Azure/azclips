using System.Globalization;

namespace Live.Tool;

/// <summary>Thin wrapper over the Azure CLI, which has no usable .NET equivalent here.</summary>
internal sealed class AzCli
{
    /// <summary>Run <c>az ... -o tsv</c> and return the trimmed value.</summary>
    public string Tsv(params string[] args)
    {
        var r = Proc.Run("az", [.. args, "-o", "tsv"]);
        return r.Ok ? r.Out : string.Empty;
    }

    /// <summary>Run <c>az ... -o tsv</c>, throwing when it fails.</summary>
    public string TsvChecked(params string[] args) => Proc.Check("az", [.. args, "-o", "tsv"]);

    /// <summary>Run <c>az</c> for its side effect, discarding output.</summary>
    public ProcResult None(params string[] args) => Proc.Run("az", [.. args, "-o", "none"]);

    public ProcResult NoneChecked(params string[] args)
    {
        var r = None(args);
        if (!r.Ok)
        {
            var detail = string.IsNullOrWhiteSpace(r.Stderr) ? r.Stdout : r.Stderr;
            throw new LiveToolException($"az {string.Join(' ', args)} failed (exit {r.ExitCode}).\n{detail.Trim()}");
        }

        return r;
    }

    public bool Installed => Proc.Exists("az");
}

/// <summary>
/// The signed-in Azure context, resolved once and then treated as the single source of
/// truth. Port of <c>require_az_context</c>.
/// </summary>
internal sealed record AzContext(
    string TenantId,
    string SubscriptionId,
    string SubscriptionName,
    string State = "")
{
    /// <summary>
    /// The environment is identified by subscription <b>name</b>, not ID, so each developer
    /// can run against their own subscription without editing anything.
    /// <para>
    /// Name, not ID, on purpose: these tenants are rotated periodically, and the
    /// replacement contains a subscription with the same name but a new ID. Matching on
    /// name means a developer only has to sign in to the new tenant; matching on ID would
    /// need a code change every rotation.
    /// </para>
    /// </summary>
    public const string ExpectedSubscriptionName = "Azure CLI/PWSH";

    /// <summary>
    /// Assert the CLI is installed, signed in, and pointed at the expected subscription.
    /// Call FIRST: everything after either costs minutes or fails obscurely.
    /// </summary>
    public static AzContext Require(AzCli az, string? expectedName = null)
    {
        expectedName ??= ExpectedSubscriptionName;

        if (!az.Installed)
        {
            throw new LiveToolException("The Azure CLI (az) is not installed, or is not on PATH.");
        }

        // One az call, not three: each invocation costs ~1s under WSL interop.
        var account = az.Tsv("account", "show", "--query", "[tenantId,id,name,state]");
        if (string.IsNullOrWhiteSpace(account))
        {
            throw new LiveToolException(
                "Not signed in to Azure. Run:\n"
                + "     az login\n"
                + $"   then select the '{expectedName}' subscription.");
        }

        // `-o tsv` returns a list query one value per line, not tab-separated.
        var fields = account.Split('\n', StringSplitOptions.TrimEntries);
        var ctx = new AzContext(
            fields.ElementAtOrDefault(0) ?? string.Empty,
            fields.ElementAtOrDefault(1) ?? string.Empty,
            fields.ElementAtOrDefault(2) ?? string.Empty,
            fields.ElementAtOrDefault(3) ?? string.Empty);

        if (!string.Equals(ctx.SubscriptionName, expectedName, StringComparison.Ordinal))
        {
            throw new LiveToolException(
                "Azure CLI is pointed at the wrong subscription.\n"
                + $"   Expected: {expectedName}\n"
                + $"   Current:  {ctx.SubscriptionName} ({ctx.SubscriptionId})\n"
                + SuggestSwitch(az, expectedName));
        }

        // The old tenant is disabled before it disappears, and its subscription stays
        // selected and still matches by name. Every later call would fail with an opaque
        // authorization error, so say plainly that a new tenant must be used.
        if (ctx.State.Length > 0 && !string.Equals(ctx.State, "Enabled", StringComparison.Ordinal))
        {
            throw new LiveToolException(
                $"Subscription '{ctx.SubscriptionName}' ({ctx.SubscriptionId}) is {ctx.State}, not Enabled.\n"
                + "   These tenants are rotated periodically; the old one is disabled and replaced\n"
                + "   by a new tenant containing a subscription of the same name.\n"
                + SuggestSwitch(az, expectedName));
        }

        if (string.IsNullOrEmpty(ctx.TenantId) || string.IsNullOrEmpty(ctx.SubscriptionId))
        {
            throw new LiveToolException("Could not resolve tenant/subscription ID from 'az account show'.");
        }

        return ctx;
    }

    /// <summary>
    /// Build a switch instruction that actually works.
    /// <para>
    /// <c>az account set --subscription &lt;name&gt;</c> fails with "has more than one
    /// match" whenever the expiring tenant and its replacement are both signed in — which
    /// is exactly the window in which someone needs this advice. So resolve the name to
    /// concrete IDs and print those instead, ignoring subscriptions that are no longer
    /// Enabled: the expired tenant's copy is never the intended target.
    /// </para>
    /// </summary>
    private static string SuggestSwitch(AzCli az, string expectedName)
    {
        var query =
            $"[?name=='{expectedName.Replace("'", "''")}' && state=='Enabled'].[id,tenantId]";
        var rows = az.Tsv("account", "list", "--all", "--query", query)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rows.Length == 0)
        {
            return "   Fix with:\n"
                + "     az login\n"
                + $"   No enabled subscription named '{expectedName}' is visible. If the tenant was\n"
                + "   just rotated, sign in to the new one:\n"
                + "     az login --tenant <new-tenant-id>";
        }

        var lines = new List<string> { "   Fix with:" };
        foreach (var row in rows)
        {
            var parts = row.Split('\t', StringSplitOptions.TrimEntries);
            var id = parts.ElementAtOrDefault(0) ?? string.Empty;
            var tenant = parts.ElementAtOrDefault(1) ?? string.Empty;
            lines.Add($"     az account set --subscription {id}   # tenant {tenant}");
        }

        // Names are not unique across tenants, and during a rotation two identically named
        // subscriptions are visible at once. Only the caller knows which tenant is current,
        // so ask rather than guess.
        if (rows.Length > 1)
        {
            lines.Add($"   {rows.Length} enabled subscriptions share this name (a tenant rotation in");
            lines.Add("   progress leaves both visible). Choose the NEWER tenant.");
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Name of the state storage account: alias-prefixed so it is recognisable, with 8 hex
    /// digits of the subscription ID appended because the name must be globally unique.
    /// <para>
    /// Hyphens are stripped — storage account names allow lowercase alphanumerics only.
    /// Computing this with an empty subscription ID yields a DIFFERENT, shorter account
    /// name that Terraform will happily create and then find empty, so the caller must
    /// have a resolved context first.
    /// </para>
    /// </summary>
    public string StateAccountName(string alias)
    {
        if (string.IsNullOrEmpty(SubscriptionId))
        {
            throw new LiveToolException("Refusing to derive a state account name without a subscription ID.");
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{alias.Replace("-", string.Empty, StringComparison.Ordinal)}tf{SubscriptionId[..8]}");
    }
}
