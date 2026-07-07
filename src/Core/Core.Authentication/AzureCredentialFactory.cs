using Core.Abstractions;
using MiIdType = Core.Abstractions.ManagedIdentityIdType;

namespace Core.Authentication;

/// <summary>Creates credentials; virtual so tests can stub them.</summary>
public class AzureCredentialFactory
{
    public virtual TokenCredential CreateManagedIdentityCredential(string? identityId, MiIdType? idType)
        => new ManagedIdentityCredential(idType, identityId);
}
