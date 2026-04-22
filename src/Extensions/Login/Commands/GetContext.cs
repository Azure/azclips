using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class GetContextCommand : LoginCommandBase
    {
        public override async Task ProcessAsync() {
            await EventListener.Signal(Events.CommandProcessAsyncStart);
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            AuthContext Context =  authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()!;
            await WriteOutput(Context);
            await EventListener.Signal(Events.CommandProcessAsyncEnd);
        }
    }
}
