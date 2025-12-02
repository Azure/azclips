using Core.Abstractions;
using Core.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Engine;

public class Bootstrapper
{
    public static Task<ICommandEngine> StartAsync(CancellationToken cancellationToken = default)
    {
        return StartAsync(fallbackEngine: null, cancellationToken);
    }
    
    public static Task<ICommandEngine> StartAsync(ICommandEngine? fallbackEngine, CancellationToken cancellationToken = default)
    {
        var serviceProvider = RegisterServices(fallbackEngine);
        var telemetryService = serviceProvider.GetRequiredService<ITelemetryService>();
        telemetryService.Log("Core.Engine has started.");
        return Task.FromResult(serviceProvider.GetRequiredService<ICommandEngine>());
    }

    private static ServiceProvider RegisterServices(ICommandEngine? fallbackEngine)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ITelemetryService, ConsoleTelemetryService>();
        
        // Register DefaultCommandEngine
        services.AddSingleton<DefaultCommandEngine>();
        
        // Register composite engine with fallback
        services.AddSingleton<ICommandEngine>(sp =>
        {
            var telemetry = sp.GetRequiredService<ITelemetryService>();
            var defaultEngine = sp.GetRequiredService<DefaultCommandEngine>();
            
            // Create AgentEngine as fallback if not provided
            var actualFallback = fallbackEngine ?? new Core.Agent.AgentEngine(telemetry);
            
            return new CompositeCommandEngine(telemetry, defaultEngine, actualFallback);
        });

        return services.BuildServiceProvider();
    }
    
    /// <summary>
    /// Composite engine that tries DefaultCommandEngine first, then falls back to another engine.
    /// If the fallback engine is AgentEngine and it extracts a command, hands it back to DefaultCommandEngine.
    /// </summary>
    private class CompositeCommandEngine : ICommandEngine
    {
        private readonly ITelemetryService _telemetry;
        private readonly DefaultCommandEngine _defaultEngine;
        private readonly ICommandEngine _fallbackEngine;

        public CompositeCommandEngine(
            ITelemetryService telemetry,
            DefaultCommandEngine defaultEngine,
            ICommandEngine fallbackEngine)
        {
            _telemetry = telemetry;
            _defaultEngine = defaultEngine;
            _fallbackEngine = fallbackEngine;
        }

        public async Task ExecuteCommandAsync(string[] args, CancellationToken cancellationToken)
        {
            try
            {
                // Try DefaultCommandEngine first
                await _defaultEngine.ExecuteCommandAsync(args, cancellationToken);
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is FileNotFoundException ||
                ex is DirectoryNotFoundException)
            {
                // If parsing/binding fails, hand off to fallback engine
                _telemetry.Log($"DefaultCommandEngine failed: {ex.Message}. Handing off to fallback engine...");
                await _fallbackEngine.ExecuteCommandAsync(args, cancellationToken);
                
                // Check if fallback engine extracted a command that should be executed
                if (!string.IsNullOrEmpty(_fallbackEngine.ExtractedCommand))
                {
                    _telemetry.Log($"Fallback engine extracted command: {_fallbackEngine.ExtractedCommand}");
                    _telemetry.Log("Passing command back to DefaultCommandEngine for execution...");
                    
                    // Parse the extracted command and pass it back to DefaultCommandEngine
                    string[] commandArgs = ParseCommandString(_fallbackEngine.ExtractedCommand);
                    
                    // If the command starts with 'az' or 'azd', strip it since we ARE the az CLI
                    if (commandArgs.Length > 0 && 
                        (commandArgs[0].Equals("az", StringComparison.OrdinalIgnoreCase) ||
                         commandArgs[0].Equals("azd", StringComparison.OrdinalIgnoreCase)))
                    {
                        commandArgs = commandArgs.Skip(1).ToArray();
                        _telemetry.Log($"Stripped CLI prefix, new command: {string.Join(' ', commandArgs)}");
                    }
                    
                    try
                    {
                        await _defaultEngine.ExecuteCommandAsync(commandArgs, cancellationToken);
                    }
                    catch (Exception cmdEx)
                    {
                        _telemetry.Log($"Error executing extracted command: {cmdEx.Message}");
                        throw;
                    }
                }
            }
        }
        
        /// <summary>
        /// Parses a command string into an array of arguments.
        /// Handles quotes for arguments containing spaces.
        /// </summary>
        private static string[] ParseCommandString(string commandString)
        {
            var args = new List<string>();
            var currentArg = new System.Text.StringBuilder();
            bool inQuotes = false;
            
            for (int i = 0; i < commandString.Length; i++)
            {
                char c = commandString[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (currentArg.Length > 0)
                    {
                        args.Add(currentArg.ToString());
                        currentArg.Clear();
                    }
                }
                else
                {
                    currentArg.Append(c);
                }
            }
            
            if (currentArg.Length > 0)
            {
                args.Add(currentArg.ToString());
            }
            
            return args.ToArray();
        }
    }
}
