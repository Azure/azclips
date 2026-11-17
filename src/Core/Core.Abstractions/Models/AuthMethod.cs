namespace Core.Abstractions;

/// <summary>
/// The authentication method used to establish a profile's credentials.
/// </summary>
public enum AuthMethod
{
    /// <summary>Interactive browser-based sign-in (default).</summary>
    Browser,

    /// <summary>Interactive sign-in through the operating system's authentication broker (e.g. WAM).</summary>
    Broker,

    /// <summary>Device code flow, for environments without an interactive browser.</summary>
    DeviceCode,

    /// <summary>Service principal authenticating with a federated (OIDC) client assertion.</summary>
    Federated,

    /// <summary>Managed identity. Reserved for forward-compatibility.</summary>
    ManagedIdentity,

    ServicePrincipalSecret,
}
