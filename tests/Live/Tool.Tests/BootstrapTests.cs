using Live.Tool;

namespace Live.Tool.Tests;

public class BootstrapTests
{
    [Theory]
    [InlineData("ERROR: Forbidden({\"error\":{\"code\":\"Authorization_RequestDenied\"...")]
    [InlineData("ERROR: Insufficient privileges to complete the operation.")]
    [InlineData("ERROR: (AuthorizationFailed) The client does not have authorization")]
    public void IsPermissionDenied_MatchesTheObviousDenials(string detail) =>
        Assert.True(Bootstrap.IsPermissionDenied(detail));

    [Fact]
    public void IsPermissionDenied_MatchesTheDisguisedServicePrincipalDenial()
    {
        // Entra's wording for "you may not manage this application". It names neither a
        // code nor a privilege, and the claim it does make is false — the application IS
        // local. Retrying it wastes minutes, so it has to be recognised.
        Assert.True(Bootstrap.IsPermissionDenied(
            "ERROR: When using this permission, the backing application of the service "
            + "principal being created must in the local tenant"));
    }

    [Theory]
    [InlineData("ERROR: Request_ResourceNotFound: Resource does not exist.")]
    [InlineData("ERROR: PrincipalNotFound: Principal does not exist in the directory.")]
    [InlineData("")]
    public void IsPermissionDenied_IgnoresReplicationDelays(string detail) =>
        Assert.False(Bootstrap.IsPermissionDenied(detail));
}
