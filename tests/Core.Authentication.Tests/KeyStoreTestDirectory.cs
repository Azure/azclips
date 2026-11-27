// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Authentication.Tests;

/// <summary>
/// A per-test scratch root for the failure-history key store. <see cref="DirectoryPath"/> sits inside the
/// root but is not created, so tests can check whether the store creates it.
/// </summary>
internal sealed class KeyStoreTestDirectory : IDisposable
{
    internal KeyStoreTestDirectory()
    {
        Root = Path.GetFullPath(Path.Combine(".test-artifacts", "failure-history", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Root);
        DirectoryPath = Path.Combine(Root, "failure-history");
    }

    internal string Root { get; }
    internal string DirectoryPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}
