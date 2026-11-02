// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Core.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter<UploadRegion>))]
public enum UploadRegion
{
    EU,
    ROW,
}

public static class UploadRegions
{
    public static bool TryParse(string? value, out UploadRegion region) =>
        Enum.TryParse(value, ignoreCase: true, out region) &&
        Enum.IsDefined(region) &&
        string.Equals(value, region.ToString(), StringComparison.OrdinalIgnoreCase);

    public static UploadRegion Resolve(
        string? region, string? legacyBoundary = null, string? legacyOutcome = null)
    {
        if (region is not null)
            return TryParse(region, out var parsed)
                ? parsed
                : throw new FormatException($"Invalid telemetry upload region '{region}'.");

        // Old records carry lookup inputs rather than a region. Preserve their original defaults.
        if (string.Equals(legacyBoundary, "EU", StringComparison.OrdinalIgnoreCase))
            return UploadRegion.EU;
        if (string.Equals(legacyBoundary, "Global", StringComparison.OrdinalIgnoreCase))
            return UploadRegion.ROW;
        return string.Equals(legacyOutcome, "TenantUnavailable", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(legacyOutcome, "AuthenticationUnavailable", StringComparison.OrdinalIgnoreCase)
            ? UploadRegion.ROW
            : UploadRegion.EU;
    }
}
