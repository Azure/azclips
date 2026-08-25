// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Core.Cli.Telemetry;

/// <summary>
/// Keeps a detached background child from holding on to the CLI's own stdio.
/// </summary>
internal static class DetachedStdio
{
    // Defined in accordance to https://learn.microsoft.com/en-us/windows/console/getstdhandle#parameters
    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const int HandleFlagInherit = 0x1;

    private static readonly IntPtr InvalidHandleValue = new(-1);

    /// <summary>
    /// Marks this process's standard handles non-inheritable so the next child does not
    /// receive a duplicate of them. No-op on non-Windows platforms.
    /// </summary>
    /// <remarks>
    /// Process-wide and irreversible for the remaining lifetime of the CLI, so call it
    /// only immediately before spawning a detached child on the way out.
    /// </remarks>
    internal static void SuppressStandardHandleInheritance()
    {
        if (!OperatingSystem.IsWindows()) return;

        foreach (var handleId in new[] { StdInputHandle, StdOutputHandle, StdErrorHandle })
        {
            try
            {
                var handle = GetStdHandle(handleId);
                if (handle == IntPtr.Zero || handle == InvalidHandleValue) continue;

                // Best effort: a handle we cannot reconfigure just keeps its current
                // flags. Never let this stop the spawn — the upload still matters.
                SetHandleInformation(handle, HandleFlagInherit, 0);
            }
            catch
            {
                // Ignore and keep going with the remaining handles.
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);
}
