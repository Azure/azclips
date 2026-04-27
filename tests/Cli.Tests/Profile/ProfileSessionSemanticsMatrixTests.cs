using Xunit;

namespace Cli.Tests.Profile;

public class ProfileSessionSemanticsMatrixTests
{
    public static TheoryData<string> SessionMatrix => new()
    {
        "Resolution order is override then active profile",
        "Override is non-persistent",
        "Set active changes only active pointer",
        "Silent auth attempted before auth-required classification",
        "Missing profile returns deterministic not-found behavior"
    };

    [Theory(Skip = "Matrix scaffold only. Implement runtime assertions with profile command pipeline wiring.")]
    [MemberData(nameof(SessionMatrix))]
    public void SessionSemantics_ShouldMatchSpec(string scenario)
    {
        Assert.False(string.IsNullOrWhiteSpace(scenario));
    }
}