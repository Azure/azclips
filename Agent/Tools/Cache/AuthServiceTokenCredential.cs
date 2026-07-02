using Azure.Core;
using Core.Abstractions;

namespace Core.Agent.Tools.Cache;

/// <summary>
/// Adapter exposing <see cref="IAuthService"/> as an Azure SDK
/// <see cref="TokenCredential"/>. Lets ARM SDK clients (e.g.,
/// <see cref="Azure.ResourceManager.ArmClient"/>) authenticate through
/// the azclips-internal MSAL pipeline (single ClientId / shared cache /
/// shared telemetry) instead of carrying their own credential chain.
/// </summary>
internal sealed class AuthServiceTokenCredential : TokenCredential
{
    private readonly IAuthService _authService;

    public AuthServiceTokenCredential(IAuthService authService)
    {
        _authService = authService;
    }

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var result = await _authService
            .AcquireTokenSilentlyAsync(requestContext.Scopes, requestContext.TenantId, promptOnError: false, cancellationToken)
            .ConfigureAwait(false);
        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        => GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();
}
