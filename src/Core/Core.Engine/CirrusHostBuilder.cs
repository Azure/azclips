using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// Non-static builder for constructing a Cirrus DI container.
/// Replaces the static <see cref="Bootstrapper"/> pattern for new hosts.
///
/// Usage:
///   var builder = new CirrusHostBuilder();
///   builder.Services.AddCirrusCore();
///   builder.Services.AddCirrusCli();   // or .AddCirrusPS()
///   var sp = await builder.BuildAsync();
/// </summary>
public class CirrusHostBuilder
{
    public IServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// Builds the service provider and runs async initialization
    /// (telemetry warm-up, initial logging, etc.).
    /// </summary>
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
