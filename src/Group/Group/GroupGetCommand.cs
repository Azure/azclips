using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Group;

public class GroupGetCommand : ICommand
{
    public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;

        var authService = serviceProvider.GetRequiredService<IAuthService>();
        var token = authService.AcquireTokenSilentlyAsync(promptOnError: false, cancellationToken).GetAwaiter().GetResult();

        var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        telemetryService.Log($"Token acquired silently: {token[..4]}...{token[^4..]}");

        Console.ForegroundColor = previousColor;
        return Task.CompletedTask;
    }
}
