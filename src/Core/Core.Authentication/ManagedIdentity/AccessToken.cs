namespace Core.Authentication;

/// <summary>Access token and expiry for managed identity auth.</summary>
public readonly struct AccessToken
{
    public AccessToken(string token, DateTimeOffset expiresOn)
    {
        Token = token;
        ExpiresOn = expiresOn;
    }

    /// <summary>Bearer token.</summary>
    public string Token { get; }

    /// <summary>Token expiry time.</summary>
    public DateTimeOffset ExpiresOn { get; }
}
