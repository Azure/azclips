// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Core.Cli.Recovery;

internal static class TerminalSession
{
    internal static uint GetParentProcessId()
    {
        if (OperatingSystem.IsWindows())
            return GetWindowsParentProcessId();
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Terminal association is unavailable on this platform.");

        var parent = GetParentId();
        return parent > 0
            ? (uint)parent
            : throw new InvalidOperationException("Could not determine the parent shell process.");
    }

    private static uint GetWindowsParentProcessId()
    {
        var status = NtQueryInformationProcess(
            new IntPtr(-1), 0, out var information, Marshal.SizeOf<ProcessBasicInformation>(), out _);
        if (status != 0 || information.ParentProcessId == 0 || information.ParentProcessId > uint.MaxValue)
            throw new InvalidOperationException($"Could not determine the parent shell process (NTSTATUS 0x{status:X8}).");
        return (uint)information.ParentProcessId;
    }

    [DllImport("libc", EntryPoint = "getppid")]
    private static extern int GetParentId();

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr process, int informationClass, out ProcessBasicInformation information,
        int informationLength, out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr ExitStatus;
        public IntPtr PebBaseAddress;
        public UIntPtr AffinityMask;
        public IntPtr BasePriority;
        public UIntPtr ProcessId;
        public UIntPtr ParentProcessId;
    }
}
