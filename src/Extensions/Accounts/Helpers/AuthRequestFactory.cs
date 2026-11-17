// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

public static class AuthRequestFactory
{
    public static LoginRequest CreateInteractive(string? tenantId = null, string[]? scopes = null)
    {
        return CreateRequest(new AuthCredential.Browser(), tenantId, scopes);
    }

    private static LoginRequest CreateRequest(AuthCredential credential, string? tenantId = null, string[]? scopes = null)
    {
        return new LoginRequest(
            TenantId: string.IsNullOrEmpty(tenantId) ? null : tenantId,
            Scopes: scopes,
            Credential: credential
        );
    }
    public static LoginRequest CreateDeviceCode(string? tenantId = null, string[]? scopes = null)
    {
        return CreateRequest(new AuthCredential.DeviceCode(), tenantId, scopes);
    }

    public static LoginRequest CreateClientSecret(string clientId, string clientSecret, string? tenantId = null, string[]? scopes = null)
    {
        return CreateRequest(new AuthCredential.ClientSecret(clientId, clientSecret), tenantId, scopes);
    }

    public static LoginRequest CreateManagedIdentity(string? tenantId = null, string? clientId = null, string? objectId = null, string? resourceId = null, string[]? scopes = null)
    {
        AuthCredential credential;
        if (!string.IsNullOrEmpty(clientId))
        {
            credential = new AuthCredential.ManagedIdentity(ManagedIdentityIdType.ClientId, clientId);
        }
        else if (!string.IsNullOrEmpty(objectId))
        {
            credential = new AuthCredential.ManagedIdentity(ManagedIdentityIdType.ObjectId, objectId);
        }
        else if (!string.IsNullOrEmpty(resourceId))
        {
            credential = new AuthCredential.ManagedIdentity(ManagedIdentityIdType.ResourceId, resourceId);
        }
        else
        {
            credential = new AuthCredential.ManagedIdentity(ManagedIdentityIdType.SystemAssigned);
        }
        return CreateRequest(credential, tenantId, scopes);
    }

    public static LoginRequest CreateFederatedIdentity(string clientId, string federatedToken, string? tenantId = null, string[]? scopes = null)
    {
        return CreateRequest(new AuthCredential.FederatedIdentity(clientId, federatedToken), tenantId, scopes);
    }

    public static async Task<LoginRequest> CreateFromProfile(Profile profile, IServicePrincipalSecretStore secretStore)
    {
        AuthCredential credential = profile.AuthMethod switch
        {
            AuthMethod.ManagedIdentity => ManagedIdentityIdCodec.ParseToCredentials(profile.ManagedIdentityId),
            AuthMethod.ServicePrincipalSecret => new AuthCredential.ClientSecret(profile.ClientId!, await secretStore.GetClientSecretAsync(profile.ClientId!) ?? throw AzclipsErrorException.InvalidInput($"Could not load the persisted client secret for application '{profile.ClientId!}'. Please sign in again.")),
            AuthMethod.Federated => new AuthCredential.FederatedIdentity(profile.ClientId!, await secretStore.GetAssertionAsync(profile.ClientId!, profile.TenantId!) ?? throw AzclipsErrorException.InvalidInput($"Could not load the persisted Federated Identity for application '{profile.ClientId!}'. Please sign in again.")),
            AuthMethod.DeviceCode => new AuthCredential.DeviceCode(),
            AuthMethod.Browser => new AuthCredential.Browser(),
            _ => throw AzclipsErrorException.InvalidInput(
                $"usage error: Unsupported authentication method '{profile.AuthMethod}'.")
        };

        return new LoginRequest(
            TenantId: profile.TenantId,
            Scopes: null,
            Credential: credential
        );
    }
}
