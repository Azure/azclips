// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>Filesystem roots shared by extension loading and help-catalog invalidation.</summary>
public static class ExtensionDiscoveryPaths
{
    public const string PackagePrefix = "Microsoft.Azure.Azclips.Extensions.";
    public static IReadOnlyList<string> PackagePrefixes { get; } = [PackagePrefix];

    public static string GetDefaultBundledRoot(string baseDirectory)
    {
#if DEBUG
        return Path.GetFullPath(Path.Combine(baseDirectory, ".."));
#else
        return Path.Combine(baseDirectory, "Extensions");
#endif
    }

    public static IReadOnlyList<string> GetBundledRoots(string baseDirectory) =>
        new[]
        {
            GetDefaultBundledRoot(baseDirectory),
            Path.GetFullPath(Path.Combine(baseDirectory, "..")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "Debug")),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "Release")),
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> GetHelpRoots(string baseDirectory)
    {
        var roots = new List<string>();
        for (var current = new DirectoryInfo(baseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "azclips.slnx")))
            {
                roots.Add(Path.Combine(current.FullName, "src", "CLI", "Help", "Docs"));
                break;
            }
        }

        roots.Add(Path.Combine(baseDirectory, "help"));
        return roots;
    }
}
