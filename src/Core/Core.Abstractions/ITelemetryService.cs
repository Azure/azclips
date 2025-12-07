namespace Core.Abstractions;

public interface ITelemetryService
{
    void Log(string message);
}
