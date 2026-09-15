using Core.Abstractions;
using GitHub.Copilot;

namespace Core.Agent.Providers;

/// <summary>
/// Builds a Copilot SDK <see cref="ProviderConfig"/> for the "builtin"
/// provider (Microsoft-hosted GPT via APIM), including its Azure-login
/// side-effect. Kept out of <see cref="ChatAgent"/> so provider wiring
/// evolves without touching session lifecycle. Also owns the two error
/// helpers callers use when a builtin login fails or an unknown provider
/// name is supplied.
/// </summary>
internal static class BuiltinProviderConfig
{
    public static async Task<ProviderConfig> BuildAsync(
        BuiltinProviderOptions opts,
        IAuthService auth,
        CancellationToken ct)
    {
        var authResult = await AcquireAsync(opts, auth, ct).ConfigureAwait(false);
        return Build(opts, authResult.AccessToken);
    }

    /// <summary>
    /// Acquires the Azure token the builtin provider authenticates with.
    /// </summary>
    /// <remarks>
    /// <para>Split out from <see cref="Build"/> so a host can acquire once, up front, and feed the
    /// same token to both the provider and the ARM data boundary lookup — one MSAL flow on the
    /// startup path instead of two, and the actionable login error surfaces before anything else
    /// runs.</para>
    ///
    /// <para>The requested audience is <c>management.core.windows.net</c> while the boundary lookup
    /// calls <c>management.azure.com</c>. Those are interchangeable ARM audiences, not a mismatch:
    /// <c>TUI/Azure/ArmClient.cs</c> already ships this exact pairing — it acquires with null scopes
    /// (which <c>MsalAuthService</c> resolves to <c>AuthConstants.ArmDefaultScope</c>, the same
    /// <c>management.core.windows.net</c> value) and calls <c>management.azure.com/tenants</c> and
    /// Resource Graph under <c>EnsureSuccessStatusCode</c>. Keeping this audience also preserves the
    /// bearer the APIM provider has always been given.</para>
    /// </remarks>
    public static async Task<AuthResult> AcquireAsync(
        BuiltinProviderOptions opts,
        IAuthService auth,
        CancellationToken ct)
    {
        AuthResult? authResult;
        try
        {
            authResult = await auth.AcquireTokenSilentlyAsync(
                scopes: [BuiltinProviderOptions.ArmScope],
                tenantId: null,
                promptOnError: false,
                cancellationToken: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ctrl+C during startup: let cancellation bubble up so the top-level
            // handler exits quietly (130), instead of masquerading as an auth failure.
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                BuildAuthErrorMessage() +
                $"{Environment.NewLine}{Environment.NewLine}Underlying cause: {ex.GetType().Name}: {ex.Message}",
                ex);
        }

        if (authResult is null || string.IsNullOrEmpty(authResult.AccessToken))
        {
            throw new InvalidOperationException(BuildAuthErrorMessage());
        }

        return authResult;
    }

    /// <summary>Builds the SDK provider config from an already-acquired token.</summary>
    public static ProviderConfig Build(BuiltinProviderOptions opts, string accessToken) =>
        new()
        {
            Type = "azure",
            WireApi = "responses",
            BaseUrl = opts.Endpoint,
            BearerToken = accessToken,
            ModelId = opts.ModelId,
            WireModel = opts.ModelId,
            Azure = new AzureOptions { ApiVersion = opts.ApiVersion },
        };

    public static string BuildAuthErrorMessage() =>
        """
        Agent's 'builtin' provider requires an Azure login to call the managed GPT model.
        No active Azure account was found.

        To fix this, run:
            az login --subscription-id <your-subscription-id>

        Alternative: switch to the 'github' provider (uses your own GitHub Copilot subscription,
        billed to your GitHub account):
            az config set --key agent.provider --value github
        """;

    public static string FormatProviderForError(string? provider)
    {
        if (provider is null) return "(null)";
        if (provider.Length == 0) return "(empty)";
        if (string.IsNullOrWhiteSpace(provider)) return $"(whitespace, length={provider.Length})";
        return $"'{provider}'";
    }
}
