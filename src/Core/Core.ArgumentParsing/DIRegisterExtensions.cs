using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Core.ArgumentParsing;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register argument parsing-related services.
    /// </summary>
    public static IServiceCollection RegisterArgumentParsingServices(this IServiceCollection services)
    {
        services.AddSingleton<PropertyParser>();
        services.AddSingleton<IArgumentParsingService, ArgumentParsingService>();
        return services;
    }
}