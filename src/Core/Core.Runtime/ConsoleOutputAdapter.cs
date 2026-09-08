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
        ConsoleSink.EmitStderr(ConsoleColor.Yellow, message);
        return Task.CompletedTask;
    }

    public Task WriteErrorAsync(string message)
    {
        ConsoleSink.EmitStderr(ConsoleColor.Red, message);
        return Task.CompletedTask;
    }

    public Task WriteColoredAsync(string message, OutputColor color)
    {
        ConsoleSink.EmitStdout(MapColor(color), message);
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
