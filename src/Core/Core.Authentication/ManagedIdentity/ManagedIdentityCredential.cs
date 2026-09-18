using Core.Abstractions;
using Microsoft.Identity.Client;
using MsalManagedIdentityId = Microsoft.Identity.Client.AppConfig.ManagedIdentityId;

namespace Core.Authentication;

/// <summary>MSAL-backed managed identity credential.</summary>
public sealed class ManagedIdentityCredential : TokenCredential
{
    private readonly ManagedIdentityIdType _idType;
    private readonly string? _id;
    private readonly MsalManagedIdentityId _managedIdentityId;
    private readonly Lazy<IManagedIdentityApplication> _application;

    public ManagedIdentityCredential(ManagedIdentityIdType? idType, string? id)
    {
        // validate the identity shape before building the MSAL descriptor
        switch (idType)
        {
            // system-assigned
            case null:
            case ManagedIdentityIdType.SystemAssigned:
                if (!string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException(
                        "A system-assigned managed identity must not be given an id; " +
                        "use a ClientId/ObjectId/ResourceId type for a user-assigned identity.");
                }
                _idType = ManagedIdentityIdType.SystemAssigned;
                _id = null;
                _managedIdentityId = MsalManagedIdentityId.SystemAssigned;
                break;

            // user-assigned
            case ManagedIdentityIdType.ClientId:
            case ManagedIdentityIdType.ObjectId:
            case ManagedIdentityIdType.ResourceId:
                if (string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException(
                        $"A user-assigned managed identity of type {idType} requires a non-empty id.");
                }
                _idType = idType.Value;
                _id = id;
                _managedIdentityId = CreateUserAssignedId(idType.Value, id);
                break;

            default:
                throw new ArgumentException($"Unsupported managed-identity type: {idType}.");
        }

        _application = new Lazy<IManagedIdentityApplication>(
            () => ManagedIdentityApplicationBuilder.Create(_managedIdentityId).Build());
    }

    /// <summary>Maps a user-assigned identity to MSAL's descriptor.</summary>
    private static MsalManagedIdentityId CreateUserAssignedId(ManagedIdentityIdType idType, string id) => idType switch
    {
        ManagedIdentityIdType.ClientId => MsalManagedIdentityId.WithUserAssignedClientId(id),
        ManagedIdentityIdType.ObjectId => MsalManagedIdentityId.WithUserAssignedObjectId(id),
        ManagedIdentityIdType.ResourceId => MsalManagedIdentityId.WithUserAssignedResourceId(id),
        _ => throw new ArgumentException($"'{idType}' is not a user-assigned managed-identity type."),
    };

    public ManagedIdentityIdType IdType => _idType;

    public string? Id => _id;

    public async override Task<AccessToken> GetTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        var resource = ScopeToResource(scopes);

        // MSAL handles source selection, retries and caching.
        var result = await _application.Value
            .AcquireTokenForManagedIdentity(resource)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }

    /// <summary>Converts a scope to the resource format expected by MI endpoints.</summary>
    public static string ScopeToResource(string[] scopes)
    {
        // Managed identity acquires tokens through AcquireTokenForManagedIdentity, which takes a
        // single resource rather than a scope list, so a request with more than one scope cannot be
        // honored. Reject it with a usage error rather than silently keeping only the first scope and
        // dropping the rest, which would hand back a token for a resource the caller did not ask for.
        if (scopes is { Length: > 1 })
        {
            throw AzclipsErrorException.UsageError(
                "Managed identity supports a single resource per token request; multiple scopes cannot be requested for a managed identity account.");
        }

        var scope = scopes is { Length: > 0 } ? scopes[0] : AuthConstants.ArmDefaultScope;
        return scope.EndsWith("/.default", StringComparison.Ordinal)
            ? scope[..^"/.default".Length]
            : scope;
    }
}
