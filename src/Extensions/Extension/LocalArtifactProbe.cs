// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

/// <summary>
/// Closes the dev-loop gap where <c>extension add --name &lt;pkg&gt;</c> fails because no NuGet
/// feed is configured. When the feed lookup misses, the host probes the local repository for a
/// freshly built <c>.nupkg</c> matching the requested package id and surfaces it as an
/// <c>--path</c> suggestion in the error message. This is *suggestion only* — we never
/// auto-install from local artifacts, because doing so would silently pick up stale builds and
/// turn a loud "not found" into a quiet "wrong bits."
/// </summary>
/// <remarks>
/// Behavior: walks up from <see cref="Directory.GetCurrentDirectory"/> looking for
/// <c>cirrus.slnx</c> as the repo-root anchor. If found, globs
/// <c>artifacts/Debug/{packageId}/{packageId}.*.nupkg</c> and returns the most recently written
/// match. Only Debug is probed — surfacing one path keeps the message focused, and a developer
/// who builds Release locally will edit the suggested command to swap <c>Debug</c> for
/// <c>Release</c>. Returns <c>null</c> when no slnx is found (e.g. user is running deployed
/// <c>azv2</c> outside the repo) or no artifact matches.
/// </remarks>
public static class LocalArtifactProbe
{
    /// <summary>
    /// Probes the local repository for a <c>.nupkg</c> matching <paramref name="packageId"/>.
    /// Returns the absolute path to the newest matching nupkg, or <c>null</c> if none is found.
    /// Never throws; failure modes (no slnx, no artifacts dir, IO errors) all return <c>null</c>.
    /// </summary>
    /// <param name="packageId">Fully qualified NuGet package id (e.g.
    /// <c>Microsoft.Azure.Cirrus.Extensions.Compute</c>).</param>
    /// <param name="startDirectory">Directory to begin the walk-up from. Defaults to
    /// <see cref="Directory.GetCurrentDirectory"/>; overridable for tests.</param>
    public static string? Find(string packageId, string? startDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(packageId))
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

            // Single search location per design: Debug only. Developers building Release locally
            // will substitute the path manually — surfacing both options would make the error
            // message noisier without measurable benefit.
            var packageDir = Path.Combine(repoRoot, "artifacts", "Debug", packageId);
            if (!Directory.Exists(packageDir))
            {
                return null;
            }

            // Multiple versions on disk are common during dev (rebuild after version bump). Pick
            // the most recently written so the suggestion matches "what you just built."
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
}
