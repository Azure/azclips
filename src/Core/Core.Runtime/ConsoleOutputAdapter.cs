// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Runtime;

/// <summary>
/// Console implementation of IOutputAdapter
/// </summary>
public class ConsoleOutputAdapter : IOutputAdapter
{
    public bool IsInteractive => !Console.IsInputRedirected;

    public Task WriteLineAsync(string message)
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    }

    public Task WriteAsync(string message)
    {
        Console.Write(message);
        return Task.CompletedTask;
    }

    public Task<string?> ReadLineAsync(string prompt)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        return Task.FromResult(input);
    }

    public Task WriteWarningAsync(string message)
    {
        var emitColor = ColorPolicy.ShouldEmitColor(Console.IsErrorRedirected);
        var originalColor = Console.ForegroundColor;
        if (emitColor) Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Error.WriteLine(message);
        if (emitColor) Console.ForegroundColor = originalColor;
        return Task.CompletedTask;
    }

    public Task WriteErrorAsync(string message)
    {
        var emitColor = ColorPolicy.ShouldEmitColor(Console.IsErrorRedirected);
        var originalColor = Console.ForegroundColor;
        if (emitColor) Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        if (emitColor) Console.ForegroundColor = originalColor;
        return Task.CompletedTask;
    }

    public Task WriteColoredAsync(string message, OutputColor color)
    {
        var emitColor = ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected);
        var originalColor = Console.ForegroundColor;
        if (emitColor) Console.ForegroundColor = MapColor(color);
        Console.Write(message);
        if (emitColor) Console.ForegroundColor = originalColor;
        return Task.CompletedTask;
    }

    private static ConsoleColor MapColor(OutputColor color) => color switch
    {
        OutputColor.Cyan => ConsoleColor.Cyan,
        OutputColor.Yellow => ConsoleColor.Yellow,
        OutputColor.Red => ConsoleColor.Red,
        OutputColor.Green => ConsoleColor.Green,
        _ => Console.ForegroundColor
    };
}
