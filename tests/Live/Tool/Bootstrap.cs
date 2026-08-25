using System.Text.Json;

namespace Live.Tool;

/// <summary>
/// Creates the Entra identity GitHub Actions authenticates as, and publishes its IDs to
/// the repository as <c>dev</c> environment variables.
///
/// <para>
/// Deliberately driven by <c>az</c> and <c>gh</c> rather than Terraform, and holding no
/// state of its own. Everything it creates is an Entra directory object or a
/// subscription-scoped role assignment, so none of it lives in a resource group — there is
/// nothing for a resource-group sweep to delete, but equally nowhere natural to keep state.
/// The previous Terraform module kept LOCAL state, which made it the one piece that could
/// not survive a tenant rotation: the state described objects in a tenant no longer
/// reachable, and refreshing them failed with a 401 from the old issuer.
/// </para>
///
/// <para>
/// Being stateless, this reads what already exists and creates only what is missing, so
/// re-running after a rotation just works. It is also why <c>az ad app create</c> matters:
/// it records the caller as an <b>owner</b> of the application, and an owner may manage it
/// afterwards. The azuread Terraform provider left the app ownerless, so a guest could
/// create it and then not create its service principal, add a federated credential, or
/// delete it — the exact failure this replaces.
/// </para>
///
/// <para>
/// Run from a developer machine, never in CI: it creates the very identity CI logs in as.
/// </para>
/// </summary>
internal static class Bootstrap
{
    private const string GitHubEnvironment = "dev";
    private const string OidcIssuer = "https://token.actions.githubusercontent.com";
    private const string OidcAudience = "api://AzureADTokenExchange";
    /// <summary>
    /// Contributor cannot write <c>Microsoft.Authorization/*</c>, but the module grants the
    /// VM's two identities a role at resource-group scope — so provisioning needs RBAC
    /// write as well. Two narrow roles rather than Owner: RBAC Administrator adds only
    /// roleAssignments write/delete, and the roles being granted are non-privileged
    /// (Reader), which is what it is allowed to assign.
    /// </summary>
    private static readonly string[] SubscriptionRoles =
        ["Contributor", "Role Based Access Control Administrator"];

    /// <summary>
    /// Newly created Entra objects are not immediately visible to every endpoint, and the
    /// next call is against a different one (Graph -> Graph -> ARM). Retry rather than
    /// sleeping a fixed amount, because the delay is usually zero and occasionally long.
    /// </summary>
    private const int MaxAttempts = 30;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(4);

    public static int Run(AzCli az, AzContext ctx, string alias, string repo, Timing timing)
    {
        var appName = $"{alias}-azclips-live-test-mi";
        var subject = $"repo:{repo}:environment:{GitHubEnvironment}";

        Console.WriteLine($"==> Tenant:       {ctx.TenantId}");
        Console.WriteLine($"==> Subscription: {ctx.SubscriptionName} ({ctx.SubscriptionId})");
        Console.WriteLine($"==> Application:  {appName}");
        Console.WriteLine($"==> Trusting:     {subject}");
        Console.WriteLine();

        string appObjectId = string.Empty, clientId = string.Empty, spObjectId = string.Empty;

        var reusedExisting = false;
        timing.Timed("app registration", () => (appObjectId, clientId, reusedExisting) = EnsureApp(az, appName));

        timing.Timed("service principal", () =>
        {
            try
            {
                spObjectId = EnsureServicePrincipal(az, clientId);
            }
            catch (LiveToolException) when (reusedExisting)
            {
                // Reusing an application is a judgement call based on an ownership read,
                // and that read is not always right — Graph will occasionally report an
                // application as owned that we cannot in fact manage. The service
                // principal is where that shows up, so treat it as the ownership check
                // having been wrong rather than as fatal: a fresh application is
                // guaranteed manageable, because creating it is what confers ownership.
                Console.WriteLine("    The existing application cannot be used after all. Creating a new one.");
                (appObjectId, clientId, _) = EnsureApp(az, appName, forceCreate: true);
                spObjectId = EnsureServicePrincipal(az, clientId);
            }
        });
        timing.Timed("federated credential", () => EnsureFederatedCredential(az, appObjectId, subject));
        timing.Timed("role assignments", () => EnsureRoleAssignments(az, ctx, spObjectId));
        timing.Timed("publish GitHub variables", () => PublishVariables(ctx, alias, clientId, repo));

        Console.WriteLine();
        Console.WriteLine("==> Bootstrap complete.");
        Console.WriteLine($"     client id:  {clientId}");
        Console.WriteLine($"     tenant id:  {ctx.TenantId}");
        Console.WriteLine($"     subscription: {ctx.SubscriptionId}");
        Console.WriteLine();
        Console.WriteLine("     Run the workflow with:");
        Console.WriteLine($"       gh workflow run mi-live.yml --repo {repo} --ref $(git branch --show-current)");
        return 0;
    }

