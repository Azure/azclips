using System.Text.Json.Serialization;

namespace Live.Tests;

/// <summary>
/// A profile as <c>profile show</c> prints it.
/// </summary>
internal sealed record ProfileOutput
{
    public string? Name { get; init; }

    public string? SubscriptionId { get; init; }

    public AuthMethod AuthMethod { get; init; }

    /// <summary>
    /// How the identity was selected at login: <c>MSI@</c> for the system-assigned
    /// identity, or <c>MSIClient@</c>/<c>MSIObject@</c>/<c>MSIResource@</c> followed
    /// by the id for a user-assigned one.
    /// </summary>
    public string? ManagedIdentityId { get; init; }
}

/// <summary>
/// How a profile authenticates.
///
/// <para>
/// The CLI serializes this by ordinal, so the order of these members is part of the
/// contract, not a detail — <c>ManagedIdentity</c> must stay 4. The converter also
/// accepts the member name so this suite keeps working if the CLI ever switches to
/// writing names.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AuthMethod>))]
internal enum AuthMethod
{
    Browser,
    Broker,
    DeviceCode,
    ServicePrincipal,
    ManagedIdentity,
}
