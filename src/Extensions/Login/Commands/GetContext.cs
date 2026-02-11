using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    [CommandInfo(Name = "show", Description = "Show current login context.")]
    public class GetContextCommand : LoginCommandBase
    {
        public override async Task ProcessAsync() {
            using var _ = StartActivity($"{nameof(GetContextCommand)}.{nameof(ProcessAsync)}");
            var authContextService = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult()
                .GetRequiredService<IAuthContextService>();
            AuthContext Context =  authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()!;
            await WriteOutput(Context);
        }
    }
}
