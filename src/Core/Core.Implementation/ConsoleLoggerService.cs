using Core.Abstractions;

namespace Core.Implementation;

/// <summary>
/// Simple console-based telemetry service for logging.
/// </summary>
public class ConsoleLoggerService : ILoggerService
{
    public void Log(string message)
    {
        // Silent for clean output - can be enabled for debugging
        // Console.WriteLine($"[LOG] {message}");
    }

    public void LogInfo(string message)
    {
        // Silent for clean output - can be enabled for debugging
        // Console.WriteLine($"[INFO] {message}");
    }

    public void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Error: {message}");
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
