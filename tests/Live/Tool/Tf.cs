using System.Text.RegularExpressions;

namespace Live.Tool;

/// <summary>
/// Terraform state plumbing shared by the provision and read-only paths.
/// Port of <c>Terraform/tf-common.sh</c>.
/// </summary>
internal sealed partial class Tf(AzCli az, AzContext ctx, string terraformRoot)
{
    /// <summary>State location, shared so the wrappers and read-only consumers agree.</summary>
    public const string BackendContainer = "tfstate";
    public const string BackendLocation = "australiaeast";

    /// <summary>
    /// Must stay in sync with the <c>alias</c> validation in both modules' variables.tf.
    /// 3-14 characters; the upper bound is set by the state storage account, capped at 24:
    /// alias (hyphens stripped, &lt;=14) + "tf" + 8 hex digits = 24 exactly.
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9-]{1,12}[a-z0-9]$")]
    private static partial Regex AliasRegex();

    [GeneratedRegex("^\\s*alias\\s*=\\s*\"([^\"]*)\"")]
    private static partial Regex AliasAssignment();

    /// <summary>Last <c>alias = "..."</c> assignment in a tfvars file, or null if absent.</summary>
    internal static string? ParseAlias(string tfvarsContent) =>
        tfvarsContent.Split('\n')
            .Select(l => AliasAssignment().Match(l))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .LastOrDefault(v => v.Length > 0);

    /// <summary>Throws if the alias would not survive Azure resource naming.</summary>
    internal static void ValidateAlias(string alias, string source = "TF_VAR_alias")
    {
        if (!AliasRegex().IsMatch(alias))
        {
            throw new LiveToolException(
                $"Invalid alias '{alias}' (from {source}).\n"
                + "   Must be 3-14 characters, lowercase, start with a letter, and contain\n"
                + "   only a-z, 0-9 and '-'. It cannot end with '-'.");
        }
    }

    /// <summary>ARM_ACCESS_KEY for the azurerm backend, set by <see cref="EnsureStateBackend"/>.</summary>
    public string? ArmAccessKey { get; private set; }

    private IDictionary<string, string> BackendEnv =>
        ArmAccessKey is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["ARM_ACCESS_KEY"] = ArmAccessKey };

    /// <summary>
    /// Resolve the developer alias.
    /// <para>
    /// Backend blocks are evaluated before Terraform variables exist, so the state blob key
    /// cannot interpolate var.alias — this has to know the alias too. Precedence:
    /// TF_VAR_alias (how CI supplies it) &gt; module tfvars &gt; shared tfvars.
    /// </para>
    /// </summary>
    public string ResolveAlias(string moduleDir)
    {
        var sharedTfvars = Path.Combine(terraformRoot, "terraform.tfvars");
        var moduleTfvars = Path.Combine(moduleDir, "terraform.tfvars");

        var value = Environment.GetEnvironmentVariable("TF_VAR_alias") ?? string.Empty;
        var source = "TF_VAR_alias";

        if (string.IsNullOrEmpty(value))
        {
            foreach (var file in new[] { moduleTfvars, sharedTfvars })
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                // Take the last assignment, tolerate whitespace, strip quotes and comments.
                var found = ParseAlias(File.ReadAllText(file));

                if (!string.IsNullOrEmpty(found))
                {
                    value = found;
                    source = file;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(value))
        {
            throw new LiveToolException(
                "No 'alias' configured.\n"
                + $"   Set it in {sharedTfvars} (copy terraform.tfvars.example),\n"
                + "   or export TF_VAR_alias.\n"
                + "   The alias prefixes your resources and selects your Terraform state.");
        }

        ValidateAlias(value, source);

        return value;
    }

    /// <summary>
    /// Guard against an alias change silently orphaning infrastructure. Changing the alias
    /// points init at a DIFFERENT state blob, so Terraform sees empty state and creates a
    /// second full set of resources while the originals keep running (and billing).
    /// </summary>
    public static void CheckAliasDrift(string moduleDir, string alias)
    {
        var marker = Path.Combine(moduleDir, ".alias");
        if (!File.Exists(marker))
        {
            return;
        }

        var previous = File.ReadAllText(marker).Trim();
        if (previous.Length == 0 || previous == alias)
        {
            return;
        }

        throw new LiveToolException(
            $"Alias changed: '{previous}' -> '{alias}'.\n"
            + "   Each alias has its own Terraform state, so continuing would leave the\n"
            + $"   '{previous}' resources running and untracked.\n"
            + $"   Either destroy them first (TF_VAR_alias={previous} terraform destroy),\n"
            + $"   or, if they are already gone, remove the marker:\n     rm {marker}");
    }

    public static void RecordAlias(string moduleDir, string alias) =>
        File.WriteAllText(Path.Combine(moduleDir, ".alias"), alias + "\n");

    /// <summary>Forget the recorded alias. Only correct once its resources are gone.</summary>
    public static void ClearAlias(string moduleDir)
    {
        var marker = Path.Combine(moduleDir, ".alias");
        if (File.Exists(marker))
        {
            File.Delete(marker);
        }
    }

    public static string BackendResourceGroup(string alias) => $"{alias}-azclips-live-test-mi-tfstate-rg";

    /// <summary>
    /// Ensure the state storage account and container exist, then capture the access key.
    /// <para>
    /// The account is located by NAME across the whole subscription rather than by a
    /// hardcoded resource group, so it keeps working if the group was renamed. Storage
    /// account names are globally unique, so creating one in the "wrong" group would fail
    /// with name-taken instead of silently finding different state.
    /// </para>
    /// </summary>
    public void EnsureStateBackend(string storageAccount, string container, string fallbackGroup, string location)
    {
        var resourceGroup = az.Tsv("storage", "account", "show", "--name", storageAccount, "--query", "resourceGroup");

        if (string.IsNullOrEmpty(resourceGroup))
        {
            resourceGroup = fallbackGroup;
            Console.WriteLine($"==> Terraform state account {storageAccount} not found; creating it in {resourceGroup}");

            if (!az.None("group", "show", "--name", resourceGroup).Ok)
            {
                Console.WriteLine($"==> Creating resource group {resourceGroup} ({location})");
                az.NoneChecked("group", "create", "--name", resourceGroup, "--location", location);
            }

            var groupLocation = az.TsvChecked("group", "show", "--name", resourceGroup, "--query", "location");
            az.NoneChecked(
                "storage", "account", "create",
                "--name", storageAccount,
                "--resource-group", resourceGroup,
                "--location", groupLocation,
                "--sku", "Standard_LRS",
                "--kind", "StorageV2",
                "--min-tls-version", "TLS1_2",
                "--allow-blob-public-access", "false");
        }
        else
        {
            Console.WriteLine($"==> Terraform state account {storageAccount} found in {resourceGroup}");
        }

        ArmAccessKey = az.TsvChecked(
            "storage", "account", "keys", "list",
            "--account-name", storageAccount,
            "--resource-group", resourceGroup,
            "--query", "[0].value");

        az.NoneChecked(
            "storage", "container", "create",
            "--name", container,
            "--account-name", storageAccount,
            "--account-key", ArmAccessKey);
    }

    /// <summary>
    /// Point a module at this developer's remote state without applying anything.
    /// <c>terraform output</c> cannot read remote state until the backend is initialised
    /// and ARM_ACCESS_KEY is set, so any read-only path must call this first.
    /// </summary>
    public string InitModuleState(string moduleDir, string moduleName)
    {
        var alias = ResolveAlias(moduleDir);
        Environment.SetEnvironmentVariable("TF_VAR_alias", alias);
        var account = ctx.StateAccountName(alias);

        EnsureStateBackend(account, BackendContainer, BackendResourceGroup(alias), BackendLocation);
        Init(moduleDir, account, $"{alias}-{moduleName}.tfstate");
        return alias;
    }

    public void Init(string moduleDir, string storageAccount, string key)
    {
        var r = Proc.Run(
            "terraform",
            [
                $"-chdir={moduleDir}", "init", "-input=false", "-reconfigure",
                $"-backend-config=storage_account_name={storageAccount}",
                $"-backend-config=key={key}",
            ],
            env: BackendEnv);

        if (!r.Ok)
        {
            throw new LiveToolException(
                $"terraform init failed (exit {r.ExitCode}).\n{(string.IsNullOrWhiteSpace(r.Stderr) ? r.Stdout : r.Stderr).Trim()}");
        }
    }

    /// <summary>Read a single output, or empty string when unavailable.</summary>
    public string Output(string moduleDir, string name)
    {
        var r = Proc.Run("terraform", [$"-chdir={moduleDir}", "output", "-raw", name], env: BackendEnv);
        return r.Ok ? r.Out : string.Empty;
    }

    public void Validate(string moduleDir) =>
        Proc.Check("terraform", [$"-chdir={moduleDir}", "validate"], env: BackendEnv);

    /// <summary>
    /// plan + apply, skipping the apply when nothing changed.
    /// <c>-detailed-exitcode</c>: 0 = no changes, 1 = error, 2 = changes pending.
    /// <para>
    /// The plan is always saved and applied from the file, so what gets applied is exactly
    /// what was shown — which is what makes <paramref name="confirm"/> meaningful for the
    /// destroy path, where the user is approving a specific list of resources.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> when a plan was applied, <c>false</c> when there was nothing to do.</returns>
    public bool PlanAndApply(
        string moduleDir,
        string planFile = "provision.tfplan",
        bool destroy = false,
        Func<bool>? confirm = null)
    {
        Console.WriteLine($"==> terraform plan{(destroy ? " -destroy" : string.Empty)} (checking for pending changes)");

        List<string> planArgs = [$"-chdir={moduleDir}", "plan", "-input=false", "-detailed-exitcode", $"-out={planFile}"];
        if (destroy)
        {
            planArgs.Insert(2, "-destroy");
        }

        var plan = Proc.RunStreaming("terraform", planArgs, env: BackendEnv);

        var planPath = Path.Combine(moduleDir, planFile);
        try
        {
            switch (plan.ExitCode)
            {
                case 0:
                    Console.WriteLine(destroy
                        ? "==> Nothing to destroy — the state tracks no resources."
                        : "==> No changes — infrastructure is up to date. Skipping apply.");
                    return false;
                case 2:
                    if (confirm is not null && !confirm())
                    {
                        throw new LiveToolException("Aborted — nothing was destroyed.");
                    }

                    Console.WriteLine($"==> Applying the plan above ({(destroy ? "destroy" : "auto-approve")})");
                    var apply = Proc.RunStreaming(
                        "terraform",
                        [$"-chdir={moduleDir}", "apply", "-input=false", "-auto-approve", planFile],
                        env: BackendEnv);
                    if (!apply.Ok)
                    {
                        throw new LiveToolException($"terraform apply failed (exit {apply.ExitCode}).");
                    }

                    return true;
                default:
                    throw new LiveToolException($"terraform plan failed (exit {plan.ExitCode}).");
            }
        }
        finally
        {
            if (File.Exists(planPath))
            {
                File.Delete(planPath);
            }
        }
    }

    /// <summary>Stage 1: provision or update the VM. Port of provision-infrastructure.sh.</summary>
    public void Provision(string moduleDir)
    {
        PrepareModule(moduleDir);

        Console.WriteLine("==> terraform validate");
        Validate(moduleDir);

        PlanAndApply(moduleDir);
        RecordAlias(moduleDir, ResolveAlias(moduleDir));
    }

    /// <summary>
    /// Tear down everything stage 1 created: the VM, both managed identities, their
    /// resource-group-scoped role assignments, networking, and the resource group itself —
    /// then the state group that recorded it.
    /// <para>
    /// The state group cannot be Terraform-managed (a destroy would be deleting the state
    /// it is writing to), so it is deleted separately, after the workload is gone. That
    /// also takes the bundle blob container with it, which is intended: nothing survives a
    /// destroy except the bootstrap identity, an Entra object with a subscription-scoped
    /// role assignment that no resource-group deletion can reach.
    /// </para>
    /// <para>
    /// Prefer this over deleting the resource group by hand: the portal route leaves the
    /// state claiming the resources still exist, which breaks the next provision.
    /// </para>
    /// </summary>
    public void Destroy(string moduleDir, Func<bool> confirm)
    {
        var alias = PrepareModule(moduleDir);
        var applied = PlanAndApply(moduleDir, "destroy.tfplan", destroy: true, confirm);

        // With nothing in state the plan never asked for consent, but the state group is
        // about to go regardless — so ask now.
        if (!applied && !confirm())
        {
            throw new LiveToolException("Aborted — nothing was destroyed.");
        }

        DestroyStateBackend(moduleDir, alias);

        // The marker exists to stop an alias change orphaning running resources. There are
        // none left, so keeping it would only block the next alias change for no reason.
        ClearAlias(moduleDir);
    }

    /// <summary>
    /// Delete the state resource group, once the resources it tracked are gone.
    /// <para>
    /// Located by storage account name, the same way <see cref="EnsureStateBackend"/> finds
    /// it, and only deleted when it sits in the group this tool would have created. A group
    /// someone moved it into may hold unrelated resources, and deleting that is not this
    /// tool's call to make.
    /// </para>
    /// </summary>
    public void DestroyStateBackend(string moduleDir, string alias)
    {
        var account = ctx.StateAccountName(alias);
        var expected = BackendResourceGroup(alias);
        var resourceGroup = az.Tsv("storage", "account", "show", "--name", account, "--query", "resourceGroup");

        if (string.IsNullOrEmpty(resourceGroup))
        {
            Console.WriteLine($"==> Terraform state account {account} is already gone.");
        }
        else if (!string.Equals(resourceGroup, expected, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"!! Terraform state account {account} is in '{resourceGroup}', not '{expected}'.\n"
                + "   Leaving it alone — it may share the group with resources this tool did not\n"
                + "   create. Delete it by hand if you no longer need it.");
            return;
        }
        else
        {
            Console.WriteLine($"==> Deleting Terraform state group {resourceGroup}");
            az.NoneChecked("group", "delete", "--name", resourceGroup, "--yes");
            ArmAccessKey = null;
        }

        // The backend cache still names a storage account that no longer exists, and its
        // key is stale. Left in place, the next init reuses it and fails; removing it makes
        // the next provision re-bootstrap the backend from scratch.
        var backendCache = Path.Combine(moduleDir, ".terraform", "terraform.tfstate");
        if (File.Exists(backendCache))
        {
            File.Delete(backendCache);
        }
    }

    /// <summary>
    /// Shared setup for both write paths: resolve the alias, point Terraform at this
    /// developer's subscription, and initialise the matching remote state.
    /// </summary>
    private string PrepareModule(string moduleDir)
    {
        var alias = ResolveAlias(moduleDir);
        CheckAliasDrift(moduleDir, alias);

        // Terraform targets whatever subscription the CLI is on, rather than a hardcoded
        // one, so each developer works in their own subscription.
        Environment.SetEnvironmentVariable("TF_VAR_tenant_id", ctx.TenantId);
        Environment.SetEnvironmentVariable("TF_VAR_subscription_id", ctx.SubscriptionId);
        Environment.SetEnvironmentVariable("TF_VAR_alias", alias);
        Console.WriteLine($"==> Alias: {alias}");

        var account = ctx.StateAccountName(alias);
        EnsureStateBackend(account, BackendContainer, BackendResourceGroup(alias), BackendLocation);

        Console.WriteLine($"==> terraform init (state: {alias}-linux-vm.tfstate)");
        Init(moduleDir, account, $"{alias}-linux-vm.tfstate");
        return alias;
    }
}
