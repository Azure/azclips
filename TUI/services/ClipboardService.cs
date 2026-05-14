using System.Diagnostics;

namespace Core.Tui.Services;

/// <summary>
/// Cross-platform system clipboard access via platform-native tools.
/// Windows: clip.exe / powershell Get-Clipboard
/// macOS: pbcopy / pbpaste
/// Linux: xclip -selection clipboard
/// </summary>
static class ClipboardService
{
    public static void SetText(string text)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                RunPipe("clip.exe", "", text);
            else if (OperatingSystem.IsMacOS())
                RunPipe("pbcopy", "", text);
            else
                RunPipe("xclip", "-selection clipboard", text);
        }
        catch { /* clipboard not available — silently ignore */ }
    }

    public static string? GetText()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return RunCapture("powershell.exe", "-NoProfile -Command Get-Clipboard");
            else if (OperatingSystem.IsMacOS())
                return RunCapture("pbpaste", "");
            else
                return RunCapture("xclip", "-selection clipboard -o");
        }
        catch { return null; }
    }

    private static void RunPipe(string fileName, string arguments, string input)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.WaitForExit(2000);
    }

    private static string? RunCapture(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        var result = process.StandardOutput.ReadToEnd();
        process.WaitForExit(2000);
        return string.IsNullOrEmpty(result) ? null : result.TrimEnd('\r', '\n');
    }
}