    /// <summary>
    /// Finds an application we can actually manage, or creates one.
    ///
    /// <para>
    /// Selection is by ownership, not by display name alone. Entra display names are not
    /// unique, and ownership is what decides whether the rest of this can run at all: a
    /// guest may manage an application they own and essentially nothing else. So a
    /// same-named application we cannot manage is not "the" application — it is an
    /// unrelated object that happens to share a label. CI is pointed at whatever
    /// <c>AZURE_CLIENT_ID</c> ends up being, never at a name, so this is unambiguous
    /// downstream.
    /// </para>
    /// </summary>
    private static (string AppObjectId, string ClientId, bool ReusedExisting) EnsureApp(
        AzCli az,
        string appName,
        bool forceCreate = false)
    {
        var candidates = az.Tsv("ad", "app", "list", "--display-name", appName, "--query", "[].[id,appId]")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(row => row.Split('\t', StringSplitOptions.TrimEntries))
            .Where(f => f.Length >= 2)
            .Select(f => (AppObjectId: f[0], ClientId: f[1]))
            .ToList();

        // Signing in as anything but a user (which --bootstrap does not support) leaves
        // nothing to compare owners against; fall back to name matching alone.
        var me = az.Tsv("ad", "signed-in-user", "show", "--query", "id");

        var manageable = forceCreate
            ? []
            : string.IsNullOrWhiteSpace(me)
                ? candidates
                : candidates.Where(c => IsOwnedBy(az, c.AppObjectId, me)).ToList();

        if (manageable.Count > 1)
        {
            throw new LiveToolException(
                $"{manageable.Count} applications named '{appName}' are owned by you. This cannot\n"
                + "   tell which one CI should use. Delete the extras, or choose a different alias.");
        }

        if (manageable.Count == 1)
        {
            Console.WriteLine($"==> Application exists ({manageable[0].ClientId})");
            return (manageable[0].AppObjectId, manageable[0].ClientId, true);
        }

        // Same-named applications exist but none is ours. An ownerless one is usually the
        // residue of a tool that did not record an owner; claiming it is better than
        // duplicating it, and an administrator running this will succeed here too.
        foreach (var candidate in forceCreate ? [] : candidates)
        {
            if (TryAdopt(az, candidate.AppObjectId, me))
            {
                Console.WriteLine($"==> Adopted the existing application ({candidate.ClientId})");
                return (candidate.AppObjectId, candidate.ClientId, true);
            }
        }

        if (candidates.Count > 0 && !forceCreate)
        {
            Console.WriteLine($"==> {candidates.Count} application(s) named '{appName}' exist, but none can be");
            Console.WriteLine("    managed by you and none would let you take ownership. Creating a");
            Console.WriteLine("    separate one rather than failing — CI is pointed at a client id, not a");
            Console.WriteLine("    name, so this is unambiguous. Ask a tenant administrator to delete the");
            Console.WriteLine("    others when convenient:");
            foreach (var candidate in candidates)
            {
                Console.WriteLine($"      {candidate.ClientId}");
            }
        }

        Console.WriteLine("==> Creating the application registration");

        // Graph directly, not `az ad app create`: that command treats the display name as
        // a key and PATCHES an existing application instead of creating one, which is the
        // opposite of what is wanted when the existing application is the unusable one.
        //
        // Creating it is also what makes it manageable — Graph records the calling user as
        // an owner. AzureADMyOrg because the credential is only used by this tenant's CI.
        var body = JsonSerializer.Serialize(new
        {
            displayName = appName,
            signInAudience = "AzureADMyOrg",
        });

        var created = Proc.Check("az", [
            "rest",
            "--method", "POST",
            "--url", "https://graph.microsoft.com/v1.0/applications",
            "--headers", "Content-Type=application/json",
            "--body", body,
            "--query", "[id,appId]",
            "-o", "tsv",
        ]);

        // A flat projection comes back one value per line, CRLF-terminated on some
        // platforms — unlike the list query above, whose rows are tab-separated.
        var parts = created.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var appObjectId = parts.ElementAtOrDefault(0) ?? string.Empty;
        var clientId = parts.ElementAtOrDefault(1) ?? string.Empty;

        if (string.IsNullOrEmpty(appObjectId) || string.IsNullOrEmpty(clientId))
        {
            throw new LiveToolException("Created the application but could not read its ids back.");
        }

        Console.WriteLine($"    created {clientId}");
        return (appObjectId, clientId, false);
    }

