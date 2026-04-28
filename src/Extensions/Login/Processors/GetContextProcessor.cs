using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Processors
{
    public class GetContextProcessor : LoginProcessorBase
    {
        public override async Task ProcessAsync() {
            await EventListener.Signal(Events.ProcessorProcessAsyncStart);
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            AuthContext Context =  authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()!;
            await WriteOutput(Context);
            await EventListener.Signal(Events.ProcessorProcessAsyncEnd);
        }
    }
}
