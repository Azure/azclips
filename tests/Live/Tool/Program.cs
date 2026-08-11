using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Live.Tool;

/// <summary>
/// Full managed-identity live test against real Azure (no SSH).
/// <para>
/// Drives an already-provisioned Linux VM entirely through <c>az vm run-command</c> — there
/// is no SSH, no open port 22, and no long-lived key. Port of <c>mi-live-test.sh</c>;
/// everything that runs ON the VM stays bash, because that is where it executes.
/// </para>
/// </summary>
internal static class Program
{
    private const string Usage = """
        mi-live — managed-identity live test against real Azure.

        Usage:
          dotnet run --project tests/Live/Tool -- [options]

          --rid RID              Runtime id for the bundle (default: linux-x64)
          --config CONFIG        Debug|Release (default: Release)
          --vm-name NAME         VM to verify (default: terraform output vm_name)
          --resource-group RG    Resource group (default: terraform output resource_group)
          --storage-account NAME Storage account for the bundle blob (default: derived)
          --container NAME       Blob container name (default: milive-bundles)
          --provision            Provision/update the VM first. Fast no-op when unchanged.
          --destroy              Destroy the VM and its resource group, then exit. Runs no test.
          --bootstrap            Create the GitHub Actions identity, then exit. Runs no test.
                                 Only needed for CI, and only once per tenant.
          --yes                  Skip the --destroy confirmation prompt (for scripts/CI)
          --skip-publish         Reuse the existing tarball instead of rebuilding
          --keep-blob            Do not delete the uploaded bundle blob afterwards
          -h, --help             Show this help
        """;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (LiveToolException ex)
        {
            Console.Error.WriteLine($"!! {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var rid = "linux-x64";
        var config = "Release";
        var vmName = string.Empty;
        var resourceGroup = string.Empty;
        var storageAccount = string.Empty;
        var container = "milive-bundles";
        var provision = false;
        var destroy = false;
        var bootstrap = false;
        var assumeYes = false;
        var skipPublish = false;
        var keepBlob = false;

        for (var i = 0; i < args.Length; i++)
        {
            string Next(string flag) => i + 1 < args.Length
                ? args[++i]
                : throw new LiveToolException($"{flag} requires a value.");

            switch (args[i])
            {
                case "--rid": rid = Next("--rid"); break;
                case "--config": config = Next("--config"); break;
                case "--vm-name": vmName = Next("--vm-name"); break;
                case "--resource-group": resourceGroup = Next("--resource-group"); break;
                case "--storage-account": storageAccount = Next("--storage-account"); break;
                case "--container": container = Next("--container"); break;
                case "--provision": provision = true; break;
                case "--destroy": destroy = true; break;
                case "--bootstrap": bootstrap = true; break;
                case "--yes": assumeYes = true; break;
                case "--skip-publish": skipPublish = true; break;
                case "--keep-blob": keepBlob = true; break;
                case "-h" or "--help": Console.WriteLine(Usage); return 0;
                default: throw new LiveToolException($"Unknown argument: {args[i]}");
            }
        }

        // Each is a complete alternative to a test run rather than a stage of one, so
        // combining them expresses nothing.
        if (new[] { provision, destroy, bootstrap }.Count(f => f) > 1)
        {
            throw new LiveToolException("--provision, --destroy and --bootstrap are mutually exclusive.");
        }

        var liveRoot = FindLiveRoot();
        var repoRoot = Path.GetFullPath(Path.Combine(liveRoot, "..", ".."));
        var terraformRoot = Path.Combine(liveRoot, "Terraform");
        var tfDir = Path.Combine(terraformRoot, "linux-vm");

        var timing = new Timing();
        var az = new AzCli();

        // Fail fast, before anything slow or billable: building the bundle takes minutes,
        // and every later step assumes the right subscription.
        var ctx = AzContext.Require(az);
        var tf = new Tf(az, ctx, terraformRoot);

        // Creating the identity that CI signs in as cannot itself be done by CI, and
        // needs no terraform, no bundle and no VM — so it returns before the preflight.
        if (bootstrap)
        {
            if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is { Length: > 0 })
            {
                throw new LiveToolException(
                    "--bootstrap creates the identity this workflow signs in as, so it cannot run\n"
                    + "   inside CI. Run it from a developer machine signed in with 'az login'.");
            }

            var result = Bootstrap.Run(az, ctx, tf.ResolveAlias(tfDir), GitHubRepo.Require(), timing);
            timing.Summary("Bootstrap");
            return result;
        }

        // Destroying needs terraform and an alias, but never the .NET SDK — there is no
        // bundle to build.
        Preflight(provision || destroy, skipPublish || destroy, terraformRoot, tfDir);

        // Teardown is a complete alternative to the test run, not a stage of it.
        if (destroy)
        {
            timing.Timed("terraform destroy", () => tf.Destroy(tfDir, () => ConfirmDestroy(assumeYes)));

            Console.WriteLine();
            Console.WriteLine("==> Destroyed. Re-create it with:");
            Console.WriteLine("     dotnet run --project tests/Live/Tool -- --provision");
            return 0;
        }

        // 0) Optionally provision/update the VM first (Stage 1).
        if (provision)
        {
            timing.Timed("terraform provision (plan + apply-if-changed)", () => tf.Provision(tfDir));
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("==> Skipping provision (pass --provision to plan/apply the VM first)");
        }

        // `terraform output` reads REMOTE state, which needs the backend initialised and
        // the access key available. Unconditional: provisioning may not have run.
        string uamiClientId = string.Empty, uamiResourceId = string.Empty, uamiObjectId = string.Empty;
        var alias = string.Empty;
        timing.Timed("terraform init backend + read outputs", () =>
        {
            alias = tf.InitModuleState(tfDir, "linux-vm");

            if (string.IsNullOrEmpty(vmName))
            {
                vmName = tf.Output(tfDir, "vm_name");
            }

            if (string.IsNullOrEmpty(resourceGroup))
            {
                resourceGroup = tf.Output(tfDir, "resource_group");
            }

            uamiClientId = tf.Output(tfDir, "user_assigned_identity_client_id");
            uamiResourceId = tf.Output(tfDir, "user_assigned_identity_resource_id");
            uamiObjectId = tf.Output(tfDir, "user_assigned_identity_principal_id");
        });

        // No name fallbacks: resource names are per-developer (alias-derived), so a guessed
        // default would point at somebody else's VM or at nothing at all.
        if (string.IsNullOrEmpty(vmName) || string.IsNullOrEmpty(resourceGroup))
        {
            var hint = Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is { Length: > 0 }
                ? "\n   In CI: re-run this workflow with the 'provision' input enabled."
                : string.Empty;

            throw new LiveToolException(
                "Could not determine VM name / resource group from the Terraform outputs.\n"
                + "   Provision first (creates them and populates the outputs):\n"
                + "     dotnet run --project tests/Live/Tool -- --provision\n"
                + "   or pass them explicitly with --vm-name and --resource-group."
                + hint
                + "\n   If you have provisioned already, check that tests/Live/Terraform/linux-vm has\n"
                + "   the same alias configured — each alias has its own state and resources.");
        }

        // Bundles live in the per-developer Terraform state account, NOT in the workload
        // resource group. That account already exists, already has its key resolved by the
        // backend init above, and sits outside the group — so the group holds only
        // Terraform-managed resources and `--destroy` can delete it without tripping the
        // provider's refusal to remove a non-empty group.
        if (string.IsNullOrEmpty(storageAccount))
        {
            storageAccount = ctx.StateAccountName(alias);
        }

        var bundleName = $"{config}-{rid}";
        var tarball = Path.Combine(repoRoot, "publish", "vm", $"azclips-{bundleName}.tar.gz");

        Console.WriteLine();
        Console.WriteLine($"==> Target VM:        {vmName}");
        Console.WriteLine($"==> Resource group:   {resourceGroup}");
        Console.WriteLine($"==> Bundle:           {bundleName}");
        Console.WriteLine($"==> Storage account:  {storageAccount} (container {container})");
        Console.WriteLine();

        // 1) Build the bundle unless reusing an existing tarball.
        if (skipPublish)
        {
            Console.WriteLine($"==> --skip-publish: reusing {tarball}");
            if (!File.Exists(tarball))
            {
                throw new LiveToolException($"Tarball not found: {tarball} (drop --skip-publish to build it).");
            }
        }
        else
        {
            timing.Timed("build bundle", () => new Bundle(repoRoot, liveRoot, new Timing()).Build(rid, config, makeTar: true));
        }

        // 2) Ensure the bundle container exists in the state account, then upload the
        //    tarball. A shared-key SAS lets the VM download with a plain HTTPS GET.
        Console.WriteLine();
        var storageKey = tf.ArmAccessKey
            ?? throw new LiveToolException("Storage key unavailable — the Terraform backend was not initialised.");

        timing.Timed("ensure bundle container", () => az.NoneChecked(
            "storage", "container", "create",
            "--name", container, "--account-name", storageAccount, "--account-key", storageKey));

        var blobName = $"azclips-{bundleName}-{DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}.tar.gz";
        var sizeMb = new FileInfo(tarball).Length / 1024 / 1024;
        Console.WriteLine($"==> Uploading {Path.GetFileName(tarball)} ({sizeMb} MB) -> {storageAccount}/{container}/{blobName}");

        timing.Timed("upload bundle blob", () => az.NoneChecked(
            "storage", "blob", "upload",
            "--account-name", storageAccount, "--account-key", storageKey,
            "--container-name", container, "--name", blobName,
            "--file", tarball, "--overwrite"));

        // Short-lived, read-only SAS URL (2h) so the VM can pull the bundle over HTTPS.
        var blobUrl = timing.Timed("generate SAS url", () =>
        {
            var expiry = DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-dd'T'HH:mm'Z'", CultureInfo.InvariantCulture);
            var sas = az.TsvChecked(
                "storage", "blob", "generate-sas",
                "--account-name", storageAccount, "--account-key", storageKey,
                "--container-name", container, "--name", blobName,
                "--permissions", "r", "--expiry", expiry, "--https-only");
            return $"https://{storageAccount}.blob.core.windows.net/{container}/{blobName}?{sas}";
        });

        var remoteScript = Path.GetTempFileName();
        try
        {
            RemoteScript.Write(
                remoteScript,
                RemoteScript.Build(blobUrl, bundleName, vmName, resourceGroup, ctx.SubscriptionId,
                    uamiClientId, uamiResourceId, uamiObjectId));

            Console.WriteLine();
            var runOutput = timing.Timed("az vm run-command (download + install + tests)", () =>
                az.TsvChecked(
                    "vm", "run-command", "invoke",
                    "--resource-group", resourceGroup, "--name", vmName,
                    "--command-id", "RunShellScript", "--scripts", $"@{remoteScript}",
                    "--query", "value[0].message"));

            var runCommandMs = timing.LastMs;

            Console.WriteLine("----- run-command output -----");
            Console.WriteLine(runOutput);
            Console.WriteLine("------------------------------");

            // The orchestrator's own table first, then the VM's, rebuilt from the markers
            // the guest printed. Two tables because the run-command row already contains
            // every VM row.
            timing.Summary("Stage 2 — orchestrator");

            var vmTiming = new Timing();
            vmTiming.RecordFromMarkers(runOutput);
            vmTiming.Summary("Stage 2 — on the VM (via run-command)", runCommandMs);

            // run-command only returns the tail of stdout, so a very chatty guest can lose
            // the earliest markers. Say so rather than showing a silently short table.
            if (!runOutput.Contains("MI_LIVE_TIMING:download bundle", StringComparison.Ordinal))
            {
                Console.WriteLine("    (note: run-command truncated its reply — earliest VM steps are missing above)");
                Console.WriteLine();
            }

            if (!runOutput.Contains("MI_LIVE_RESULT:PASS", StringComparison.Ordinal))
            {
                throw new LiveToolException("End-to-end MI test FAILED (no PASS sentinel in run-command output).");
            }

            Console.WriteLine("==> End-to-end MI test PASSED.");
            return 0;
        }
        finally
        {
            File.Delete(remoteScript);
            if (!keepBlob)
            {
                az.None("storage", "blob", "delete",
                    "--account-name", storageAccount, "--account-key", storageKey,
                    "--container-name", container, "--name", blobName);
            }
        }
    }

