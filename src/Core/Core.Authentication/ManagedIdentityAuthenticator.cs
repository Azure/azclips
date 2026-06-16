using Core.Abstractions;
using System.IdentityModel.Tokens.Jwt;

namespace Core.Authentication;

/// <summary>
/// Handles managed-identity login and silent token refresh.
/// </summary>
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

        // Determine identity type from whichever typed field is set
        var (idType, idValue) = !string.IsNullOrEmpty(request.ManagedIdentityClientId)
            ? (ManagedIdentityIdType.ClientId, request.ManagedIdentityClientId)
            : !string.IsNullOrEmpty(request.ManagedIdentityObjectId)
            ? (ManagedIdentityIdType.ObjectId, request.ManagedIdentityObjectId)
            : !string.IsNullOrEmpty(request.ManagedIdentityResourceId)
            ? (ManagedIdentityIdType.ResourceId, request.ManagedIdentityResourceId)
            : (ManagedIdentityIdType.SystemAssigned, (string?)null);

        Logger.Debug($"[{nameof(ManagedIdentityAuthenticator)}] Acquiring token via managed identity. Id:'{idValue ?? "(system-assigned)"}', IdType:{idType}");

        var credential = CredentialFactory.CreateManagedIdentityCredential(idValue, idType);
        var token = await credential.GetTokenAsync(scopes, cancellationToken);

        Logger.Debug($"[{nameof(ManagedIdentityAuthenticator)}] Token acquired. ExpiresOn:{token.ExpiresOn}");

        // Tenant comes from the token's tid claim; throw if absent (matches Azure CLI).
        var tenantId = ExtractTenantIdFromToken(token.Token);

        return new AuthResult
        {
            AccessToken = token.Token,
            ExpiresOn = token.ExpiresOn,
            Scopes = scopes,
            TenantId = tenantId,
            CorrelationId = null,
        };
    }

    /// <summary>
    /// Re-acquires a token using the persisted auth context (for silent token refresh).
    /// Parses the combined "MSIClient@guid" key from context.ManagedIdentityId.
    /// </summary>
    public async Task<AuthResult> AcquireTokenAsync(AuthContext context, string[]? scopes, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(ManagedIdentityAuthenticator)}.{nameof(AcquireTokenAsync)}");

        var effectiveScopes = scopes ?? [ArmDefaultScope];

        var (idType, idValue) = AuthContext.ParseManagedIdentityId(context.ManagedIdentityId);
        var credential = CredentialFactory.CreateManagedIdentityCredential(idValue, idType);
        var token = await credential.GetTokenAsync(effectiveScopes, cancellationToken);

        return new AuthResult
        {
            AccessToken = token.Token,
            ExpiresOn = token.ExpiresOn,
            Scopes = effectiveScopes,
            TenantId = context.TenantId,
            CorrelationId = null,
        };
    }

    /// <summary>
    /// Extracts the tenant ID from a JWT access token's "tid" claim.
    /// Matches Azure CLI behavior: throws if tid claim is not found.
    /// </summary>
    private static string ExtractTenantIdFromToken(string accessToken)
    {
        var handler = new JwtSecurityTokenHandler();
        // ReadJwtToken doesn't validate signature - we trust IMDS source
        // Matches Azure CLI: jwt.decode(..., options={"verify_signature": False})
        var jwt = handler.ReadJwtToken(accessToken);
        var tidClaim = jwt.Claims.FirstOrDefault(c => c.Type == "tid");
        
        if (tidClaim == null)
        {
            throw new InvalidOperationException(
                "Managed identity token is missing required 'tid' (tenant ID) claim. " +
                "This indicates an invalid or malformed token from IMDS.");
        }
        
        return tidClaim.Value;
    }
}
