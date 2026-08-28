// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class GenerateStorageBlobSasProcessor : StorageBlobProcessorBase
{
    private const string SasVersion = "2023-11-03";
    private const string PermissionOrder = "racwdxytmeopi";

    public string Permissions { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;
    public string? Start { get; set; }
    public string? IpAddressOrRange { get; set; }
    public bool HttpsOnly { get; set; }
    public bool FullUri { get; set; }

    public override async Task ProcessAsync()
    {
        ValidateStorageTarget(requireBlob: true);
        string permissions = NormalizePermissions(Permissions);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset start = ParseTime(Start, nameof(Start)) ?? now.AddMinutes(-5);
        DateTimeOffset expiry = ParseTime(Expiry, nameof(Expiry))
            ?? throw new ArgumentException("Expiry is required.", nameof(Expiry));
        if (expiry <= start || expiry > now.AddDays(7))
        {
            throw new ArgumentException(
                "Expiry must be after Start and no more than seven days from now.",
                nameof(Expiry));
        }
        if (IpAddressOrRange?.Any(char.IsControl) == true)
        {
            throw new ArgumentException("IpAddressOrRange cannot contain control characters.", nameof(IpAddressOrRange));
        }

        string signedStart = FormatTime(start);
        string signedExpiry = FormatTime(expiry);
        UserDelegationKey key = await GetUserDelegationKeyAsync(signedStart, signedExpiry);
        string protocol = HttpsOnly ? "https" : string.Empty;
        string stringToSign = string.Join(
            "\n",
            permissions,
            signedStart,
            signedExpiry,
            $"/blob/{AccountName}/{ContainerName}/{BlobName}",
            key.SignedObjectId,
            key.SignedTenantId,
            key.SignedStart,
            key.SignedExpiry,
            key.SignedService,
            key.SignedVersion,
            "", // signed authorized object ID
            "", // signed unauthorized object ID
            "", // signed correlation ID
            IpAddressOrRange ?? string.Empty,
            protocol,
            SasVersion,
            "b",
            "", // signed snapshot time
            "", // signed encryption scope
            "", "", "", "", ""); // response header overrides

        using var hmac = new HMACSHA256(Convert.FromBase64String(key.Value));
        string signature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign)));
        string token = BuildQuery(
            ("sv", SasVersion),
            ("st", signedStart),
            ("se", signedExpiry),
            ("skoid", key.SignedObjectId),
            ("sktid", key.SignedTenantId),
            ("skt", key.SignedStart),
            ("ske", key.SignedExpiry),
            ("sks", key.SignedService),
            ("skv", key.SignedVersion),
            ("sip", IpAddressOrRange),
            ("spr", protocol),
            ("sr", "b"),
            ("sp", permissions),
            ("sig", signature));

        string output = FullUri
            ? new UriBuilder(BuildBlobUri()) { Query = token }.Uri.AbsoluteUri
            : token;
        await WriteOutput(output, Publisher.Token);
    }

    private async Task<UserDelegationKey> GetUserDelegationKeyAsync(string start, string expiry)
    {
        var uri = new Uri(
            $"https://{AccountName}.blob.core.windows.net/?restype=service&comp=userdelegationkey");
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, uri);
        request.Content = new StringContent(
            new XElement("KeyInfo", new XElement("Start", start), new XElement("Expiry", expiry))
                .ToString(SaveOptions.DisableFormatting),
            Encoding.UTF8,
            "application/xml");
        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);
        XDocument document = await XDocument.LoadAsync(
            await response.Content.ReadAsStreamAsync(Publisher.Token),
            LoadOptions.None,
            Publisher.Token);
        XElement root = document.Root
            ?? throw new InvalidDataException("The user delegation key response was empty.");

        return new UserDelegationKey(
            Required(root, "SignedOid"),
            Required(root, "SignedTid"),
            Required(root, "SignedStart"),
            Required(root, "SignedExpiry"),
            Required(root, "SignedService"),
            Required(root, "SignedVersion"),
            Required(root, "Value"));
    }

    private static string NormalizePermissions(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(character => !PermissionOrder.Contains(character))
            || value.Distinct().Count() != value.Length)
        {
            throw new ArgumentException(
                $"Permissions must contain unique characters from '{PermissionOrder}'.",
                nameof(Permissions));
        }
        return string.Concat(PermissionOrder.Where(value.Contains));
    }

    private static DateTimeOffset? ParseTime(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        if (!DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out DateTimeOffset result))
        {
            throw new ArgumentException($"{parameterName} must be an ISO 8601 date and time.", parameterName);
        }
        return result;
    }

    private static string FormatTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    private static string BuildQuery(params (string Name, string? Value)[] values) =>
        string.Join(
            "&",
            values
                .Where(value => !string.IsNullOrEmpty(value.Value))
                .Select(value => $"{value.Name}={Uri.EscapeDataString(value.Value!)}"));

    private static string Required(XElement root, string name) =>
        root.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value
        ?? throw new InvalidDataException($"The user delegation key response omitted {name}.");

    private sealed record UserDelegationKey(
        string SignedObjectId,
        string SignedTenantId,
        string SignedStart,
        string SignedExpiry,
        string SignedService,
        string SignedVersion,
        string Value);
}
