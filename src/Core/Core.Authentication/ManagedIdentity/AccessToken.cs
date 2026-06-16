namespace Core.Authentication;

/// <summary>
/// Minimal access token returned by the native managed-identity credential.
/// Replaces Azure.Core.AccessToken so Azclips no longer depends on the Azure SDK for MI auth.
/// </summary>
public readonly struct AccessToken
{
    public AccessToken(string token, DateTimeOffset expiresOn)
    {
        Token = token;
        ExpiresOn = expiresOn;
    }

    /// <summary>The bearer access token string.</summary>
    public string Token { get; }

    /// <summary>UTC time at which the token expires.</summary>
    public DateTimeOffset ExpiresOn { get; }
}
