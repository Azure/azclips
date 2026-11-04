// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class FileCliOrchestratorBase : FileShareWorkflowBase
{
    public string AuthMode { get; set; } = "login";
    public string? FileEndpoint { get; set; }
    public bool DisallowTrailingDot { get; set; }

    protected void ResolveCliDirectory()
    {
        if (!string.Equals(AuthMode, "login", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Azure Files supports auth-mode login only; no account-key fallback is performed.");
        string? account = StorageAccountName;
        if (FileEndpoint is not null)
        {
            if (!Uri.TryCreate(FileEndpoint, UriKind.Absolute, out var endpoint))
                throw new ArgumentException("Specify an HTTPS public Azure Files account endpoint.");
            string endpointAccount = StorageContext.ValidatePublicUri(endpoint, StorageService.Files);
            if (endpoint.AbsolutePath != "/" || endpoint.Query.Length != 0 || account is not null && account != endpointAccount)
                throw new ArgumentException("The Files endpoint must be an account root consistent with account-name.");
            account = endpointAccount;
        }
        if (StorageAccountName is null) StorageAccountName = account;
        var directory = ResolveDirectory();
        if (account is not null && directory.Context.StorageAccountName != account)
            throw new ArgumentException("The Files endpoint conflicts with the native context.");
        Context = directory.Context;
        Profile = directory.Context.Profile.Name;
        ShareObject = directory.Share;
        DirectoryObject = directory;
        AllowTrailingDot = !DisallowTrailingDot;
    }
}
