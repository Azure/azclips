// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public enum StorageService
{
    Blob,
    Files,
    Management
}

public sealed class StorageContext
{
    public string StorageAccountName { get; }
    public Uri BlobEndpoint { get; }
    public Uri FileEndpoint { get; }
    public string ProfileName => Profile.Name;
    public bool EnableFileBackupRequestIntent { get; }

    [JsonIgnore]
    public Profile Profile { get; }

    public StorageContext(string storageAccountName, Profile profile, bool enableFileBackupRequestIntent = false)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(storageAccountName);
        ValidateProfile(profile);
        if (!Regex.IsMatch(storageAccountName, "^[a-z0-9]{3,24}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Storage account names must contain 3-24 lowercase letters or digits.", nameof(storageAccountName));
        StorageAccountName = storageAccountName;
        Profile = profile;
        EnableFileBackupRequestIntent = enableFileBackupRequestIntent;
        BlobEndpoint = new Uri($"https://{storageAccountName}.blob.core.windows.net/");
        FileEndpoint = new Uri($"https://{storageAccountName}.file.core.windows.net/");
    }

    public Uri GetBlobUri(string container, string? blob = null) =>
        GetResourceUri(BlobEndpoint, container, blob, allowSpecialContainer: true);

    public Uri GetFileUri(string share, string? path = null) =>
        GetResourceUri(FileEndpoint, share, path, allowSpecialContainer: false);

    internal static void ValidateProfile(Profile? profile)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.TenantId))
            throw new InvalidOperationException("Sign in and select a profile before using Storage.");
        if (!string.IsNullOrEmpty(profile.CloudName) && profile.CloudName != "AzureCloud")
            throw new NotSupportedException("Storage currently supports AzureCloud only.");
    }

    internal static string ValidatePublicUri(Uri uri, StorageService service)
    {
        string suffix = service switch
        {
            StorageService.Blob => ".blob.core.windows.net",
            StorageService.Files => ".file.core.windows.net",
            _ => throw new ArgumentException("A data-plane service is required.", nameof(service))
        };
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
            uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.IdnHost.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use an HTTPS Azure public-cloud Storage URL without user information or a fragment.");
        string account = uri.IdnHost[..^suffix.Length];
        if (!Regex.IsMatch(account, "^[a-z0-9]{3,24}$", RegexOptions.CultureInvariant))
            throw new ArgumentException("The Storage URL must identify one public-cloud storage account.");
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        foreach (string? key in query.AllKeys)
        {
            if (key is null || key.Equals("sig", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("sv", StringComparison.OrdinalIgnoreCase) || key.Equals("sp", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("se", StringComparison.OrdinalIgnoreCase) || key.StartsWith("sk", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Destination SAS authentication is not supported. Use a profile-backed Storage context.");
        }
        return account;
    }

    internal static Uri MergeResourceQuery(Uri resource, string generatedRequest)
    {
        if (!Uri.TryCreate(generatedRequest, UriKind.Absolute, out var request) ||
            resource.Scheme != request.Scheme || !string.Equals(resource.IdnHost, request.IdnHost, StringComparison.OrdinalIgnoreCase) ||
            resource.Port != request.Port || resource.AbsolutePath != request.AbsolutePath ||
            request.UserInfo.Length != 0 || request.Fragment.Length != 0)
            throw new ArgumentException("The request must target the selected Storage resource.");
        var query = System.Web.HttpUtility.ParseQueryString(request.Query);
        var resourceQuery = System.Web.HttpUtility.ParseQueryString(resource.Query);
        foreach (string? key in resourceQuery.AllKeys)
        {
            if (key is null) throw new ArgumentException("Storage query parameters must be named.");
            var values = resourceQuery.GetValues(key)!;
            if (values.Length != 1 || query.GetValues(key) is { } existing &&
                (existing.Length != 1 || !string.Equals(existing[0], values[0], StringComparison.Ordinal)))
                throw new ArgumentException("Conflicting Storage resource query parameters.");
            query[key] = values[0];
        }
        if (query["snapshot"] is not null && query["versionid"] is not null)
            throw new ArgumentException("Specify a blob snapshot or version, not both.");
        return new UriBuilder(request) { Query = query.ToString() }.Uri;
    }

    private static Uri GetResourceUri(Uri endpoint, string parent, string? path, bool allowSpecialContainer)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (!(allowSpecialContainer && parent is "$root" or "$web" or "$logs") &&
            (!Regex.IsMatch(parent, "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant) ||
             parent.Contains("--", StringComparison.Ordinal)))
            throw new ArgumentException("Container/share names must contain 3-63 lowercase letters, digits or nonconsecutive internal hyphens.");
        if (path is not null && (path.Length == 0 || path.Split('/').Any(segment => segment is "." or "..")))
            throw new ArgumentException("Resource paths must be nonempty and cannot contain dot-only path segments.", nameof(path));
        string escapedPath = path is null ? string.Empty : "/" + string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        return new Uri(endpoint.AbsoluteUri + Uri.EscapeDataString(parent) + escapedPath);
    }
}
