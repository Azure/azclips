// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class FileCliOperations
{

    internal static void ResolveDirectory<T>(T owner)
        where T : StorageDataPlaneProcessorBase, IFileCliOptions
    {
        if (!string.Equals(owner.AuthMode, "login", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Azure Files supports auth-mode login only; no account-key fallback is performed.");
        string? account = owner.StorageAccountName;
        if (owner.FileEndpoint is not null)
        {
            if (!Uri.TryCreate(owner.FileEndpoint, UriKind.Absolute, out var endpoint))
                throw new ArgumentException("Specify an HTTPS public Azure Files account endpoint.");
            string endpointAccount = StorageContext.ValidatePublicUri(endpoint, StorageService.Files);
            if (endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || account is not null && account != endpointAccount)
                throw new ArgumentException("The Files endpoint must be an account root consistent with account-name.");
            account = endpointAccount;
        }
        if (owner.StorageAccountName is null) owner.StorageAccountName = account;
        var directory = FileShareOperations.Resolve(owner, owner);
        if (account is not null && directory.Context.StorageAccountName != account)
            throw new ArgumentException("The Files endpoint conflicts with the native context.");
        owner.Context = directory.Context;
        owner.Profile = directory.Context.Profile.Name;
        owner.ShareObject = directory.Share;
        owner.DirectoryObject = directory;
        owner.AllowTrailingDot = !owner.DisallowTrailingDot;
    }
}
