using Xunit;

namespace Cli.Tests.Profile;

public class ProfileCommandContractMatrixTests
{
    public static TheoryData<string, string> CommandMatrix => new()
    {
        { "Create/Update via login", "azv2 login --subscription <id> --tenant <id> --profile <name>" },
        { "List profiles", "azv2 profile list" },
        { "Show active profile", "azv2 profile show" },
        { "Set active profile", "azv2 profile set <name>" },
        { "Delete profile", "azv2 profile delete <name> [--force]" },
        { "Rename profile", "azv2 profile rename <old> <new>" },
        { "One-time override", "azv2 <command> --profile <name>" }
    };

    [Theory(Skip = "Matrix scaffold only. Implement command assertions as profile commands are added.")]
    [MemberData(nameof(CommandMatrix))]
    public void CommandContract_ShouldMatchSpec(string scenario, string commandExample)
    {
        Assert.False(string.IsNullOrWhiteSpace(scenario));
        Assert.False(string.IsNullOrWhiteSpace(commandExample));
    }
}