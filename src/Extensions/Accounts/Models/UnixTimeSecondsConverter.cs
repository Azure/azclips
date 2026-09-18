// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Models;

/// <summary>
/// Serializes a <see cref="DateTimeOffset"/> as an epoch integer (Unix time in whole seconds), so CLI
/// JSON emits <c>expires_on</c> the way classic <c>az account get-access-token</c> does. CLI output only;
/// PowerShell surfaces the <see cref="DateTimeOffset"/> directly through the object pipeline.
/// </summary>
public sealed class UnixTimeSecondsConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String &&
            long.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromString))
        {
            return DateTimeOffset.FromUnixTimeSeconds(fromString);
        }

        return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64());
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToUnixTimeSeconds());
    }
}
