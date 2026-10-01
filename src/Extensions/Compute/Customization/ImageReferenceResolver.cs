// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.Extensions.Compute.Models;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

internal static class ImageReferenceResolver
{
    internal static ImageReference? ResolveMarketplaceUrn(string image)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        var value = image.Trim();
        if (TryParseUrn(value, out var urn))
        {
            return urn;
        }

        throw new NotImplementedException("Only marketplace image URNs are supported by azclips vm create --image currently.");
    }

    private static ImageReference Urn(string publisher, string offer, string sku, string version) => new()
    {
        Publisher = publisher,
        Offer = offer,
        Sku = sku,
        Version = version
    };

    private static bool TryParseUrn(string value, out ImageReference imageReference)
    {
        var parts = value.Split(':');
        if (parts.Length == 4 && parts.All(part => !string.IsNullOrWhiteSpace(part)))
        {
            imageReference = Urn(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        imageReference = null!;
        return false;
    }

}
