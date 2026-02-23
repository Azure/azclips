using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Config.Commands;

[CommandInfo(Name = "Set", Description = "Set a configuration value for Cirrus CLI")]
public class SetConfigCommand : IProcessor
{
    [ArgumentInfo(
        Required = true,
        Description = @"Configuration name to set.",
        SerializedName = @"Name")]
    public string Name { get; set; } = null!;
    [ArgumentInfo(
        Required = true,
        Description = @"Configuration value to set.",
        SerializedName = @"Value")]
    public string Value { get; set; } = null!;

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

    public Task ProcessAsync()
    {
        using var _ = StartActivity($"{nameof(SetConfigCommand)}.{nameof(ProcessAsync)}");
        var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
        var config = serviceProvider?.GetRequiredService<IConfigurationProvider>() ?? throw new InvalidOperationException("Configuration provider is not available.");
        config.Set(Name, Value);
        return Task.CompletedTask;
    }
}