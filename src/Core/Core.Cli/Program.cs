using Core.Abstractions;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;
using Core.Engine;

//
// ARCHITECTURAL EVOLUTION
// =======================
//
// ORIGINAL DESIGN (main branch):
// - Simple 3-line Program.cs: Start bootstrapper -> Get command engine -> Execute
// - Only DefaultCommandEngine existed (parsed structured commands like "group create --name test")
// - No AI integration, no natural language support
// - Failed commands just threw exceptions
//
// CURRENT DESIGN (this branch):
// - Same simple 3-line pattern restored via Bootstrapper
// - But now Bootstrapper returns OrchestratorEngine instead of just DefaultCommandEngine
// - OrchestratorEngine provides intelligent fallback:
//   1. Try DefaultCommandEngine first (structured commands work as before)
//   2. If that fails (typo, unknown command, natural language), invoke AgentEngine (AI)
//   3. AI can explain, suggest corrections, or generate executable commands
// - This means:
//   * "azv2 group create --name test" → DefaultEngine (fast, direct)
//   * "azv2 groop create --name test" → AgentEngine suggests correction
//   * "azv2 create a resource group called test" → AgentEngine generates command
//
// WHY THE CHANGE:
// The original design required users to know exact command syntax. The new design
// keeps that efficiency for power users (structured commands still fast-path through
// DefaultEngine) but adds graceful degradation for natural language, typos, and
// exploratory queries. This is accomplished by swapping DefaultCommandEngine for
// OrchestratorEngine in the Bootstrapper, keeping Program.cs simple.
//

internal class Program
{
    private static async Task Main(string[] args)
    {
        var serviceProvider = await Bootstrapper.ServiceProviderAsync;
        var commandEngine = serviceProvider.GetRequiredService<ICommandEngine>();
        await commandEngine.ExecuteCommandAsync(args, default);
    }
}
