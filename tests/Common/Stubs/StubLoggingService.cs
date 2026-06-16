using Core.Abstractions;

namespace Tests.Common.Stubs;

public class StubLoggingService : ILoggingService
{
    public void Log(string message) { }
    public void Info(string message) { }
    public void Error(string message) { }
    public void Warn(string message) { }
    public void Verbose(string message) { }
    public void Debug(string message) { }
}
