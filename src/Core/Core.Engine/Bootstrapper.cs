using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

/// <summary>
/// Static bootstrapper for backward compatibility. Wraps <see cref="CirrusHostBuilder"/>
/// to provide the same Lazy&lt;Task&lt;IServiceProvider&gt;&gt; pattern used by existing
/// extension commands and PowerShell scripts.
///
/// New code should prefer <see cref="CirrusHostBuilder"/> directly.
/// </summary>
public class Bootstrapper
{
    public static Task<IServiceProvider> ServiceProviderAsync => _serviceProvider.Value;
    private static readonly Lazy<Task<IServiceProvider>> _serviceProvider = new(() => StartAsync());

    private static async Task<IServiceProvider> StartAsync()
    {
        var builder = new CirrusHostBuilder();
        builder.Services.AddCirrusCore();
        builder.Services.AddCirrusCli();
        return await builder.BuildAsync();
    }
}
