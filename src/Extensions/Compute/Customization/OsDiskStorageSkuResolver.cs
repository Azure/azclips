// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors;

// Accepts SKU or os=SKU. Data-disk selectors are unsupported until vm create supports data disks.
internal static class OsDiskStorageSkuResolver
{
    private const string Usage = "Specify a SKU, or os=SKU.";

    internal static string? Resolve(string? storageSku)
    {
        if (storageSku is null)
        {
            return null;
        }

        int separatorIndex = storageSku.IndexOf('=');
        if (separatorIndex < 0)
        {
            return storageSku;
        }

        string diskId = storageSku[..separatorIndex].Trim();

        if (int.TryParse(diskId, out _))
        {
            throw AzclipsErrorException.UsageError("Data-disk SKU assignments are not supported. Specify a SKU or os=SKU.");
        }

        if (!diskId.Equals("os", StringComparison.OrdinalIgnoreCase))
        {
            throw AzclipsErrorException.UsageError($"A sku ID is incorrect. {Usage}");
        }

        string storageAccountType = storageSku[(separatorIndex + 1)..].Trim();
        if (storageAccountType.Length == 0)
        {
            throw AzclipsErrorException.UsageError($"A sku's format is incorrect. {Usage}");
        }

        return storageAccountType;
    }
}
