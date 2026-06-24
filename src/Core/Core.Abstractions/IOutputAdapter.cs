// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Abstractions;

/// <summary>
/// Host-agnostic output adapter for CLI and PowerShell
/// </summary>
public interface IOutputAdapter
{
    /// <summary>
    /// Write a line of text to the output
    /// </summary>
    Task WriteLineAsync(string message);

    /// <summary>
    /// Write text without a newline (for streaming)
    /// </summary>
    Task WriteAsync(string message);

    /// <summary>
    /// Read a line of input from the user
    /// </summary>
    Task<string?> ReadLineAsync(string prompt);

    /// <summary>
    /// Write a warning message
    /// </summary>
    Task WriteWarningAsync(string message);

    /// <summary>
    /// Write an error message
    /// </summary>
    Task WriteErrorAsync(string message);

    /// <summary>
    /// Write colored text (for agent responses, spinners, etc.)
    /// </summary>
    Task WriteColoredAsync(string message, OutputColor color);

    /// <summary>
    /// Check if the adapter supports interactive input
    /// </summary>
    bool IsInteractive { get; }
}

/// <summary>
/// Common colors for output
/// </summary>
public enum OutputColor
{
    Default,
    Cyan,
    Yellow,
    Red,
    Green
}
