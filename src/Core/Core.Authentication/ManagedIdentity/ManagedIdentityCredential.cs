using Core.Abstractions;
using Microsoft.Identity.Client;
using MsalManagedIdentityId = Microsoft.Identity.Client.AppConfig.ManagedIdentityId;

namespace Core.Authentication;

/// <summary>
/// Managed-identity credential backed by MSAL .NET's <see cref="IManagedIdentityApplication"/>,
/// which handles source detection (IMDS, App Service, Arc, etc.), retries and token caching.
/// Mirrors Azure CLI's msal_credentials.ManagedIdentityCredential.
/// </summary>
public sealed class ManagedIdentityCredential : TokenCredential
{
    private readonly ManagedIdentityIdType _idType;
    private readonly string? _id;
    private readonly MsalManagedIdentityId _managedIdentityId;
    private readonly Lazy<IManagedIdentityApplication> _application;

    public ManagedIdentityCredential(ManagedIdentityIdType? idType, string? id)
    {
        // Valid by construction: the caller passes the identity explicitly, and each case
        // is validated here so a bad combination fails fast. The switch also resolves the
        // MSAL descriptor; only the app build is deferred (Lazy below).
        switch (idType)
        {
            // System-assigned: no type (null) or SystemAssigned, and no id.
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

            // User-assigned: an explicit type together with a non-empty id.
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

    /// <summary>
    /// Maps a user-assigned identity type and its (validated, non-empty) id onto the
    /// corresponding MSAL <see cref="MsalManagedIdentityId"/> descriptor.
    /// </summary>
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

        // MSAL handles source selection (IMDS/App Service/Arc/Service Fabric/Cloud Shell),
        // transient retries and token caching internally.
        var result = await _application.Value
            .AcquireTokenForManagedIdentity(resource)
            .ExecuteAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccessToken(result.AccessToken, result.ExpiresOn);
    }

    /// <summary>
    /// Converts an Azure scope (e.g. "https://management.azure.com/.default") to the
    /// resource form expected by the managed-identity endpoints, matching Azure CLI's
    /// scopes_to_resource helper.
    /// </summary>
    internal static string ScopeToResource(string[] scopes)
    {
        var scope = scopes is { Length: > 0 } ? scopes[0] : AuthConstants.ArmDefaultScope;
        return scope.EndsWith("/.default", StringComparison.Ordinal)
            ? scope[..^"/.default".Length]
            : scope;
    }
}
