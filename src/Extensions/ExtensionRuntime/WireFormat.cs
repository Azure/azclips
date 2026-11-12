// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.Azclips.ExtensionRuntime;

using System.Globalization;

/// <summary>
/// Renders request values the way they go on the wire. Culture-invariant, so a request
/// built on a German or Swedish machine is the request built anywhere else.
/// </summary>
public static class WireFormat
{
    /// <summary>
    /// The wire text of a path, query, or header value: <c>null</c> is empty, a string is
    /// itself, a boolean is lowercase, bytes are base64, and numbers and other formattable
    /// values use the invariant culture — a float in its shortest round-trip form.
    /// </summary>
    public static string Format(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        bool flag => flag ? "true" : "false",
        byte[] bytes => Convert.ToBase64String(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Appends <c>name=value</c> to <paramref name="uri"/>'s query string, the value
    /// formatted by <see cref="Format"/> and escaped. A <c>null</c> value appends nothing, so
    /// an unset optional parameter is left off the request rather than sent empty.
    /// </summary>
    /// <remarks>
    /// <paramref name="name"/> is appended as written: it is a constant from the spec, and
    /// some (<c>$filter</c>) must reach the service unescaped.
    /// </remarks>
    public static string WithQuery(this string uri, string name, object? value)
    {
        if (value is null)
        {
            return uri;
        }

        char separator = uri.Contains('?') ? '&' : '?';
        return $"{uri}{separator}{name}={Uri.EscapeDataString(Format(value))}";
    }

    /// <summary>
    /// Appends a collection using its declared delimiter, or repeated query entries when
    /// <paramref name="delimiter"/> is null. Null and empty collections append nothing.
    /// Each item uses <see cref="Format"/> before the query value is escaped.
    /// </summary>
    public static string WithQuery<T>(this string uri, string name, IEnumerable<T>? values, string? delimiter)
    {
        if (values is null)
        {
            return uri;
        }

        if (delimiter is null)
        {
            foreach (T value in values)
            {
                uri = uri.WithQuery(name, Format(value));
            }
            return uri;
        }

        string[] formatted = values.Select(value => Format(value)).ToArray();
        return formatted.Length == 0 ? uri : uri.WithQuery(name, string.Join(delimiter, formatted));
    }
}
