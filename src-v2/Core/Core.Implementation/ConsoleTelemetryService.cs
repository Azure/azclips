using Core.Abstractions;

namespace Core.Implementation; //todo: change Core.Implementation to Core.Impl everywhere

public class ConsoleTelemetryService : ITelemetryService
{
    public void Log(string message) => Console.WriteLine($"[{DateTime.Now}] {message}");
}
