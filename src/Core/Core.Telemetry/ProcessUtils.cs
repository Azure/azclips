// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Core.Telemetry;

public static class ProcessUtils
{
    private static readonly string[] ObservedShells = ["bash", "fish", "pwsh", "zsh"];

    public static string GetRunningProcess()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var processName = process.ProcessName;
            if (!string.IsNullOrEmpty(processName))
                return processName;
        }
        catch
        {
            // Process enumeration can be denied on restricted hosts; fall through.
        }

        return "unknown";
    }

    internal static string DetectShell(string? configuredShell, bool isWindows)
    {
        // Known Limitations:
        // $SHELL normally identifies the configured login shell, which may differ from the shell that launched this process.        
        // For WSL it will show the distribution name as the shell, ie /usr/bin/bash
        // To improve the accuracy of the shell detection we would need to check the parent process of the current process via low-level native calls
        // but that is not trivial and would require additional permissions.
        var shell = Path.GetFileNameWithoutExtension(configuredShell);
        if (shell is not null && ObservedShells.Contains(shell, StringComparer.Ordinal))
            return shell;        

        // Windows also commonly leaves SHELL env unset, 
        // this follows known dotnet patterns and assumes pwsh there for windows; cmd.exe and other
        // Windows shells will consequently be reported as pwsh without checking the parent process.
        return isWindows ? "pwsh" : "unknown";
    }
}
