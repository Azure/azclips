// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

internal static class StorageBlobCustomizationHelpers
{
    internal static string BuildContainerUrl(string accountName, string containerName)
    {
        if (accountName.Length is < 3 or > 24
            || accountName.Any(c => !(char.IsAsciiLetterLower(c) || c is >= '0' and <= '9')))
        {
            throw new ArgumentException(
                "AccountName must contain 3-24 lowercase letters or digits.",
                nameof(accountName));
        }
        if (containerName.Length is < 3 or > 63
            || !char.IsLetterOrDigit(containerName[0])
            || !char.IsLetterOrDigit(containerName[^1])
            || containerName.Contains("--", StringComparison.Ordinal)
            || containerName.Any(c => !(char.IsAsciiLetterLower(c) || c is >= '0' and <= '9' || c == '-')))
        {
            throw new ArgumentException(
                "ContainerName is not a valid Azure Blob container name.",
                nameof(containerName));
        }
        return $"https://{accountName}.blob.core.windows.net/{Uri.EscapeDataString(containerName)}";
    }

    internal static string BuildBlobUrl(string accountName, string containerName, string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName)
            || blobName.Length > 1024
            || blobName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "BlobName must contain 1-1024 non-control characters.",
                nameof(blobName));
        }

        string escapedBlobName = string.Join(
            "/",
            blobName.Split('/', StringSplitOptions.None).Select(Uri.EscapeDataString));
        return $"{BuildContainerUrl(accountName, containerName)}/{escapedBlobName}";
    }

    internal static string ValidateBlobUrl(string absoluteUri)
    {
        if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out Uri? uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !(uri.Host.EndsWith(".blob.core.windows.net", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".blob.core.usgovcloudapi.net", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".blob.core.chinacloudapi.cn", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".blob.core.cloudapi.de", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "AbsoluteUri must be an HTTPS Azure Blob Storage URL without user information or a fragment.",
                nameof(absoluteUri));
        }
        return absoluteUri;
    }

    internal static Dictionary<string, string> ParsePairs(string[] values, string parameterName)
    {
        Dictionary<string, string> pairs = [];
        foreach (string value in values)
        {
            int separator = value.IndexOf('=');
            if (separator <= 0)
            {
                throw new ArgumentException($"Expected KEY=VALUE but received '{value}'.", parameterName);
            }
            pairs[value[..separator]] = value[(separator + 1)..];
        }
        return pairs;
    }

    internal static string? SerializeTags(string[] values) =>
        values.Length == 0
            ? null
            : string.Join(
                "&",
                ParsePairs(values, "Tags").Select(pair =>
                    $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
}
