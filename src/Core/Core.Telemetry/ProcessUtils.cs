// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Core.Telemetry;

public static class ProcessUtils
{
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
}
