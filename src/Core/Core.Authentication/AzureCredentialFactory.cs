using Core.Abstractions;
using MiIdType = Core.Abstractions.ManagedIdentityIdType;

namespace Core.Authentication;

/// <summary>
/// Factory for creating credentials. Callers must pass the identity type and id explicitly
/// (there is no default); the method is virtual so tests can override it to return a stub
/// <see cref="TokenCredential"/> (avoiding real MSAL/IMDS calls).
/// </summary>
public class AzureCredentialFactory
{
    public virtual TokenCredential CreateManagedIdentityCredential(string? identityId, MiIdType? idType)
        => new ManagedIdentityCredential(idType, identityId);
}
