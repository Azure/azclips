namespace Core.Abstractions;

public interface ILoggerService
{
    void Log(string message);
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}
