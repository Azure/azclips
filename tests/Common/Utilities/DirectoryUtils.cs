// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Tests.Common.Utilities;

public static class DirectoryUtils
{
    /// <summary>
    /// Best-effort filesystem cleanup for test and benchmark scratch directories.
    /// </summary>
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
        catch (DirectoryNotFoundException)
        {
            // Raced with another cleanup; treat as success.
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
