// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tests.Common.Utilities;

/// <summary>
/// Best-effort filesystem cleanup for test and benchmark scratch directories.
/// </summary>
public static class TempFileCleanup
{
    /// <summary>
    /// Deletes <paramref name="path"/> and everything under it, ignoring a missing directory and
    /// the transient lock and permission failures that cleanup routinely races against. Returns
    /// <see langword="true"/> when the directory is gone afterwards.
    /// </summary>
    /// <remarks>
    /// Callers use this where a leftover directory is harmless but a thrown exception is not —
    /// teardown paths, and setup paths that reset stale state from an earlier run.
    /// </remarks>
    public static bool TryDeleteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            return true;
        }
        catch (IOException)
        {
            // A file under the directory is still open, typically an assembly held by a load
            // context that outlives the test.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Read-only bits, or another process holding a handle.
            return false;
        }
    }
}
