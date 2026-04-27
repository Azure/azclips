using Core.Abstractions;
using Core.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Login.Commands
{
    public class DeleteProfileCommand : LoginCommandBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Profile name to delete.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Force delete even if the profile is active.",
        SerializedName = @"Force")]
        public bool Force { get; set; } = false;

        public override async Task ProcessAsync()
        {
            using var _ = StartActivity($"{nameof(DeleteProfileCommand)}.{nameof(ProcessAsync)}");
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();
            authContextService.DeleteProfileAsync(Name, Force).GetAwaiter().GetResult();
            await WriteOutput(new { Deleted = Name, Force });
        }
    }
}
