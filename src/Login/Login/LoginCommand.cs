using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Login;

public class LoginCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        string token = await serviceProvider.GetRequiredService<IAuthService>().AuthenticateAsync();
        Console.WriteLine($"Bearer token: {token[..4]}...{token[^4..]}");
        Console.ForegroundColor = previousColor;
        return;
    }

}
