// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

/// <summary>
/// The two inputs to MSAL that vary between production and test hosts. The default instance
/// reproduces production behaviour exactly, so registering it changes nothing; benchmarks
/// override it to point MSAL at a scratch token cache and to block its network access.
/// </summary>
public sealed record MsalClientOptions
{
    /// <summary>Directory holding the MSAL token cache file.</summary>
    public string CacheDirectory { get; init; } = HomePaths.GetDefaultLocalAppDataRoot();

    /// <summary>
    /// HTTP factory handed to MSAL. Null leaves MSAL on its own HTTP stack, which is what
    /// production uses. Tests supply one to observe or block MSAL's network calls.
    /// </summary>
    public IMsalHttpClientFactory? HttpClientFactory { get; init; }
}
