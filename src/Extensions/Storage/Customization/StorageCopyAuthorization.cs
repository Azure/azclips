// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class StorageCopyAuthorization
{
    internal const string Version = "2026-06-06";
    internal static string Time(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    internal static string? PublicSource(string? value)
    {
        if (value is null) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidDataException("Storage returned an invalid copy-source URI. Sensitive details are omitted.");
        var original = HttpUtility.ParseQueryString(uri.Query);
        var query = HttpUtility.ParseQueryString("");
        foreach (string selector in new[] { "snapshot", "versionid", "sharesnapshot" })
            if (original[selector] is { } selected) query[selector] = selected;
        return new UriBuilder(uri) { UserName = "", Password = "", Fragment = "", Query = query.ToString() }.Uri.AbsoluteUri;
    }

    internal static string? StatusDescription(string? value) =>
        value is null ? null : "Storage reported additional copy status details; sensitive details are omitted.";

    internal static byte[] DecodeAccountKey(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 4096)
            throw new ArgumentException("Specify a nonempty Base64 source account key.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); }
        catch (FormatException) { throw new ArgumentException("Specify a valid Base64 source account key."); }
        if (bytes.Length != 0) return bytes;
        throw new ArgumentException("Specify a nonempty Base64 source account key.");
    }

    internal static Uri SignFileServiceSas(Uri resource, StorageContext context, string accountKey,
        DateTimeOffset start, DateTimeOffset expiry)
    {
        if (StorageContext.ValidatePublicUri(resource, StorageService.Files) != context.StorageAccountName)
            throw new ArgumentException("The copy source conflicts with its authorization context.");
        var query = HttpUtility.ParseQueryString(resource.Query);
        if (query.AllKeys.Any(key => !string.Equals(key, "sharesnapshot", StringComparison.OrdinalIgnoreCase)) || expiry <= start)
            throw new ArgumentException("Specify a Files resource with compatible selectors and a valid SAS lifetime.");
        string from = Time(start), until = Time(expiry);
        string canonical = "/file/" + context.StorageAccountName + Uri.UnescapeDataString(resource.AbsolutePath);
        string[] fields = ["r", from, until, canonical, "", "", "https", Version, "", "", "", "", ""];
        byte[] key = DecodeAccountKey(accountKey);
        byte[] signature;
        try { signature = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(string.Join("\n", fields))); }
        finally { CryptographicOperations.ZeroMemory(key); }
        query["sp"] = "r"; query["st"] = from; query["se"] = until; query["spr"] = "https";
        query["sv"] = Version; query["sr"] = "f"; query["sig"] = Convert.ToBase64String(signature);
        return new UriBuilder(resource) { Query = query.ToString() }.Uri;
    }

    internal static Uri Sign(Uri resource, StorageContext context, StorageService service, BlobModels.UserDelegationKey key,
        DateTimeOffset start, DateTimeOffset expiry)
    {
        string signedService = service == StorageService.Blob ? "b" : "f";
        if (!Guid.TryParseExact(key.SignedOid, "D", out var oid) || oid == Guid.Empty ||
            !Guid.TryParseExact(key.SignedTid, "D", out var tenant) ||
            !Guid.TryParse(context.Profile.TenantId, out var selectedTenant) || tenant != selectedTenant ||
            key.SignedService != signedService || key.SignedVersion != Version || key.SignedDelegatedUserTid is not null ||
            key.Value is null || key.Value.Length is < 16 or > 4096 ||
            !DateTimeOffset.TryParse(key.SignedStart, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var keyStart) ||
            !DateTimeOffset.TryParse(key.SignedExpiry, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var keyExpiry) ||
            keyStart > start || keyExpiry < expiry || keyExpiry - keyStart > TimeSpan.FromDays(7) || expiry <= start)
            throw new InvalidDataException("Storage returned an invalid source delegation key. Sensitive details are omitted.");
        if (StorageContext.ValidatePublicUri(resource, service) != context.StorageAccountName)
            throw new ArgumentException("The copy source conflicts with its authorization context.");
        var query = HttpUtility.ParseQueryString(resource.Query);
        string snapshot = query["snapshot"] ?? query["versionid"] ?? string.Empty;
        string signedResource = service == StorageService.Files ? "f" :
            query["snapshot"] is not null ? "bs" : query["versionid"] is not null ? "bv" : "b";
        string canonical = "/" + (service == StorageService.Blob ? "blob" : "file") + "/" +
            context.StorageAccountName + Uri.UnescapeDataString(resource.AbsolutePath);
        string from = Time(start), until = Time(expiry);
        string[] fields = service == StorageService.Blob
            ? ["r", from, until, canonical, key.SignedOid, key.SignedTid, key.SignedStart, key.SignedExpiry,
               key.SignedService, key.SignedVersion, "", "", "", "", "", "", "https", Version,
               signedResource, snapshot, "", "", "", "", "", "", "", ""]
            : ["r", from, until, canonical, key.SignedOid, key.SignedTid, key.SignedStart, key.SignedExpiry,
               key.SignedService, key.SignedVersion, "", "", "", "https", Version, "", "", "", "", ""];
        byte[] signature = HMACSHA256.HashData(key.Value, Encoding.UTF8.GetBytes(string.Join("\n", fields)));
        query["sp"] = "r"; query["st"] = from; query["se"] = until; query["spr"] = "https";
        query["sv"] = Version; query["sr"] = signedResource; query["skoid"] = key.SignedOid; query["sktid"] = key.SignedTid;
        query["skt"] = key.SignedStart; query["ske"] = key.SignedExpiry; query["sks"] = key.SignedService;
        query["skv"] = key.SignedVersion; query["sig"] = Convert.ToBase64String(signature);
        return new UriBuilder(resource) { Query = query.ToString() }.Uri;
    }
}
