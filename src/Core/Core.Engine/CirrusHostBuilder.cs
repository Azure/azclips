using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// Usage:
///   var builder = new CirrusHostBuilder();
///   builder.Services.AddCirrusCore();
///   builder.Services.AddCirrusCli();   // or .AddCirrusPS()
///   var sp = await builder.BuildAsync();
/// </summary>
public class CirrusHostBuilder
{
    public IServiceCollection Services { get; } = new ServiceCollection();

    public async Task<IServiceProvider> BuildAsync()
    {
        var serviceProvider = Services.BuildServiceProvider();

        await Task.Run(() =>
        {
            var logger = serviceProvider.GetService<ILoggingService>();
            logger?.Verbose($"[{nameof(CirrusHostBuilder)}] Cirrus host initialized.");
        });

        return serviceProvider;
    }
}
