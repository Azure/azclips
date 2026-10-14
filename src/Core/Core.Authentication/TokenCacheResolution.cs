// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

/// <summary>Where a resolved token cache actually keeps its payload.</summary>
internal enum TokenCacheProtection
{
    /// <summary>DPAPI, libsecret, or Keychain — the payload never reaches disk unprotected.</summary>
    OsCredentialStore,

    /// <summary>An unprotected file. Readable by anything running as this user.</summary>
    PlaintextFile,
}

/// <summary>A resolved cache and the protection that ended up holding it.</summary>
internal sealed class TokenCacheResolution
{
    /// <summary>The backing store. Always present: every protection tier has one.</summary>
    public MsalCacheHelper? Helper { get; init; }

    public TokenCacheProtection Protection { get; init; }

    public bool PlaintextUnsafe => Protection == TokenCacheProtection.PlaintextFile;

    /// <summary>Keeps the older <c>(helper, plaintextUnsafe)</c> shape usable at call sites.</summary>
    public void Deconstruct(out MsalCacheHelper? helper, out bool plaintextUnsafe)
    {
        helper = Helper;
        plaintextUnsafe = PlaintextUnsafe;
    }
}