    /// <summary>
    /// Ask before destroying, after the plan has already printed what would go.
    /// <para>
    /// Requires the literal word "yes" rather than a bare "y": the plan above it is long
    /// enough that a single keystroke is too easy to give without reading it. Refuses to
    /// assume consent when stdin is not a terminal, so an unattended run can never destroy
    /// a developer's environment by accident.
    /// </para>
    /// </summary>
    private static bool ConfirmDestroy(bool assumeYes)
    {
        if (assumeYes)
        {
            return true;
        }

        if (Console.IsInputRedirected)
        {
            throw new LiveToolException(
                "--destroy needs confirmation, but stdin is not a terminal.\n"
                + "   Re-run with --yes to confirm non-interactively.");
        }

        Console.WriteLine();
        Console.Write("Destroy the resources listed above? Type 'yes' to confirm: ");
        return string.Equals(Console.ReadLine()?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Fail fast with a clear message if required tooling/config is missing.</summary>
    private static void Preflight(bool provision, bool skipPublish, string terraformRoot, string tfDir)
    {
        var missing = new List<string>();

        if (!Proc.Exists("terraform"))
        {
            missing.Add("terraform");
        }

        if (!skipPublish && !Proc.Exists("dotnet"))
        {
            missing.Add("dotnet (.NET SDK)");
        }

        // Provisioning needs `alias` — it names every resource and selects the state blob.
        if (provision
            && !File.Exists(Path.Combine(terraformRoot, "terraform.tfvars"))
            && !File.Exists(Path.Combine(tfDir, "terraform.tfvars"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_VAR_alias")))
        {
            missing.Add($"{Path.Combine(terraformRoot, "terraform.tfvars")} (set alias; copy from terraform.tfvars.example) or TF_VAR_alias");
        }

        if (missing.Count > 0)
        {
            throw new LiveToolException(
                "Preflight failed — missing prerequisites:\n"
                + string.Join('\n', missing.Select(m => $"     - {m}"))
                + "\n   Install/provide the above, then re-run. See --help.");
        }
    }

    /// <summary>
    /// Locate tests/Live by walking up from the executing assembly.
    /// <para>
    /// Anchored on a marker directory rather than a fixed number of "..", so moving the
    /// tool or changing the build output layout cannot silently break path resolution.
    /// </para>
    /// </summary>
    private static string FindLiveRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var d = new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            if (d.Name == "Live" && Directory.Exists(Path.Combine(d.FullName, "Terraform")))
            {
                return d.FullName;
            }
        }

        throw new LiveToolException($"Could not locate the tests/Live directory from {dir}.");
    }
}
