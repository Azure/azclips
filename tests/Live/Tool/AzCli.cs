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
internal sealed record AzContext(string TenantId, string SubscriptionId, string SubscriptionName)
{
    /// <summary>
    /// The environment is identified by subscription <b>name</b>, not ID, so each developer
    /// can run against their own MSDN subscription without editing anything.
    /// </summary>
    public const string ExpectedSubscriptionName = "Visual Studio Enterprise Subscription";

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
        var account = az.Tsv("account", "show", "--query", "[tenantId,id,name]");
        if (string.IsNullOrWhiteSpace(account))
        {
            throw new LiveToolException(
                "Not signed in to Azure. Run:\n"
                + "     az login\n"
                + $"     az account set --subscription \"{expectedName}\"");
        }

        // `-o tsv` returns a list query one value per line, not tab-separated.
        var fields = account.Split('\n', StringSplitOptions.TrimEntries);
        var ctx = new AzContext(
            fields.ElementAtOrDefault(0) ?? string.Empty,
            fields.ElementAtOrDefault(1) ?? string.Empty,
            fields.ElementAtOrDefault(2) ?? string.Empty);

        if (!string.Equals(ctx.SubscriptionName, expectedName, StringComparison.Ordinal))
        {
            throw new LiveToolException(
                "Azure CLI is pointed at the wrong subscription.\n"
                + $"   Expected: {expectedName}\n"
                + $"   Current:  {ctx.SubscriptionName} ({ctx.SubscriptionId})\n"
                + "   Fix with:\n"
                + $"     az account set --subscription \"{expectedName}\"");
        }

        if (string.IsNullOrEmpty(ctx.TenantId) || string.IsNullOrEmpty(ctx.SubscriptionId))
        {
            throw new LiveToolException("Could not resolve tenant/subscription ID from 'az account show'.");
        }

        return ctx;
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
