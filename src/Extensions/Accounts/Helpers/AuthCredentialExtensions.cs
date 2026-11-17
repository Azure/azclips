// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Accounts;

internal static class AuthCredentialExtensions
{
    public static AuthMethod ToAuthMethod(this AuthCredential credential)
    {
        return credential switch
        {
            AuthCredential.Browser => AuthMethod.Browser,
            AuthCredential.DeviceCode => AuthMethod.DeviceCode,
            AuthCredential.ManagedIdentity => AuthMethod.ManagedIdentity,
            AuthCredential.FederatedIdentity => AuthMethod.Federated,
            AuthCredential.ClientSecret => AuthMethod.ServicePrincipalSecret,
            _ => throw new NotSupportedException($"Unsupported credential type: {credential.GetType().Name}")
        };
    }
}
