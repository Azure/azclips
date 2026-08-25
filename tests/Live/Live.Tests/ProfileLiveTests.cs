namespace Live.Tests;

/// <summary>
/// Live test for the <c>profile</c> verbs against a real managed-identity login.
///
/// <para>
/// The managed-identity tests prove a token can be acquired and used. This one
/// proves the login is <b>persisted correctly</b>: that the profile records the
/// identity actually used. A bug here would not break the first call after login —
/// it would surface later, when the CLI reloads the profile and re-authenticates as
/// the wrong identity.
/// </para>
///
/// <para>
/// This is the only case kept in the live run, because it is the only one that
/// exercises the whole chain end to end: the self-contained bundle starts on a
/// machine with no .NET, IMDS issues a real token, the profile is written, and it
/// is read back. The cases covering list/set/remove and the user-assigned selectors
/// were removed rather than left skipped — a run that is watched should not be
/// mostly skips — and their verbs are already covered against a mock IMDS in
/// <c>Cli.Tests</c>. They are in git history if the live coverage is wanted back.
/// </para>
/// </summary>
public class ProfileLiveTests : IClassFixture<LiveTestFixture>, IAsyncLifetime
{
    /// <summary>
    /// Clears any cached credential first, so the login under test is what the
    /// profile ends up recording.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        if (!LiveTestEnvironment.IsEnabled)
        {
            return; // a skipped case still constructs the class
        }

        await AzCli.LogoutAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (LiveTestEnvironment.IsEnabled)
        {
            await AzCli.LogoutAsync();
        }
    }

    [LiveFact(DisplayName = "Managed identity: login is recorded in the profile")]
    public async Task ProfileShow_AfterSystemAssignedLogin_RecordsManagedIdentity()
    {
        var login = await AzCli.LoginWithIdentityAsync();
        Assert.True(login.Succeeded, $"login failed.{Environment.NewLine}{login.Describe()}");

        var show = await AzCli.RunAsync("profile", "show", "--active");
        Assert.True(show.Succeeded, $"'profile show --active' failed.{Environment.NewLine}{show.Describe()}");

        var profile = CliJson.Parse<ProfileOutput>(show, "profile show --active");

        Assert.Equal(AuthMethod.ManagedIdentity, profile.AuthMethod);
        Assert.Equal(LiveTestEnvironment.SubscriptionId, profile.SubscriptionId);

        // "MSI@" is the encoding for the system-assigned identity; a user-assigned
        // login would record MSIClient@/MSIObject@/MSIResource@ instead.
        Assert.Equal("MSI@", profile.ManagedIdentityId);
    }
}
