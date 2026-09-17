// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Models;

/// <summary>
/// Serializes a <see cref="DateTimeOffset"/> as Azure CLI's legacy <c>expiresOn</c> string
/// (local time, <c>yyyy-MM-dd HH:mm:ss.ffffff</c>, e.g. <c>2020-11-12 13:50:47.114324</c>) so the
/// CLI JSON output is a drop-in match for <c>az account get-access-token</c>.
/// </summary>
/// <remarks>
/// Azure CLI itself marks this field deprecated: in <c>azure-cli-core/_profile.py</c> the comment
/// reads "expiresOn is deprecated and will be removed in future release." It is a machine-local time
/// with no UTC offset, so it is timezone-ambiguous and cannot be reliably parsed back to an instant;
/// the unambiguous replacement is the epoch <c>expires_on</c> field (see
/// <see cref="AccessTokenResult.ExpiresOnUnixTime"/>). We reproduce the deprecated shape here only so
/// existing <c>az</c> scripts that still read <c>expiresOn</c> keep working. Local time is
/// intentional - it mirrors Python's <c>datetime.fromtimestamp(...)</c>. This converter only affects
/// JSON (CLI) output; PowerShell surfaces <see cref="AccessTokenResult.ExpiresOn"/> as a real
/// <see cref="DateTimeOffset"/> via the object pipeline.
/// </remarks>
public sealed class AzureCliExpiresOnConverter : JsonConverter<DateTimeOffset>
{
    private const string Format = "yyyy-MM-dd HH:mm:ss.ffffff";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrEmpty(text))
        {
            throw new JsonException("Expected a non-empty expiresOn string.");
        }

        return DateTimeOffset.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        // Classic az derives expiresOn from the integer epoch (datetime.fromtimestamp(expires_on)), so it
        // always renders whole-second precision (".000000"). Truncate sub-second ticks so our output is
        // byte-for-byte compatible instead of leaking the token's fractional expiry.
        var wholeSecond = value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
        writer.WriteStringValue(wholeSecond.ToLocalTime().ToString(Format, CultureInfo.InvariantCulture));
    }
}
