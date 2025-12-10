namespace Core.Abstractions;

public interface IAuthService
{
    Task<AuthenticationInfo> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    AuthenticationInfo? GetCurrentContext();
    Task<string> AcquireTokenSilentlyAsync(string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default);
}
