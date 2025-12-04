using System.Diagnostics;

namespace Group;

/// <summary>
/// Helper to execute azv2 CLI from PowerShell cmdlets
/// </summary>
public static class CliRunner
{
    public static void ExecuteCommand(string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "azv2",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        // Set environment variable to indicate PowerShell flavor
        startInfo.EnvironmentVariables["AZURECLI_FLAVOR"] = "azure powershell";

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start azv2 CLI");
        }

        // Forward stdin/stdout/stderr
        var outputTask = Task.Run(() =>
        {
            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                if (line != null)
                {
                    Console.WriteLine(line);
                }
            }
        });

        var errorTask = Task.Run(() =>
        {
            while (!process.StandardError.EndOfStream)
            {
                var line = process.StandardError.ReadLine();
                if (line != null)
                {
                    Console.Error.WriteLine(line);
                }
            }
        });

        // Forward input from console to process
        var inputTask = Task.Run(() =>
        {
            try
            {
                while (!process.HasExited)
                {
                    if (Console.KeyAvailable || !Console.IsInputRedirected)
                    {
                        var line = Console.ReadLine();
                        if (line != null)
                        {
                            process.StandardInput.WriteLine(line);
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch
            {
                // Input redirection ended or process exited
            }
        });

        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"azv2 CLI exited with code {process.ExitCode}");
        }
    }
}
