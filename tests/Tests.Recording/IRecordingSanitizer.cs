// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace Tests.Recording;

/// <summary>
/// Rewrites one class of secret out of a recorded value (URI, header value, or body). Applied at
/// record time so nothing live reaches disk, and to outgoing requests at playback time so they
/// still match the sanitised recording.
/// </summary>
public interface IRecordingSanitizer
{
    string Name { get; }

    string Sanitize(string value);
}

/// <summary>A sanitizer that replaces every match of a pattern with a fixed placeholder.</summary>
public sealed class RegexSanitizer(string name, string pattern, string replacement) : IRecordingSanitizer
{
    private readonly Regex _regex = new(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Name { get; } = name;

    public string Sanitize(string value) => _regex.Replace(value, replacement);
}
