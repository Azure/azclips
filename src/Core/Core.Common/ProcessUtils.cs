using System.Diagnostics;

namespace Core.Common;

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
