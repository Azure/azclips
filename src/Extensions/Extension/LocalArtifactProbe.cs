// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

/// <summary>
/// On `extension add --name` feed-miss, probes the local repo (artifacts/Debug/) for a freshly
/// built .nupkg and surfaces it as an --path suggestion. Suggestion-only; never auto-installs.
/// </summary>
public static class LocalArtifactProbe
{
    /// <summary>
    /// Probes the local repo for a .nupkg matching <paramref name="packageId"/>. Returns the
    /// newest match under <c>artifacts/Debug/</c>, or null on any failure (no slnx, no match, IO).
    /// </summary>
    public static string? Find(string packageId, string? startDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !IsValidPackageId(packageId))
        {
            return null;
        }

        try
        {
            var repoRoot = FindRepoRoot(startDirectory ?? Directory.GetCurrentDirectory());
            if (repoRoot is null)
            {
                return null;
            }

            // Debug only by design; surfacing both Debug+Release would noise up the message.
            var packageDir = Path.Combine(repoRoot, "artifacts", "Debug", packageId);
            if (!Directory.Exists(packageDir))
            {
                return null;
            }

            // Pick the most recently written nupkg so the suggestion matches "what you just built."
            var candidates = Directory.EnumerateFiles(packageDir, $"{packageId}.*.nupkg", SearchOption.TopDirectoryOnly);
            return candidates
                .Select(p => new FileInfo(p))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Walks up the directory tree from <paramref name="startDirectory"/> looking for
    /// <c>cirrus.slnx</c>. Returns the directory containing it, or <c>null</c> if the walk
    /// reaches a root without finding one.
    /// </summary>
    private static string? FindRepoRoot(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cirrus.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Validates that <paramref name="packageId"/> conforms to NuGet package id grammar
    /// (ASCII letters, digits, '.', '-', '_'). Defense-in-depth: <c>packageId</c> is
    /// user-influenced (dotted tokens pass through <c>Add.ResolvePackageId</c>), so a
    /// path-traversal token like <c>"..\..\Windows\System32"</c> must not be combined with
    /// <c>repoRoot</c> and probed.
    /// </summary>
    /// <remarks>
    /// Strictly ASCII: <c>char.IsLetterOrDigit</c> would accept non-ASCII letters/digits
    /// (e.g. <c>'é'</c> or Roman numeral <c>'Ⅳ'</c>) which lie outside NuGet's grammar.
    /// </remarks>
    private static bool IsValidPackageId(string packageId)
    {
        foreach (var c in packageId)
        {
            bool isAsciiLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
            bool isAsciiDigit  = c >= '0' && c <= '9';
            if (!(isAsciiLetter || isAsciiDigit || c == '.' || c == '-' || c == '_'))
            {
                return false;
            }
        }
        return true;
    }
}
