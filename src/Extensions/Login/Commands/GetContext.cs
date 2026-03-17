using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class GetContextCommand : LoginCommandBase
    {
        public override async Task ProcessAsync() {
            using var _ = StartActivity($"{nameof(GetContextCommand)}.{nameof(ProcessAsync)}");
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            AuthContext Context =  authContextService.GetCurrentContextAsync().GetAwaiter().GetResult()!;
            await WriteOutput(Context);
        }
    }
}
