// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageBlobCliAddress
{
    internal static string? ResolveAccount(string? accountName, string? blobEndpoint, string authMode)
    {
        if (!string.Equals(authMode, "login", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Storage supports auth-mode login only; no account-key fallback is performed.");
        if (blobEndpoint is null) return accountName;
        if (!Uri.TryCreate(blobEndpoint, UriKind.Absolute, out var endpoint))
            throw new ArgumentException("Specify an absolute HTTPS public-Azure Blob endpoint.");
        string endpointAccount = StorageContext.ValidatePublicUri(endpoint, StorageService.Blob);
        if (endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0)
            throw new ArgumentException("BlobEndpoint must be an account root without a path or query.");
        if (accountName is not null && accountName != endpointAccount)
            throw new ArgumentException("The account name conflicts with the Blob endpoint.");
        return endpointAccount;
    }

    internal static void ValidateContext(string? account, StorageContext context)
    {
        if (account is not null && context.StorageAccountName != account)
            throw new ArgumentException("The Blob endpoint conflicts with the selected context.");
    }
}
