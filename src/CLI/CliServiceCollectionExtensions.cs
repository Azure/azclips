using Core.Abstractions;
using Core.Cli.Parser;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Cli;

/// <summary>
/// CLI-specific DI registrations that depend on types in the CLI project
/// (CommandResolver, IExtensionLoader implementations, ExtensionLoaderOptions).
/// <para>
/// This complements <see cref="ServiceCollectionExtensions.AddCirrusCli"/> in Core.Engine,
/// which registers types visible from the shared engine layer (IEngine, IOutputAdapter).
/// </para>
/// </summary>
public static class CliServiceCollectionExtensions
{
    /// <summary>
    /// Registers CLI parser and extension loader services.
    /// Call after <see cref="ServiceCollectionExtensions.AddCirrusCli"/>.
    /// </summary>
    public static IServiceCollection AddCirrusCliParser(this IServiceCollection services)
    {
        // Extension loader options with standard directory layout
        services.AddSingleton(_ => ExtensionLoaderOptions.CreateDefault());

        // Extension loaders — ordered: user multi-version first, then builtin fallback
        services.AddSingleton<IExtensionLoader>(sp =>
        {
            var options = sp.GetRequiredService<ExtensionLoaderOptions>();
            var logger = sp.GetService<ILoggingService>();
            return new MultiVersionExtensionLoader(options.UserExtensionsDir, logger);
        });
        services.AddSingleton<IExtensionLoader>(sp =>
        {
            var options = sp.GetRequiredService<ExtensionLoaderOptions>();
            var logger = sp.GetService<ILoggingService>();
            return new BuiltinExtensionLoader(options.BuiltinExtensionsDir, logger);
        });

        // Command resolver — uses all registered IExtensionLoader instances
        services.AddSingleton<CommandResolver>(sp =>
        {
            var loaders = sp.GetServices<IExtensionLoader>();
            var logger = sp.GetService<ILoggingService>();
            return new CommandResolver(loaders, logger);
        });

        return services;
    }
}
