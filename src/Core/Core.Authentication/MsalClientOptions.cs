// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

internal sealed record MsalClientOptions
{
    public MsalClientOptions(IPersistenceStore persistence)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        Persistence = persistence;
    }

    /// <summary>Storage providing managed paths for SDK-owned token caches.</summary>
    public IPersistenceStore Persistence { get; }

    /// <summary>
    /// HTTP factory handed to MSAL's public client and client-secret confidential client.
    /// Null leaves MSAL on its own HTTP stack, which is what production uses.
    /// Tests supply one to observe or block MSAL's network calls.
    /// </summary>
    public IMsalHttpClientFactory? HttpClientFactory { get; init; }
}
