using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.AssemblyLoading;
using Core.Cli.Parser;
using Core.DependencyInjection;
using Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.Processors;
using Moq;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// End-to-end CLI tests for login-profile integration and profile management commands, exercised
/// through the real Engine with an in-memory profile store and a stub auth service.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public sealed class ProfileCliTests
{
    private const string ValidSubscriptionId = "11111111-1111-1111-1111-111111111111";
    private const string SecondValidSubscriptionId = "22222222-2222-2222-2222-222222222222";

    // ── login ──

    [Theory]
    [InlineData("--identity")]
    [InlineData("-i")]
    public async Task Login_ManagedIdentityWithoutSubscription_ShouldFailWithoutWritingProfile(string identityFlag)
    {
        using var fixture = new ProfileCliFixture();

        var ex = Assert.Throws<InvalidInputException>(() => fixture.ExecuteCli(["login", identityFlag]));

        Assert.Contains("subscription-id", ex.Message, StringComparison.Ordinal);
        Assert.Empty(await fixture.Profiles.GetAllProfilesAsync());
        Assert.Empty(fixture.RawOutputs);
    }

    [Theory]
    [InlineData("browser")]
    [InlineData("device-code")]
    [InlineData("managed-identity")]
    [InlineData("client-secret")]
    [InlineData("federated")]
    public async Task Login_WithoutExplicitSubscription_ShouldNotUseActiveProfile(string flow)
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedActiveProfile("existing", ValidSubscriptionId, userPrincipalName: null);
        string[] authArguments = flow switch
        {
            "browser" => [],
            "device-code" => ["--use-device-code"],
            "managed-identity" => ["--identity"],
            "client-secret" => ["--client-id", ValidSubscriptionId, "--tenant-id", ValidSubscriptionId,
                "--client-secret", "test-secret"],
            "federated" => ["--service-principal", "--client-id", ValidSubscriptionId,
                "--tenant-id", ValidSubscriptionId, "--federated-token", "test-assertion"],
            _ => throw new ArgumentOutOfRangeException(nameof(flow)),
        };

        var ex = Assert.Throws<InvalidInputException>(() => fixture.ExecuteCli(["login", .. authArguments]));

        Assert.Contains("missing mandatory [--subscription-id]", ex.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.RawOutputs);
        Assert.Equal("existing", await fixture.Profiles.GetActiveProfileNameAsync());
        Assert.Single(await fixture.Profiles.GetAllProfilesAsync());
    }

    [Fact]
    public async Task Login_ManagedIdentity_ShouldPersistExplicitSubscription()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedActiveProfile("existing", SecondValidSubscriptionId, userPrincipalName: null);

        fixture.ExecuteCli(["login", "--identity", "--subscription-id", ValidSubscriptionId]);

        var profile = await fixture.Profiles.GetActiveProfileAsync();
        Assert.NotNull(profile);
        Assert.Equal(AuthMethod.ManagedIdentity, profile.AuthMethod);
        Assert.Equal(ValidSubscriptionId, profile.SubscriptionId);
        Assert.Equal("MSI@", profile.ManagedIdentityId);
        fixture.Subscriptions.Verify(s => s.GetSubscriptionByIdAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithOutProfile_ShouldCreateActivateAndReturnProfile()
    {
        using var fixture = new ProfileCliFixture();

        var output = fixture.ExecuteCli(["login", "--subscription-id", ValidSubscriptionId, "--out-profile", "prod"]);

        Assert.Equal("prod", await fixture.Profiles.GetActiveProfileNameAsync());
        var prod = await fixture.Profiles.GetProfileAsync("prod");
        Assert.NotNull(prod);
        Assert.Equal(ValidSubscriptionId, prod!.SubscriptionId);
        Assert.Equal(AuthMethod.Browser, prod.AuthMethod);
        Assert.Contains(output, line => line.Contains("prod"));

        var result = AssertProfileResult(fixture.RawOutputs, "prod");
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Login_WithDeviceCode_ShouldRecordDeviceCodeAuthMethod()
    {
        using var fixture = new ProfileCliFixture();

        fixture.ExecuteCli(["login", "--subscription-id", ValidSubscriptionId, "--use-device-code", "--out-profile", "ci"]);

        var ci = await fixture.Profiles.GetProfileAsync("ci");
        Assert.Equal(AuthMethod.DeviceCode, ci!.AuthMethod);
    }

    [Fact]
    public async Task Login_WithoutOutProfile_ShouldCreateDefaultProfile()
    {
        using var fixture = new ProfileCliFixture();

        fixture.ExecuteCli(["login", "--subscription-id", ValidSubscriptionId]);

        Assert.Equal("default", await fixture.Profiles.GetActiveProfileNameAsync());
        Assert.NotNull(await fixture.Profiles.GetProfileAsync("default"));
    }

    [Fact]
    public async Task Login_CrossTenantSubscription_ShouldPersistOwningTenant()
    {
        using var fixture = new ProfileCliFixture();
        const string owningTenant = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        // The signed-in (home) tenant does not own the subscription; ARM reveals the real tenant.
        fixture.Subscriptions
            .Setup(s => s.ResolveSubscriptionTenantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(owningTenant);

        fixture.ExecuteCli(["login", "--subscription-id", ValidSubscriptionId, "--out-profile", "prod"]);

        var prod = await fixture.Profiles.GetProfileAsync("prod");
        Assert.NotNull(prod);
        Assert.Equal(owningTenant, prod!.TenantId);
    }

    [Fact]
    public async Task Login_WhenOwningTenantLookupFails_ShouldFail_WithoutWritingProfile()
    {
        using var fixture = new ProfileCliFixture();
        // The owning-tenant lookup fails (e.g., transient ARM error). Login must not persist a guessed
        // (home) tenant that may be wrong for the subscription.
        fixture.Subscriptions
            .Setup(s => s.ResolveSubscriptionTenantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));

        Assert.ThrowsAny<Exception>(() =>
            fixture.ExecuteCli(["login", "--subscription-id", ValidSubscriptionId, "--out-profile", "prod"]));

        Assert.Null(await fixture.Profiles.GetProfileAsync("prod"));
        Assert.Null(await fixture.Profiles.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task Login_WithOutProfileDefault_ShouldFail_AsReserved()
    {
        using var fixture = new ProfileCliFixture();

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["login", "--subscription-id", ValidSubscriptionId, "--out-profile", "default"]));
        Assert.Null(await fixture.Profiles.GetProfileAsync("default"));
    }

    [Fact]
    public async Task Login_WithProfileAndOutProfile_ShouldFailFast_BeforeCreatingTarget()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        Assert.ThrowsAny<Exception>(() =>
            fixture.ExecuteCli(["login", "--profile", "prod", "--out-profile", "staging"]));

        Assert.Null(await fixture.Profiles.GetProfileAsync("staging"));
    }

    [Fact]
    public async Task Login_WithProfile_ShouldReauthenticateExistingProfile()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        fixture.ExecuteCli(["login", "--profile", "prod"]);

        var prod = await fixture.Profiles.GetProfileAsync("prod");
        Assert.NotNull(prod);
        Assert.Equal(ValidSubscriptionId, prod!.SubscriptionId); // sourced from the existing profile
        Assert.Equal("prod", await fixture.Profiles.GetActiveProfileNameAsync());
    }

    [Theory]
    [InlineData("--subscription-id")]
    [InlineData("--subscription")]
    [InlineData("-s")]
    public async Task Login_WithProfileAndSubscription_ShouldRejectUnusedSubscription(string subscriptionFlag)
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        var ex = Assert.Throws<InvalidInputException>(() =>
            fixture.ExecuteCli(["login", "--profile", "prod", subscriptionFlag, SecondValidSubscriptionId]));

        Assert.Contains("does not allow [--subscription-id]", ex.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.RawOutputs);
        Assert.Null(await fixture.Profiles.GetActiveProfileNameAsync());
        Assert.Equal(ValidSubscriptionId, (await fixture.Profiles.GetProfileAsync("prod"))!.SubscriptionId);
    }

    [Theory]
    [InlineData("row-profile", "eu-profile")]
    [InlineData("eu-profile", "row-profile")]
    public void RepeatedProfile_ParserAndSessionUseLastOccurrence(string firstProfile, string lastProfile)
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile(firstProfile, ValidSubscriptionId);
        fixture.SeedProfile(lastProfile, SecondValidSubscriptionId);
        string[] args = ["login", "--profile", firstProfile, "-Profile", lastProfile];
        Profile? contextProfile = null;

        fixture.ExecuteCli(args, profile => contextProfile = profile);

        Assert.NotNull(contextProfile);
        Assert.Equal(lastProfile, contextProfile.Name);
        Assert.Equal(SecondValidSubscriptionId, contextProfile.SubscriptionId);
        Assert.Equal(Core.Cli.Program.TryGetProfileOverride(args), contextProfile.Name);
        var result = AssertProfileResult(fixture.RawOutputs, lastProfile);
        Assert.Equal(contextProfile.SubscriptionId, result.SubscriptionId);
    }

    [Fact]
    public void Login_WithProfileAndInvalidExplicitSubscriptionId_ShouldFailFast()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        var ex = Assert.ThrowsAny<Exception>(() =>
            fixture.ExecuteCli(["login", "--profile", "prod", "--subscription-id", "Steven Ma [stema] subscription"]));

        Assert.Contains("Invalid subscription id", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--subscription-id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_WithProfile_ShouldPreserveManagedIdentityAuthMethodAndSelector()
    {
        using var fixture = new ProfileCliFixture();
        const string miKey = "MSIClient@11111111-1111-1111-1111-111111111111";
        fixture.SeedManagedIdentityProfile("mi-prod", ValidSubscriptionId, miKey);

        fixture.ExecuteCli(["login", "--profile", "mi-prod"]);

        var prod = await fixture.Profiles.GetProfileAsync("mi-prod");
        Assert.NotNull(prod);
        // Re-auth reuses the stored auth method instead of falling back to interactive/browser.
        Assert.Equal(AuthMethod.ManagedIdentity, prod!.AuthMethod);
        // The managed-identity selector is preserved so token refresh targets the same identity.
        Assert.Equal(miKey, prod.ManagedIdentityId);
    }

    [Fact]
    public void Login_WithProfileAndAuthFlag_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        Assert.ThrowsAny<Exception>(() =>
            fixture.ExecuteCli(["login", "--profile", "prod", "--identity"]));
    }

    [Fact]
    public void Login_WithNonExistentProfile_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["login", "--profile", "nope"]));
    }

    // ── profile list / show ──

    [Fact]
    public void ProfileList_ShouldReturnEmpty_WhenNoProfiles()
    {
        using var fixture = new ProfileCliFixture();

        fixture.ExecuteCli(["profile", "list"]);

        // The list command emits a single collection (so the JSON formatter renders one valid
        // JSON array) even when there are no profiles.
        var emitted = Assert.Single(fixture.RawOutputs);
        var results = Assert.IsAssignableFrom<IEnumerable<ProfileResult>>(emitted);
        Assert.Empty(results);
    }

    [Fact]
    public async Task ProfileList_ShouldIndicateActiveProfile()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        fixture.SeedProfile("staging", SecondValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        fixture.ExecuteCli(["profile", "list"]);

        // All profiles are written as a single collection rather than one WriteOutput per profile.
        var emitted = Assert.Single(fixture.RawOutputs);
        var results = Assert.IsAssignableFrom<IEnumerable<ProfileResult>>(emitted).ToList();
        Assert.Equal(2, results.Count);
        Assert.Contains(results, s => s.Name == "prod" && s.IsActive);
        Assert.Contains(results, s => s.Name == "staging" && !s.IsActive);
        Assert.All(results, result => Assert.Equal(AuthMethod.Browser, result.AuthMethod));
        Assert.All(results, result => Assert.Null(result.GetType().GetProperty("Metadata")));
    }

    [Fact]
    public void ProfileShow_ShouldReturnProfile()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        var output = fixture.ExecuteCli(["profile", "show", "prod"]);

        Assert.Contains(output, line => line.Contains("prod"));
        var result = AssertProfileResult(fixture.RawOutputs, "prod");
        Assert.False(result.IsActive);
    }

    [Fact]
    public void ProfileShow_NonExistent_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();

        var ex = Assert.ThrowsAny<AzclipsErrorException>(() => fixture.ExecuteCli(["profile", "show", "nope"]));
        Assert.Equal("ProfileNotFound", ex.Code);
    }

    [Fact]
    public async Task ProfileShow_Active_ShouldReturnActiveProfile()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        fixture.SeedProfile("staging", SecondValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        var output = fixture.ExecuteCli(["profile", "show", "--active"]);

        Assert.Contains(output, line => line.Contains("prod"));
        Assert.DoesNotContain(output, line => line.Contains("staging"));
        var result = AssertProfileResult(fixture.RawOutputs, "prod");
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task ProfileShow_NoName_ShouldReturnActiveProfile()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        fixture.SeedProfile("staging", SecondValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        var output = fixture.ExecuteCli(["profile", "show"]);

        Assert.Contains(output, line => line.Contains("prod"));
        Assert.DoesNotContain(output, line => line.Contains("staging"));
        var result = AssertProfileResult(fixture.RawOutputs, "prod");
        Assert.True(result.IsActive);
    }

    [Fact]
    public void ProfileShow_Active_NoActiveProfile_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["profile", "show", "--active"]));
    }

    [Fact]
    public async Task ProfileShow_NameAndActiveTogether_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["profile", "show", "prod", "--active"]));
    }

    [Fact]
    public void ProfileShow_NoNameNoActive_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["profile", "show"]));
    }

    // ── profile set ──

    [Fact]
    public async Task ProfileSet_ShouldSetActiveProfile()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        fixture.SeedProfile("staging", SecondValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        fixture.ExecuteCli(["profile", "set", "staging"]);

        Assert.Equal("staging", await fixture.Profiles.GetActiveProfileNameAsync());
        var result = AssertProfileResult(fixture.RawOutputs, "staging");
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task ProfileSet_NonExistent_ShouldFail_AndLeaveActiveUnchanged()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["profile", "set", "nope"]));
        Assert.Equal("prod", await fixture.Profiles.GetActiveProfileNameAsync());
    }

    // ── profile remove ──

    [Fact]
    public async Task ProfileRemove_NonActive_ShouldDelete_AndLeaveActiveUnchanged()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        fixture.SeedProfile("staging", SecondValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        fixture.ExecuteCli(["profile", "remove", "staging"]);

        Assert.Null(await fixture.Profiles.GetProfileAsync("staging"));
        Assert.Equal("prod", await fixture.Profiles.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task ProfileRemove_Active_ShouldClearActivePointer()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId);
        await fixture.Profiles.SetActiveProfileNameAsync("prod");

        fixture.ExecuteCli(["profile", "remove", "prod"]);

        Assert.Null(await fixture.Profiles.GetProfileAsync("prod"));
        Assert.Null(await fixture.Profiles.GetActiveProfileNameAsync());
    }

    [Fact]
    public void ProfileRemove_NonExistent_ShouldFail()
    {
        using var fixture = new ProfileCliFixture();

        Assert.ThrowsAny<Exception>(() => fixture.ExecuteCli(["profile", "remove", "nope"]));
    }

    // ── logout ──

    [Fact]
    public async Task Logout_WithActiveProfile_ShouldRemoveAccountAndClearActivePointer()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedActiveProfile("prod", ValidSubscriptionId, userPrincipalName: "alice@contoso.com");

        fixture.ExecuteCli(["logout"]);

        Assert.True(fixture.Auth.LogoutCalled);
        Assert.Equal("prod", fixture.Auth.LastLoggedOutProfileName);
        Assert.Null(await fixture.Profiles.GetActiveProfileNameAsync());
        // The profile definition itself is retained; only the active pointer is cleared.
        Assert.NotNull(await fixture.Profiles.GetProfileAsync("prod"));
    }

    [Fact]
    public async Task Logout_WithActiveProfileMissingUpn_ShouldStillClearActivePointer()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedActiveProfile("prod", ValidSubscriptionId, userPrincipalName: null);

        fixture.ExecuteCli(["logout"]);

        Assert.True(fixture.Auth.LogoutCalled);
        Assert.Equal("prod", fixture.Auth.LastLoggedOutProfileName);
        Assert.Null(await fixture.Profiles.GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task Logout_WithNoActiveProfile_ShouldBeNoOp()
    {
        using var fixture = new ProfileCliFixture();
        fixture.SeedProfile("prod", ValidSubscriptionId); // exists but not active

        fixture.ExecuteCli(["logout"]);

        Assert.False(fixture.Auth.LogoutCalled);
        Assert.Null(await fixture.Profiles.GetActiveProfileNameAsync());
    }

    private static ProfileResult AssertProfileResult(IReadOnlyList<object?> rawOutputs, string expectedName)
    {
        var result = Assert.IsType<ProfileResult>(Assert.Single(rawOutputs));
        Assert.Equal(expectedName, result.Name);
        Assert.Null(result.GetType().GetProperty("Metadata"));
        return result;
    }

    private sealed class ProfileCliFixture : IDisposable
    {
        private readonly List<string> _outputs = [];
        private readonly List<object?> _rawOutputs = [];

        public StubProfileService Profiles { get; } = new(seedDefault: false);

        public StubAuthService Auth { get; } = new();

        public Mock<ISubscriptionService> Subscriptions { get; } = new();

        /// <summary>Raw values passed to <c>WriteOutput</c> by the most recent <see cref="ExecuteCli"/> call.</summary>
        public IReadOnlyList<object?> RawOutputs => _rawOutputs;

        public ProfileCliFixture()
        {
            Subscriptions
                .Setup(s => s.ResolveSubscriptionTenantAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("00000000-0000-0000-0000-000000000000");
            Subscriptions
                .Setup(s => s.GetSubscriptionByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string _, string id, CancellationToken _) => new SubscriptionInfo(id, "Test", "Enabled"));
            AzclipsServiceProvider.Dispose();
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider>(new StubExtensionLoader(typeof(LoginProcessor).Assembly, "Accounts"));
                services.AddSingleton<ITelemetryService, StubTelemetryService>();
                services.AddSingleton<IProfileService>(Profiles);
                services.AddSingleton<IAuthService>(Auth);
                services.AddSingleton<ISubscriptionService>(Subscriptions.Object);
                services.AddSingleton<Core.Cli.PropertyParser>();
                services.AddSingleton<IArgumentParsingProvider, Core.Cli.CLIArgumentParsingProvider>();
            });
            AzclipsServiceProvider.Initialize(
                new ClientMetadata(ClientType.CLI, "00000000-0000-0000-0000-000000000000"));
        }

        public void SeedProfile(string name, string subscriptionId) =>
            Profiles.SaveProfileAsync(new Profile
            {
                Name = name,
                TenantId = "00000000-0000-0000-0000-000000000000",
                SubscriptionId = subscriptionId,
                CloudName = "AzureCloud",
                AuthMethod = AuthMethod.Browser,
            }).GetAwaiter().GetResult();

        public void SeedManagedIdentityProfile(string name, string subscriptionId, string managedIdentityId) =>
            Profiles.SaveProfileAsync(new Profile
            {
                Name = name,
                TenantId = "00000000-0000-0000-0000-000000000000",
                SubscriptionId = subscriptionId,
                CloudName = "AzureCloud",
                AuthMethod = AuthMethod.ManagedIdentity,
                ManagedIdentityId = managedIdentityId,
            }).GetAwaiter().GetResult();

        public void SeedActiveProfile(string name, string subscriptionId, string? userPrincipalName) =>
            Profiles.SaveAndActivateProfileAsync(new Profile
            {
                Name = name,
                TenantId = "tenant-1",
                SubscriptionId = subscriptionId,
                CloudName = "AzureCloud",
                AuthMethod = AuthMethod.Browser,
                UserPrincipalName = userPrincipalName,
            }).GetAwaiter().GetResult();

        public IReadOnlyList<string> ExecuteCli(string[] args, Action<Profile?>? onProfileResolved = null)
        {
            _outputs.Clear();
            _rawOutputs.Clear();

            var dispatcher = BuildDispatcher();
            var invocation = dispatcher.Parse(args);
            var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();

            var streams = new OutputStreams(
                WriteOutput: (value, _) =>
                {
                    _rawOutputs.Add(value);
                    _outputs.Add(Convert.ToString(value) ?? string.Empty);
                    return Task.CompletedTask;
                },
                WriteVerbose: (_, _) => Task.CompletedTask,
                WriteDebug: (_, _) => Task.CompletedTask,
                WriteWarning: (_, _) => Task.CompletedTask,
                WriteError: (_, _) => Task.CompletedTask,
                WriteInformation: (_, _) => Task.CompletedTask);

            using ISession session = engine.OpenExecutionSession(invocation, streams, CancellationToken.None);
            if (onProfileResolved is not null)
            {
                session.EventBus.Subscribe(Events.ProcessorContextCreated,
                    data => onProfileResolved(((EventData)data).Payload as Profile));
            }
            session.Run();

            return _outputs.ToArray();
        }

        public void Dispose() => AzclipsServiceProvider.Dispose();

        private static ExtensionDispatcher BuildDispatcher()
        {
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Accounts"] = new ExtensionRegistryEntry
                    {
                        Version = "0.0.0",
                        Contributions =
                        {
                            new Contribution { Verb = "login", Path = "" },
                            new Contribution { Verb = "logout", Path = "" },
                            new Contribution { Verb = "profile", Path = "" },
                            new Contribution { Verb = "profile", Path = "list" },
                            new Contribution { Verb = "profile", Path = "show" },
                            new Contribution { Verb = "profile", Path = "set" },
                            new Contribution { Verb = "profile", Path = "remove" },
                        },
                    },
                },
            };
            var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
            return new ExtensionDispatcher(resolver, () => LeafIndex.Build(registry));
        }

    }
}
