using Core.Abstractions;

namespace Core.Cli;

/// <summary>
/// Simple console-based telemetry service for logging.
/// </summary>
public class ConsoleTelemetryService : ITelemetryService
{
    public void LogInfo(string message)
    {
        // Silent for clean output - can be enabled for debugging
        // Console.WriteLine($"[INFO] {message}");
    }

    public void LogError(string message, Exception? exception = null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {message}");
        if (exception != null)
        {
            Console.WriteLine($"Details: {exception.Message}");
        }
        Console.ResetColor();
    }

    public void LogWarning(string message)
    {
        // Silent for clean output
        // Console.ForegroundColor = ConsoleColor.Yellow;
        // Console.WriteLine($"[WARN] {message}");
        // Console.ResetColor();
    }
}
