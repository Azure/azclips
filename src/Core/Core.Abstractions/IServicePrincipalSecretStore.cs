// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Stores and retrieves service principal credentials independently of the storage backend.
/// </summary>
public interface IServicePrincipalSecretStore
{
    Task<string?> GetAssertionAsync(string clientId, string tenantId);
    Task<string?> GetClientSecretAsync(string clientId);
    Task RemoveAsync(string clientId, string tenantId);
    Task SaveAssertionAsync(string clientId, string tenantId, string assertion);
    Task SaveClientSecretAsync(string clientId, string clientSecret);
}
