namespace Core.Abstractions;

/// <summary>
/// Specifies the authentication mode for login.
/// </summary>
public enum LoginMode
{
    /// <summary>Interactive browser-based login (default).</summary>
    Interactive,

    /// <summary>Device code flow login for environments without a browser.</summary>
    DeviceCode,

    /// <summary>Managed identity login (system-assigned or user-assigned).</summary>
    ManagedIdentity,
}
