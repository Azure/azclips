using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Login;

public class LoginCommand : ICommand
{
    public async Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        string token = await serviceProvider.GetRequiredService<IAuthService>().AcquireTokenInteractiveAsync();

        var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        telemetryService.Log($"Token acquired interactively: {token[..4]}...{token[^4..]}");

        Console.ForegroundColor = previousColor;
        return;
    }
}
