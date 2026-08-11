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
    private const string SubscriptionRole = "Contributor";

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

        timing.Timed("app registration", () => (appObjectId, clientId) = EnsureApp(az, appName));
        timing.Timed("service principal", () => spObjectId = EnsureServicePrincipal(az, clientId));
        timing.Timed("federated credential", () => EnsureFederatedCredential(az, appObjectId, subject));
        timing.Timed("role assignment", () => EnsureRoleAssignment(az, ctx, spObjectId));
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
    /// Finds the app by display name, or creates it. Display names are not unique in Entra,
    /// so an ambiguous match is an error rather than a guess — picking the wrong one would
    /// repoint CI at another identity.
    /// </summary>
    private static (string AppObjectId, string ClientId) EnsureApp(AzCli az, string appName)
    {
        var existing = az.Tsv(
            "ad", "app", "list",
            "--display-name", appName,
            "--query", "[].[id,appId]");

        var rows = existing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (rows.Length > 1)
        {
            throw new LiveToolException(
                $"{rows.Length} applications are named '{appName}'. Entra display names are not\n"
                + "   unique, so this cannot tell which one CI should use. Delete the extras, or\n"
                + "   choose a different alias.");
        }

        if (rows.Length == 1)
        {
            var fields = rows[0].Split('\t', StringSplitOptions.TrimEntries);
            var found = (fields.ElementAtOrDefault(0) ?? string.Empty, fields.ElementAtOrDefault(1) ?? string.Empty);
            Console.WriteLine($"==> Application exists ({found.Item2})");
            WarnIfNotOwned(az, found.Item1);
            return found;
        }

        Console.WriteLine("==> Creating the application registration");

        // AzureADMyOrg: the credential is only ever used by this tenant's CI.
        var created = az.TsvChecked(
            "ad", "app", "create",
            "--display-name", appName,
            "--sign-in-audience", "AzureADMyOrg",
            "--query", "[id,appId]");

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
        return (appObjectId, clientId);
    }

    /// <summary>
    /// A guest may manage an application they own and little else, so an ownerless app is
    /// the difference between this working and a bare 403 on the next call. Only warn:
    /// a tenant admin is not an owner either and does not need to be.
    /// </summary>
    private static void WarnIfNotOwned(AzCli az, string appObjectId)
    {
        var owners = az.Tsv("ad", "app", "owner", "list", "--id", appObjectId, "--query", "[].id");
        if (!string.IsNullOrWhiteSpace(owners))
        {
            return;
        }

        Console.WriteLine("    !! This application has no owners. If you are a guest in this tenant,");
        Console.WriteLine("       the following steps will fail with 'Insufficient privileges'. Add");
        Console.WriteLine("       yourself as an owner in the portal, or ask an administrator to.");
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
    private static void EnsureRoleAssignment(AzCli az, AzContext ctx, string spObjectId)
    {
        var scope = $"/subscriptions/{ctx.SubscriptionId}";

        var existing = az.Tsv(
            "role", "assignment", "list",
            "--assignee", spObjectId,
            "--scope", scope,
            "--query", $"[?roleDefinitionName=='{SubscriptionRole}'].id");

        if (!string.IsNullOrWhiteSpace(existing))
        {
            Console.WriteLine($"==> {SubscriptionRole} assignment exists at subscription scope");
            return;
        }

        Console.WriteLine($"==> Granting {SubscriptionRole} at {scope}");

        // --assignee-object-id with an explicit principal type skips the directory lookup
        // that would otherwise fail with PrincipalNotFound while the new service principal
        // is still replicating.
        Retry(
            () => az.None("role", "assignment", "create",
                "--assignee-object-id", spObjectId,
                "--assignee-principal-type", "ServicePrincipal",
                "--role", SubscriptionRole,
                "--scope", scope),
            "the service principal to replicate before it can be granted a role");
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
