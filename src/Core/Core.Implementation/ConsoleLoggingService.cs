using Core.Abstractions;

namespace Core.Implementation;

/// <summary>
/// Simple console-based telemetry logger.
/// </summary>
public class ConsoleLoggingService : ILoggingService
{
    public void Log(string message)
    {
        Console.WriteLine($"{message}");
    }

    public void Info(string message)
    {
        Console.WriteLine($"[INFO] {message}");
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    public void Warn(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] {message}");
        Console.ResetColor();
    }

    public void Verbose(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[VERBOSE] {message}");
        Console.ResetColor();
    }

    public void Debug(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[DEBUG] {message}");
        Console.ResetColor();
    }
}
