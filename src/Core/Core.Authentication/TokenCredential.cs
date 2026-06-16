namespace Core.Authentication;

/// <summary>
/// Base type for credentials that acquire access tokens, mirroring Azure SDK's
/// TokenCredential. All credential types derive from this so callers (and tests) depend
/// on the abstraction rather than a concrete credential.
/// </summary>
public abstract class TokenCredential
{
    /// <summary>Acquires an access token for the given scopes.</summary>
    public abstract Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default);
}
