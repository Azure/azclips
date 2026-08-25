// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Core.Cli.Telemetry;

/// <summary>
/// Keeps a detached background child from holding on to the CLI's own stdio.
/// </summary>
/// <remarks>
/// A child that outlives <c>az</c> must not keep the caller's stdout open: a pipe reader
/// (<c>az ... | jq</c>) only observes EOF once every writer has closed its end, so an
/// inherited handle makes the shell wait for the background upload rather than for the
/// command. Redirecting the child's streams covers this on Unix, where exec replaces
/// descriptors 0-2 outright. Windows needs one more step, because <c>Process.Start</c>
/// always calls <c>CreateProcess</c> with <c>bInheritHandles: true</c> — that duplicates
/// <em>every</em> inheritable handle into the child, including the standard handles the
/// shell marked inheritable when it handed us the pipe. Clearing that flag first is what
/// keeps the duplicate out of the child.
/// </remarks>
internal static class DetachedStdio
{
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
