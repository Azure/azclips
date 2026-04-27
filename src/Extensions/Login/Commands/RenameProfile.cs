using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class RenameProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Existing profile name.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = true,
        Description = @"New profile name.",
        SerializedName = @"NewName")]
        public string NewName { get; set; } = "";

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(RenameProfileCommand)}.{nameof(ProcessAsync)}");
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            var renamed = authContextService.RenameProfileAsync(Name, NewName).GetAwaiter().GetResult();
            await WriteOutput(renamed);
        }
    }
}
