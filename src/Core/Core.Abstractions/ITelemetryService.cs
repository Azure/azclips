namespace Core.Abstractions;

public interface ITelemetryService
{
    void Log(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}
