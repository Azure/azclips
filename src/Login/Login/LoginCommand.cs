using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Login;

public class LoginCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Bearer token: {await serviceProvider.GetRequiredService<IAuthService>().AuthenticateAsync()}");
        Console.ForegroundColor = previousColor;
        return;
    }

}
