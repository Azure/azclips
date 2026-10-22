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
        string baseUrl,
        IAuthService auth,
        CancellationToken ct)
    {
        var authResult = await AcquireAsync(opts, auth, ct).ConfigureAwait(false);
        return Build(opts, baseUrl, authResult.AccessToken);
    }

    /// <summary>Acquires the ARM token shared by the builtin provider and boundary lookup.</summary>
    /// <remarks>
    /// Keep the existing ARM audience used by the builtin provider.
    /// ARM accepts that token at management.azure.com; no second acquisition is needed.
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
    public static ProviderConfig Build(BuiltinProviderOptions opts, string baseUrl, string accessToken) =>
        new()
        {
            Type = "azure",
            WireApi = "responses",
            BaseUrl = baseUrl,
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
