// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

public abstract record AuthCredential
{
    public sealed record Browser : AuthCredential;
    public sealed record DeviceCode : AuthCredential;
    public sealed record ManagedIdentity(ManagedIdentityIdType Type, string? ID = null) : AuthCredential;
    public sealed record FederatedIdentity(string ClientId, string Assertion) : AuthCredential;
    public sealed record ClientSecret(string ClientId, string Secret) : AuthCredential;
}