    private static bool IsOwnedBy(AzCli az, string appObjectId, string userObjectId) =>
        az.Tsv("ad", "app", "owner", "list", "--id", appObjectId, "--query", "[].id")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(userObjectId, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to take ownership. Succeeds for an administrator; for anyone else this is
    /// the cheapest reliable test of whether the application is usable at all, because the
    /// alternative is discovering it three calls later.
    /// </summary>
    private static bool TryAdopt(AzCli az, string appObjectId, string userObjectId)
    {
        if (string.IsNullOrWhiteSpace(userObjectId))
        {
            return false;
        }

        return az.None("ad", "app", "owner", "add", "--id", appObjectId, "--owner-object-id", userObjectId).Ok;
    }

    private static string EnsureServicePrincipal(AzCli az, string clientId)
    {
        var existing = az.Tsv("ad", "sp", "list", "--filter", $"appId eq '{clientId}'", "--query", "[].id");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            var id = existing.Split('\n', StringSplitOptions.TrimEntries)[0];
            Console.WriteLine($"==> Service principal exists ({id})");
            return id;
        }

        Console.WriteLine("==> Creating the service principal");

        // A just-created application is not visible to the service-principal endpoint yet,
        // and the error it returns then is the misleading "the backing application of the
        // service principal being created must in the local tenant". Retry through it.
        Retry(
            () => az.None("ad", "sp", "create", "--id", clientId),
            "the application to replicate before its service principal can be created");

        // Read the object id back rather than parsing the create output: the create is
        // retried, and a retry that succeeds after a partial failure still needs an id.
        var spObjectId = az.TsvChecked(
            "ad", "sp", "list", "--filter", $"appId eq '{clientId}'", "--query", "[0].id");

        Console.WriteLine($"    created {spObjectId}");
        return spObjectId;
    }

    /// <summary>
    /// The OIDC trust: GitHub mints a token for the workflow, Entra exchanges it when
    /// issuer, subject and audience all match. No client secret ever exists.
    /// </summary>
    private static void EnsureFederatedCredential(AzCli az, string appObjectId, string subject)
    {
        var existing = az.Tsv(
            "ad", "app", "federated-credential", "list",
            "--id", appObjectId,
            "--query", "[].[name,subject]");

        foreach (var row in existing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = row.Split('\t', StringSplitOptions.TrimEntries);
            if (string.Equals(fields.ElementAtOrDefault(1), subject, StringComparison.Ordinal))
            {
                Console.WriteLine($"==> Federated credential exists ({fields[0]})");
                return;
            }
        }

        Console.WriteLine("==> Creating the federated credential");

        var parameters = JsonSerializer.Serialize(new
        {
            name = $"github-{GitHubEnvironment}",
            issuer = OidcIssuer,
            subject,
            audiences = new[] { OidcAudience },
        });

        // --parameters accepts inline JSON, so this avoids a temp file. Written through a
        // file would also work, but leaves one behind on failure.
        Retry(
            () => az.None("ad", "app", "federated-credential", "create",
                "--id", appObjectId, "--parameters", parameters),
            "the application to replicate before a federated credential can be added");

        Console.WriteLine($"    trusting {subject}");
    }

