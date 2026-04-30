// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tests.Common.Utilities;

public static class RepoLocator
{
    public static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cirrus.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    public static string GetRepoRoot() =>
        FindRepoRoot(AppContext.BaseDirectory)
        ?? FindRepoRoot(Directory.GetCurrentDirectory())
        ?? throw new InvalidOperationException("Cannot find repository root (looking for cirrus.slnx)");
}
