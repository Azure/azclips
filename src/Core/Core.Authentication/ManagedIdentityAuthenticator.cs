using Core.Abstractions;

namespace Core.Authentication;

/// <summary>Managed identity login and token refresh.</summary>
public class ManagedIdentityAuthenticator(
    AzureCredentialFactory CredentialFactory,
    ILoggingService Logger,
    ITelemetryService TelemetryService)
{
    private const string ArmDefaultScope = AuthConstants.ArmDefaultScope;

    public async Task<AuthResult> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(ManagedIdentityAuthenticator)}.{nameof(AuthenticateAsync)}");

        var scopes = request.Scopes ?? [ArmDefaultScope];
        var (idType, idValue) = ConvertToTypedId(request);

        Logger.Debug($"[{nameof(ManagedIdentityAuthenticator)}] Acquiring token via managed identity. Id:'{idValue ?? "(system-assigned)"}', IdType:{idType}");

        var credential = CredentialFactory.CreateManagedIdentityCredential(idValue, idType);
        var token = await credential.GetTokenAsync(scopes, cancellationToken);

        Logger.Debug($"[{nameof(ManagedIdentityAuthenticator)}] Token acquired. ExpiresOn:{token.ExpiresOn}");

        return new AuthResult
        {
            AccessToken = token.Token,
            ExpiresOn = token.ExpiresOn,
            Scopes = scopes,
            TenantId = string.Empty,
            CorrelationId = null,
        };
    }

    /// <summary>Re-acquires a token from the managed identity saved on the profile.</summary>
    public async Task<AuthResult> AcquireTokenAsync(Profile profile, string[]? scopes, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(ManagedIdentityAuthenticator)}.{nameof(AcquireTokenAsync)}");

        var effectiveScopes = scopes ?? [ArmDefaultScope];

        var (idType, idValue) = ManagedIdentityIdCodec.Parse(profile.ManagedIdentityId);
        var credential = CredentialFactory.CreateManagedIdentityCredential(idValue, idType);
        var token = await credential.GetTokenAsync(effectiveScopes, cancellationToken);

        return new AuthResult
        {
            AccessToken = token.Token,
            ExpiresOn = token.ExpiresOn,
            Scopes = effectiveScopes,
            TenantId = profile.TenantId,
            EntraObjectId = idType == ManagedIdentityIdType.ObjectId ? idValue : null,
            CorrelationId = null,
        };
    }

    /// <summary>Converts the request's managed identity fields into an (id type, id value) pair, defaulting to system-assigned.</summary>
    private static (ManagedIdentityIdType IdType, string? IdValue) ConvertToTypedId(LoginRequest request)
    {
        if (!string.IsNullOrEmpty(request.ManagedIdentityClientId))
        {
            return (ManagedIdentityIdType.ClientId, request.ManagedIdentityClientId);
        }

        if (!string.IsNullOrEmpty(request.ManagedIdentityObjectId))
        {
            return (ManagedIdentityIdType.ObjectId, request.ManagedIdentityObjectId);
        }

        if (!string.IsNullOrEmpty(request.ManagedIdentityResourceId))
        {
            return (ManagedIdentityIdType.ResourceId, request.ManagedIdentityResourceId);
        }

        return (ManagedIdentityIdType.SystemAssigned, null);
    }
}
