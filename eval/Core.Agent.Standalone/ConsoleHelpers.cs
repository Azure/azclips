namespace Core.Agent.Standalone;

/// <summary>
/// Console output helpers for formatting and colorization.
/// </summary>
internal static class ConsoleHelpers
{
    /// <summary>
    /// Writes a message in cyan color followed by a blank line.
    /// </summary>
    public static void WriteCyan(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(message);
        Console.ResetColor();
        Console.WriteLine();
    }
}