    /// <summary>
    /// Subscription scope, not resource-group scope: stage 1 creates its own resource
    /// group, which cannot be granted from inside itself.
    /// </summary>
    private static void EnsureRoleAssignments(AzCli az, AzContext ctx, string spObjectId)
    {
        var scope = $"/subscriptions/{ctx.SubscriptionId}";

        // One list call for all of them: each az invocation costs ~1s under WSL interop.
        var assigned = az.Tsv(
                "role", "assignment", "list",
                "--assignee", spObjectId,
                "--scope", scope,
                "--query", "[].roleDefinitionName")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var role in SubscriptionRoles)
        {
            if (assigned.Contains(role))
            {
                Console.WriteLine($"==> {role} assignment exists at subscription scope");
                continue;
            }

            Console.WriteLine($"==> Granting {role} at {scope}");

            // --assignee-object-id with an explicit principal type skips the directory
            // lookup that would otherwise fail with PrincipalNotFound while the new service
            // principal is still replicating.
            Retry(
                () => az.None("role", "assignment", "create",
                    "--assignee-object-id", spObjectId,
                    "--assignee-principal-type", "ServicePrincipal",
                    "--role", role,
                    "--scope", scope),
                "the service principal to replicate before it can be granted a role");
        }
    }

    /// <summary>
    /// Publishes what the workflow reads. These are variables, not secrets: a client id,
    /// tenant id and subscription id are not credentials on their own — the federated
    /// credential is what authorises, and it only trusts this repository's dev environment.
    /// </summary>
    private static void PublishVariables(AzContext ctx, string alias, string clientId, string repo)
    {
        var values = new (string Name, string Value)[]
        {
            ("AZURE_CLIENT_ID", clientId),
            ("AZURE_TENANT_ID", ctx.TenantId),
            ("AZURE_SUBSCRIPTION_ID", ctx.SubscriptionId),
            ("ALIAS", alias),
        };

        Console.WriteLine($"==> Publishing {values.Length} '{GitHubEnvironment}' environment variables to {repo}");

        foreach (var (name, value) in values)
        {
            // `gh variable set` creates the environment if it does not exist, so the
            // workflow's `environment: dev` requirement is satisfied by this alone.
            var r = Proc.Run("gh", [
                "variable", "set", name,
                "--repo", repo,
                "--env", GitHubEnvironment,
                "--body", value,
            ]);

            if (!r.Ok)
            {
                var detail = string.IsNullOrWhiteSpace(r.Stderr) ? r.Stdout : r.Stderr;
                throw new LiveToolException(
                    $"Could not set the '{name}' variable on {repo}.\n"
                    + "   The identity itself was created; only publishing failed, so re-running\n"
                    + $"   is safe.\n{detail.Trim()}");
            }

            Console.WriteLine($"    {name}");
        }
    }

    /// <summary>
    /// Retries until Entra replication catches up.
    ///
    /// <para>
    /// Only replication is worth retrying. A permission failure returns the same answer
    /// thirty times over, so it aborts on the first one — otherwise the run spends minutes
    /// before reporting something that was already decided.
    /// </para>
    /// </summary>
    private static void Retry(Func<ProcResult> action, string waitingFor)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = action();
            if (result.Ok)
            {
                return;
            }

            var detail = (string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr).Trim();

            if (IsPermissionDenied(detail))
            {
                throw new LiveToolException(
                    $"Permission denied while waiting for {waitingFor}.\n"
                    + "   This is not a replication delay and will not resolve by retrying.\n\n"
                    + "   The usual cause is an application you do not own. Entra lets a guest\n"
                    + "   manage an application they are an owner of, and 'az ad app create' makes\n"
                    + "   the caller an owner — but an application created by something else (the\n"
                    + "   azuread Terraform provider, for instance) can have no owner at all, and\n"
                    + "   then nobody but a tenant administrator can touch it.\n\n"
                    + "   Ask a tenant administrator to add you as an owner of the application, or\n"
                    + "   to delete it so this can create a fresh one.\n\n"
                    + $"   {detail}");
            }

            if (attempt == 1)
            {
                Console.WriteLine($"    waiting for {waitingFor}...");
            }

            if (attempt == MaxAttempts)
            {
                throw new LiveToolException(
                    $"Gave up after {MaxAttempts} attempts waiting for {waitingFor}.\n   {detail}");
            }

            Thread.Sleep(RetryDelay);
        }
    }

    /// <summary>
    /// Entra reports this as a 403 whose body names the code; the CLI surfaces the body
    /// rather than the status, so match on the text.
    ///
    /// <para>
    /// The last one is the same denial wearing a disguise. Creating a service principal
    /// for an application you may not manage is reported as the backing application not
    /// being local — which is untrue and sends you looking at <c>signInAudience</c>. It is
    /// matched here because it is the shape this failure actually arrives in.
    /// </para>
    /// </summary>
    internal static bool IsPermissionDenied(string detail) =>
        detail.Contains("Authorization_RequestDenied", StringComparison.OrdinalIgnoreCase)
        || detail.Contains("Insufficient privileges", StringComparison.OrdinalIgnoreCase)
        || detail.Contains("AuthorizationFailed", StringComparison.OrdinalIgnoreCase)
        || detail.Contains("backing application of the service principal", StringComparison.OrdinalIgnoreCase);
}
