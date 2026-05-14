using Core.Abstractions;

namespace Core.Tui.Services;

/// <summary>
/// TUI-aware logging service that suppresses verbose/debug console output
/// to prevent raw text from corrupting the terminal UI.
/// </summary>
class TuiLoggingService : ILoggingService
{
    public void Log(string message) { }
    public void Info(string message) { }
    public void Verbose(string message) { }
    public void Debug(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}
