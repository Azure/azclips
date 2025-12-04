namespace Core.Abstractions;

public interface IAuthService
{
    Task<string> AuthenticateAsync(CancellationToken cancellationToken = default);
}
