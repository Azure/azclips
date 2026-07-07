namespace Core.Authentication;

/// <summary>Base type for credentials that acquire access tokens.</summary>
public abstract class TokenCredential
{
    /// <summary>Acquires an access token for the given scopes.</summary>
    public abstract Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default);
}
