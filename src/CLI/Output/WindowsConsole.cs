// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Core.Cli.Output;

internal static class WindowsConsole
{
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const int ErrorInvalidHandle = 6;
    private const uint EnableProcessedOutput = 0x1;
    private const uint EnableVirtualTerminalProcessing = 0x4;

    internal static bool Initialize()
    {
        if (!OperatingSystem.IsWindows()) return true;

        return Initialize(Console.IsOutputRedirected, Console.IsErrorRedirected, TryEnable, Console.Error);
    }

    internal static bool Initialize(
        bool outputRedirected, bool errorRedirected, Func<int, int?> enable, TextWriter errors)
    {
        var outputError = outputRedirected ? null : enable(StdOutputHandle);
        var errorError = errorRedirected ? null : enable(StdErrorHandle);

        if (outputError.HasValue) Warn(errors, "stdout", outputError.Value);
        if (errorError.HasValue) Warn(errors, "stderr", errorError.Value);
        return !outputError.HasValue && !errorError.HasValue;
    }

    private static int? TryEnable(int handleId)
    {
        var handle = GetStdHandle(handleId);
        if (handle == IntPtr.Zero) return null;
        if (handle == new IntPtr(-1)) return Marshal.GetLastPInvokeError();

        if (!GetConsoleMode(handle, out var mode))
        {
            var error = Marshal.GetLastPInvokeError();
            // Pipes, files and terminal emulators without console handles have no console mode.
            return error == ErrorInvalidHandle ? null : error;
        }

        // PowerShell can disable VT processing when launching native applications.
        var enabledMode = mode | EnableProcessedOutput | EnableVirtualTerminalProcessing;
        return enabledMode == mode || SetConsoleMode(handle, enabledMode)
            ? null
            : Marshal.GetLastPInvokeError();
    }

    private static void Warn(TextWriter errors, string stream, int error) =>
        errors.WriteLine(
            $"WARNING: Unable to enable ANSI formatting on {stream} " +
            $"(Windows error {error}: {new Win32Exception(error).Message}). Continuing with plain output.");

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
