using Core.Abstractions;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var serviceProvider = await Bootstrapper.ServiceProviderAsync;
        var commandEngine = serviceProvider.GetRequiredService<ICommandEngine>();
        await commandEngine.ExecuteCommandAsync(args, default);
    }
}
