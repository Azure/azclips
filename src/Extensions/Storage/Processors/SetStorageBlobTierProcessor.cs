// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public sealed class SetStorageBlobTierProcessor : StorageBlobProcessorBase
{
    public string Tier { get; set; } = string.Empty;
    public string? RehydratePriority { get; set; }
    public string? LeaseId { get; set; }

    public override async Task ProcessAsync()
    {
        if (string.IsNullOrWhiteSpace(Tier) || Tier.Any(char.IsControl))
        {
            throw new ArgumentException("Tier is required and cannot contain control characters.", nameof(Tier));
        }
        if (!string.IsNullOrWhiteSpace(RehydratePriority)
            && !RehydratePriority.Equals("Standard", StringComparison.OrdinalIgnoreCase)
            && !RehydratePriority.Equals("High", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("RehydratePriority must be Standard or High.", nameof(RehydratePriority));
        }

        Uri uri = new UriBuilder(BuildBlobUri()) { Query = "comp=tier" }.Uri;
        using HttpRequestMessage request = CreateRequest(HttpMethod.Put, uri);
        request.Headers.TryAddWithoutValidation("x-ms-access-tier", Tier);
        if (!string.IsNullOrWhiteSpace(RehydratePriority))
        {
            request.Headers.TryAddWithoutValidation("x-ms-rehydrate-priority", RehydratePriority);
        }
        if (!string.IsNullOrWhiteSpace(LeaseId))
        {
            request.Headers.TryAddWithoutValidation("x-ms-lease-id", LeaseId);
        }

        using HttpResponseMessage response = await SendAsync(request);
        await response.EnsureSuccessAsync(Publisher);
    }
}
