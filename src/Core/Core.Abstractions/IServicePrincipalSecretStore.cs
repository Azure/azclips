// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public interface IServicePrincipalSecretStore
{
    Task<string?> GetAssertionAsync(string clientId, string tenantId, string? cacheDirectory = null, ILoggingService? logger = null);
    Task<string?> GetClientSecretAsync(string clientId, string? cacheDirectory = null, ILoggingService? logger = null);
    Task RemoveAsync(string clientId, string tenantId, string? cacheDirectory = null, ILoggingService? logger = null);
    Task SaveAssertionAsync(string clientId, string tenantId, string assertion, string? cacheDirectory = null, ILoggingService? logger = null);
    Task SaveClientSecretAsync(string clientId, string clientSecret, string? cacheDirectory = null, ILoggingService? logger = null);
}
