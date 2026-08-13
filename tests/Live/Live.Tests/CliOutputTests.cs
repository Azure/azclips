namespace Live.Tests;

/// <summary>
/// Guards the records in which this suite states the CLI's output contract.
///
/// <para>
/// These do not touch Azure, so they run everywhere — locally and on the VM. Their
/// job is to catch a mistake in the contract itself: if <see cref="AuthMethod"/>'s
/// ordinals drifted, the live assertions would still pass while comparing the wrong
/// value, and the demo would show green for a broken login.
/// </para>
/// </summary>
public class CliOutputTests
{
    private static AzResult Output(string json) => new(0, json, string.Empty);

    [Fact(DisplayName = "Output: a managed-identity login is read back correctly")]
    public void Parse_ManagedIdentityProfile_ReadsEveryField()
    {
        var profile = CliJson.Parse<ProfileOutput>(
            Output(
                """
                {
                  "name": "temp",
                  "subscriptionId": "f758ac53-3e63-4317-a956-0997793808d7",
                  "authMethod": 4,
                  "managedIdentityId": "MSI@"
                }
                """),
            "profile show");

        Assert.Equal("temp", profile.Name);
        Assert.Equal("f758ac53-3e63-4317-a956-0997793808d7", profile.SubscriptionId);
        Assert.Equal(AuthMethod.ManagedIdentity, profile.AuthMethod);
        Assert.Equal("MSI@", profile.ManagedIdentityId);
    }

    /// <summary>
    /// The CLI writes the ordinal, so this pins the member order. Reordering the enum
    /// would otherwise silently repoint <c>ManagedIdentity</c> at another method.
    /// </summary>
    [Fact(DisplayName = "Output: the auth method ordinals match the CLI's")]
    public void Parse_AuthMethod_MatchesTheOrdinalsTheCliWrites()
    {
        Assert.Equal(0, (int)AuthMethod.Browser);
        Assert.Equal(1, (int)AuthMethod.Broker);
        Assert.Equal(2, (int)AuthMethod.DeviceCode);
        Assert.Equal(3, (int)AuthMethod.ServicePrincipal);
        Assert.Equal(4, (int)AuthMethod.ManagedIdentity);
    }

    [Fact(DisplayName = "Output: the auth method is read by name as well as by value")]
    public void Parse_AuthMethodAsName_IsAccepted()
    {
        var profile = CliJson.Parse<ProfileOutput>(
            Output("""{ "authMethod": "ManagedIdentity" }"""),
            "profile show");

        Assert.Equal(AuthMethod.ManagedIdentity, profile.AuthMethod);
    }

    [Fact(DisplayName = "Output: 'vm show' reports the VM name")]
    public void Parse_Vm_ReadsTheName()
    {
        var vm = CliJson.Parse<VmOutput>(
            Output("""{ "name": "live-test-vm", "location": "eastus" }"""),
            "vm show");

        Assert.Equal("live-test-vm", vm.Name);
    }

    /// <summary>
    /// A truncated or non-JSON response must fail loudly. Silently yielding an empty
    /// record would let a broken command read as a passing test.
    /// </summary>
    [Fact(DisplayName = "Output: malformed output is reported, not swallowed")]
    public void Parse_MalformedOutput_Fails()
    {
        var failure = Assert.ThrowsAny<Exception>(
            () => CliJson.Parse<ProfileOutput>(Output("not json"), "profile show"));

        Assert.Contains("profile show", failure.Message);
    }
}
