// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Runtime;

/// <summary>
/// Single owner of console rendering for diagnostic output: color decision, stream selection,
/// redirection guard, and the canonical <c>LEVEL: message</c> prefix.
/// </summary>
/// <remarks>
/// Callers that need diagnostic prefixing use the high-level methods
/// (<see cref="Error"/>, <see cref="Warn"/>, <see cref="Info"/>, <see cref="Verbose"/>, <see cref="Debug"/>).
/// Interactive callers that want to write their own text (no prefix, custom color)
/// use the low-level primitives (<see cref="EmitStderr"/>, <see cref="EmitStderrPlain"/>, <see cref="EmitStdout"/>).
/// The sink does not gate on <c>core.logging.level</c> — callers apply their own gate before invoking it.
/// The sink does not attach telemetry events — callers that want that behavior wrap the call site.
/// </remarks>
public static class ConsoleSink
{
    /// <summary>
    /// Writes <paramref name="line"/> to <see cref="Console.Error"/> using the requested color,
    /// with color suppressed per <see cref="ColorPolicy"/> when stderr is redirected.
    /// </summary>
    public static void EmitStderr(ConsoleColor color, string line)
    {
        if (!ColorPolicy.ShouldEmitColor(Console.IsErrorRedirected))
        {
            Console.Error.WriteLine(line);
            return;
        }
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        try     { Console.Error.WriteLine(line); }
        finally { Console.ForegroundColor = originalColor; }
    }

    /// <summary>
    /// Writes <paramref name="line"/> to <see cref="Console.Error"/> without setting a color.
    /// </summary>
    public static void EmitStderrPlain(string line) => Console.Error.WriteLine(line);

    /// <summary>
    /// Writes <paramref name="text"/> to <see cref="Console.Out"/> using the requested color,
    /// with color suppressed per <see cref="ColorPolicy"/> when stdout is redirected.
    /// </summary>
    /// <param name="newline">When <c>true</c>, uses <see cref="Console.WriteLine(string)"/>; otherwise <see cref="Console.Write(string)"/>.</param>
    public static void EmitStdout(ConsoleColor color, string text, bool newline = false)
    {
        if (!ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected))
        {
            if (newline) Console.WriteLine(text);
            else Console.Write(text);
            return;
        }
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        try
        {
            if (newline) Console.WriteLine(text);
            else Console.Write(text);
        }
        finally { Console.ForegroundColor = originalColor; }
    }

    /// <summary>Emits an <c>ERROR:</c>-prefixed diagnostic in red on stderr.</summary>
    public static void Error(string message) => EmitStderr(ConsoleColor.Red, $"ERROR: {message}");

    /// <summary>Emits a <c>WARNING:</c>-prefixed diagnostic in yellow on stderr.</summary>
    public static void Warn(string message) => EmitStderr(ConsoleColor.Yellow, $"WARNING: {message}");

    /// <summary>Emits an <c>INFO:</c>-prefixed diagnostic on stderr (no color).</summary>
    public static void Info(string message) => EmitStderrPlain($"INFO: {message}");

    /// <summary>Emits a <c>VERBOSE:</c>-prefixed diagnostic in dark gray on stderr.</summary>
    public static void Verbose(string message) => EmitStderr(ConsoleColor.DarkGray, $"VERBOSE: {message}");

    /// <summary>Emits a <c>DEBUG:</c>-prefixed diagnostic in dark gray on stderr.</summary>
    public static void Debug(string message) => EmitStderr(ConsoleColor.DarkGray, $"DEBUG: {message}");
}
