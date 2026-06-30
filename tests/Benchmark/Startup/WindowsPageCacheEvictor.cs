// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Benchmark.Startup;

/// <summary>
/// In-process Windows OS page-cache evictor. Purges the system standby page list directly via the
/// memory-manager command <c>NtSetSystemInformation(SystemMemoryListInformation, MemoryPurgeStandbyList)</c>
/// </summary>
/// <remarks>
/// When a process exits, its file-backed image pages (the az.exe / pwsh DLLs and EXEs) are not freed
/// immediately — they move to the OS <em>standby list</em>, where they satisfy the next launch from RAM
/// instead of disk. That is precisely the "warm" case. Purging the standby list returns those pages to
/// the free list, so the next launch re-reads them from disk: the "cold" case we want to measure.
///
/// Purging the standby list requires <c>SeProfileSingleProcessPrivilege</c>, which the OS only grants to
/// an elevated (Administrator) process. <see cref="PurgeStandbyList"/> enables that privilege on the
/// current token, then issues the command. A non-elevated process cannot purge the cache, so callers must
/// treat that as "cannot run cold" rather than reporting a warm launch as cold.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class WindowsPageCacheEvictor
{
    // SYSTEM_INFORMATION_CLASS.SystemMemoryListInformation
    private const int SystemMemoryListInformation = 0x50;

    // SYSTEM_MEMORY_LIST_COMMAND.MemoryPurgeStandbyList
    private const int MemoryPurgeStandbyList = 4;

    private const int STATUS_SUCCESS = 0;

    private const uint SE_PRIVILEGE_ENABLED = 0x0002;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;

    // SE_PROF_SINGLE_PROCESS_NAME — the privilege required to purge the standby list.
    private const string SeProfileSingleProcessPrivilege = "SeProfileSingleProcessPrivilege";

    /// <summary>
    /// True when the current process can purge the standby list (i.e. it is elevated and the required
    /// privilege can be enabled). Does not mutate the page cache, so it is safe as an availability probe.
    /// </summary>
    public static bool CanPurge() => TryEnablePurgePrivilege();

    /// <summary>
    /// Purge the OS standby page list. Returns <c>true</c> on success, <c>false</c> when the privilege is
    /// unavailable (not elevated) or the native call reports a non-success status.
    /// </summary>
    public static bool PurgeStandbyList()
    {
        if (!TryEnablePurgePrivilege())
        {
            return false;
        }

        int command = MemoryPurgeStandbyList;
        int status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
        return status == STATUS_SUCCESS;
    }

    /// <summary>
    /// Enable <c>SeProfileSingleProcessPrivilege</c> on the current process token. Returns <c>true</c> only
    /// when the privilege was actually assigned (AdjustTokenPrivileges reports success and not
    /// ERROR_NOT_ALL_ASSIGNED), which is the reliable signal that the process is elevated.
    /// </summary>
    private static bool TryEnablePurgePrivilege()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
        {
            return false;
        }

        try
        {
            if (!LookupPrivilegeValue(null, SeProfileSingleProcessPrivilege, out var luid))
            {
                return false;
            }

            var privileges = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED },
            };

            // AdjustTokenPrivileges returns true even when it could not assign every privilege; the actual
            // outcome surfaces only via GetLastError (ERROR_NOT_ALL_ASSIGNED == 1300 for a non-elevated token).
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
            {
                return false;
            }

            return Marshal.GetLastWin32Error() == 0; // ERROR_SUCCESS
        }
        finally
        {
            CloseHandle(token);
        }
    }

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(
        int systemInformationClass, ref int systemInformation, int systemInformationLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID_AND_ATTRIBUTES Privilege;
    }
}
