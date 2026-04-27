using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class SetActiveProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Profile name to set active.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(SetActiveProfileCommand)}.{nameof(ProcessAsync)}");
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            authContextService.SetActiveProfileAsync(Name).GetAwaiter().GetResult();
            var active = authContextService.GetActiveProfileAsync().GetAwaiter().GetResult();
            await WriteOutput(active!);
        }
    }
}