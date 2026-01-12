namespace Core.Abstractions;

/// <summary>
/// Provides logging functionality with support for multiple severity levels.
/// </summary>
public interface ILoggingService
{
    /// <summary>
    /// Logs a general message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Log(string message);

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The informational message to log.</param>
    void Info(string message);

    /// <summary>
    /// Logs a verbose message. Explain what the program is doing with midium detail.
    /// </summary>
    /// <param name="message">The verbose message to log.</param>
    void Verbose(string message);

    /// <summary>
    /// Logs a debug message for development and troubleshooting purposes with high detail.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    void Debug(string message);

    /// <summary>
    /// Logs a warning message indicating a potential issue.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    void Warn(string message);

    /// <summary>
    /// Logs an error message indicating a failure or critical issue.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    void Error(string message);
}
