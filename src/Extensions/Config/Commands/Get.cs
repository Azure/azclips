using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Config.Commands;

[CommandInfo(Name = "Get", Description = "Get a configuration value for Cirrus CLI")]
public class GetConfigCommand : IProcessor
{
    [ArgumentInfo(
        Required = true,
        Description = @"Configuration name to get.",
        SerializedName = @"Name")]
    public string Name { get; set; } = null!;

    protected IRuntimeHttpPipeline HttpPipeline = null!;
    protected CommandOutputDelegate WriteOutput = null!;
    public StartActivityDelegate StartActivity { get; set; } = null!;

    public IProcessor Init(ICommandContext context)
    {
        HttpPipeline = context.HttpPipeline;
        WriteOutput = context.CommandOutputHandler.WriteOutput;
        StartActivity = context.CommandTelemetryHandler.StartActivity;

        return this;
    }

    public async Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(GetConfigCommand)}.{nameof(ProcessAsync)}");
        var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
        var config = serviceProvider?.GetRequiredService<IConfiguration>() ?? throw new InvalidOperationException("Configuration is not available.");
        var value = config[Name];
        if (value != null)
        {
            await WriteOutput(value);
        }
    }
}