using Core.Engine;
using Core.Agent;
using Core.Implementation;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Create AgentEngine as fallback
        var telemetry = new ConsoleTelemetryService();
        var agentEngine = new AgentEngine(telemetry);
        
        // Start with DefaultCommandEngine and AgentEngine fallback
        var commandEngine = await Bootstrapper.StartAsync(agentEngine);
        await commandEngine.ExecuteCommandAsync(args, default);
    }
}
