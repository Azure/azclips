namespace Core.Abstractions;

public interface IAuthService
{
    Task<string> AcquireTokenInteractiveAsync(CancellationToken cancellationToken = default);
    Task<string> AcquireTokenSilentlyAsync(bool promptOnError = false, CancellationToken cancellationToken = default);
}
