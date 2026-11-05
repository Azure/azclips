// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Net.Http.Headers;

namespace Tests.Recording;

/// <summary>Decides whether a body is stored as text or as Base64 bytes.</summary>
public static class RecordedBody
{
    private static readonly UTF8Encoding s_strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static async Task<string?> ReadTextAsync(HttpContent content, byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;
        // Ranges may split text characters or contain a BOM; their bytes must match the integrity headers.
        if (content.Headers.Contains("Content-Range"))
            return null;
        // Content-encoded bytes must not be interpreted using the decoded media type's charset.
        if (content.Headers.ContentEncoding.Any(encoding => !encoding.Equals("identity", StringComparison.OrdinalIgnoreCase)))
            return null;
        var type = content.Headers.ContentType;
        string? mediaType = type?.MediaType;
        bool textual = !string.IsNullOrEmpty(type?.CharSet) ||
            mediaType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true ||
            mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true ||
            mediaType?.EndsWith("+xml", StringComparison.OrdinalIgnoreCase) == true ||
            mediaType?.ToLowerInvariant() is "application/json" or "application/xml" or
                "application/javascript" or "application/x-www-form-urlencoded" or "application/yaml";
        if (textual || mediaType is null && (HasBom(bytes) || IsUtf8(bytes)))
            return await content.ReadAsStringAsync();
        return null;
    }

    public static Encoding TextEncoding(IReadOnlyDictionary<string, string[]> headers)
    {
        string? contentType = headers.FirstOrDefault(header =>
            header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value?.FirstOrDefault();
        string? charset = contentType is null ? null : MediaTypeHeaderValue.Parse(contentType).CharSet?.Trim('"');
        return string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
    }

    private static bool HasBom(byte[] bytes) =>
        bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ||
        bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble) ||
        bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble) ||
        bytes.AsSpan().StartsWith(Encoding.UTF32.Preamble) ||
        bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff });

    private static bool IsUtf8(byte[] bytes)
    {
        try
        {
            s_strict.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
