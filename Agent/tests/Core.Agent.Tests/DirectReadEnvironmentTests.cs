// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using Core.Abstractions;
using Core.Agent.Common;
using Core.Agent.Hosting;
using Core.Agent.Tools;
using Core.Agent.Tools.Env;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Resource.Sku;
using Core.Runtime;
using GitHub.Copilot;
using Tests.Common.Stubs;
using Xunit;
using CliHelp = Core.Cli.Help;

namespace Core.Agent.Tests;

public sealed class DirectReadEnvironmentTests
{
    private const string SubscriptionOne = "11111111-1111-1111-1111-111111111111";
    private const string SubscriptionTwo = "22222222-2222-2222-2222-222222222222";
    private const string TenantOne = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    private const string TenantTwo = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";

    [Fact]
    public async Task SnapshotUsesLiveProfileService_SignedOutSwitchProfileAndSignOut()
    {
        using var fixture = new HelpCatalogFixture();
        var profilesDirectory = Path.Combine(fixture.Root, "profiles");
        var profiles = new ProfileService(new NullLoggingService(), profilesDirectory);
        var otherProcess = new ProfileService(new NullLoggingService(), profilesDirectory);

        using (var signedOut = JsonDocument.Parse(await EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable)))
        {
            Assert.False(signedOut.RootElement.GetProperty("signedIn").GetBoolean());
            Assert.Equal(JsonValueKind.Null, signedOut.RootElement.GetProperty("subscriptionId").ValueKind);
            Assert.Equal(JsonValueKind.Null, signedOut.RootElement.GetProperty("tenantId").ValueKind);
            Assert.Equal("cli", signedOut.RootElement.GetProperty("flavor").GetString());
            Assert.Equal(typeof(CliHelp.HelpArtifactWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                signedOut.RootElement.GetProperty("azclipsVersion").GetString());
            Assert.Equal(new[] { "azclipsVersion", "browserAvailable", "cwd", "flavor", "os", "shell", "signedIn",
                    "sshSession", "stdinInteractive", "subscriptionId", "tenantId" },
                signedOut.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        }

        await otherProcess.SaveAndActivateProfileAsync(Profile("first", SubscriptionOne, TenantOne));
        var first = await EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable);
        Assert.Contains($"subscription-id: {SubscriptionOne}", EnvSnapshotFormatter.Format(first));
        Assert.False(EnvSnapshotFormatter.IsSignedOut(first));
        await otherProcess.SaveAndActivateProfileAsync(Profile("second", SubscriptionTwo, TenantTwo));
        using (var changed = JsonDocument.Parse(await EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable)))
        {
            Assert.True(changed.RootElement.GetProperty("signedIn").GetBoolean());
            Assert.Equal(SubscriptionTwo, changed.RootElement.GetProperty("subscriptionId").GetString());
            Assert.Equal(TenantTwo, changed.RootElement.GetProperty("tenantId").GetString());
        }

        await otherProcess.SetActiveProfileNameAsync(null);
        Assert.True(EnvSnapshotFormatter.IsSignedOut(await EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable)));
    }

    [Fact]
    public async Task CorruptProfileIsAnError_NotSignedOut_AndDoesNotExposeUserData()
    {
        using var fixture = new HelpCatalogFixture();
        var profiles = new ProfileService(new NullLoggingService(), fixture.Root);
        await profiles.SaveAndActivateProfileAsync(Profile("test", SubscriptionOne, TenantOne));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "profiles.json"), "PRIVATE BROKEN PROFILE");
        var json = await EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable);
        HelpCatalogProviderTests.AssertError(json, "environment_snapshot_failed");
        Assert.DoesNotContain("PRIVATE", json);
        Assert.False(EnvSnapshotFormatter.IsSignedOut(json));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpectedProfileReadFailures_RemainExplicitWithoutInventingContext(bool accessDenied)
    {
        using var fixture = new HelpCatalogFixture();
        var profiles = new FailingProfileService(accessDenied
            ? new UnauthorizedAccessException("PRIVATE profile failure")
            : new IOException("PRIVATE profile failure"));
        var json = await EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable);
        HelpCatalogProviderTests.AssertError(json, "environment_snapshot_failed");
        Assert.DoesNotContain("PRIVATE", json);
        Assert.False(EnvSnapshotFormatter.IsSignedOut(json));

        var config = CreateSessionConfig(fixture.Executable, profiles, new HelpTools(fixture.Provider));
        var start = await config.Hooks!.OnSessionStart!(null!, null!);
        Assert.Contains("NOT the classic Azure CLI", start!.AdditionalContext);
        Assert.DoesNotContain("<environment>", start.AdditionalContext);
        var prompt = await config.Hooks.OnUserPromptSubmitted!(null!, null!);
        Assert.Equal(string.Empty, prompt!.AdditionalContext);
        Assert.Equal(0, fixture.GenerationCount);
    }

    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("null-reference")]
    [InlineData("wrapped-defect")]
    public async Task UnexpectedProfileFaults_PropagateFromDirectReadsAndHooks(string kind)
    {
        using var fixture = new HelpCatalogFixture();
        Exception failure = kind switch
        {
            "invalid-operation" => new InvalidOperationException("unexpected defect"),
            "null-reference" => new NullReferenceException("unexpected defect"),
            _ => new Exception("profile wrapper", new NullReferenceException("unexpected defect")),
        };
        var profiles = new FailingProfileService(failure);
        var config = CreateSessionConfig(fixture.Executable, profiles, new HelpTools(fixture.Provider));

        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(() =>
            EnvSnapshotProvider.GetJsonAsync(profiles, fixture.Executable)));
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(async () =>
            await config.Hooks!.OnSessionStart!(null!, null!)));
        Assert.Same(failure, await Assert.ThrowsAnyAsync<Exception>(async () =>
            await config.Hooks!.OnUserPromptSubmitted!(null!, null!)));
        Assert.Equal(0, fixture.GenerationCount);
    }

    [Fact]
    public async Task ProfileCancellationPropagates_DuringAndBeforeRead()
    {
        using var fixture = new HelpCatalogFixture();
        using var cts = new CancellationTokenSource();
        var profile = new WaitingProfileService();
        var read = EnvSnapshotProvider.GetJsonAsync(profile, fixture.Executable, cts.Token);
        await profile.Started.Task;
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            EnvSnapshotProvider.GetJsonAsync(profile, fixture.Executable, cts.Token));
    }

    [Fact]
    public async Task ProductionHooksKeepBoundaryFailedHelpAndProfileRefresh_WithoutSubprocesses()
    {
        using var fixture = new HelpCatalogFixture();
        fixture.WriteCatalog(manifests:
        [
            new("login", "1.0.0", "login", [new("login", "", "sign in", "sign in", [], new([], []), [])]),
        ]);
        var profiles = new ProfileService(new NullLoggingService(), Path.Combine(fixture.Root, "profiles"));
        var tools = new HelpTools(fixture.Provider);
        using var cts = new CancellationTokenSource();
        var config = CreateSessionConfig(fixture.Executable, profiles, tools, "failed help context", cts.Token);
        var initial = await config.Hooks!.OnSessionStart!(null!, null!);
        Assert.Contains("NOT the classic Azure CLI", initial!.AdditionalContext);
        Assert.Contains("failed help context", initial.AdditionalContext);
        Assert.Contains("`login`", initial.AdditionalContext);
        Assert.Contains("signed-in: false", initial.AdditionalContext);

        await profiles.SaveAndActivateProfileAsync(Profile("active", SubscriptionOne, TenantOne));
        var refreshed = await config.Hooks.OnUserPromptSubmitted!(null!, null!);
        Assert.Contains($"subscription-id: {SubscriptionOne}", refreshed!.AdditionalContext);
        Assert.DoesNotContain("failed help context", refreshed.AdditionalContext);
        Assert.DoesNotContain("NOT the classic Azure CLI", refreshed.AdditionalContext);
        Assert.Equal(0, fixture.GenerationCount);
        Assert.Null(config.ReasoningEffort);
        Assert.Equal("gpt-5.4", new AgentOptions().Builtin.ModelId);
        Assert.NotNull(config.OnPermissionRequest);
        Assert.NotNull(config.Hooks.OnPreToolUse);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await config.Hooks.OnSessionStart!(null!, null!));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await config.Hooks.OnUserPromptSubmitted!(null!, null!));
    }

    internal static SessionConfig CreateSessionConfig(
        string executable, IProfileService profiles, HelpTools tools, string? failedHelp = null, CancellationToken ct = default)
    {
        var auth = new StubAuthService();
        return ChatAgent.BuildBaseSessionConfig(
            "unchanged prompt", new CachedResourceTools(new ArmDetailsClient(auth), profiles),
            tools, new AvailableLocationsTool(auth, profiles), new SubmitResponseTool(), new ProposePlanTool(),
            executable, profiles, new ConsoleApprovalPrompt(), new EventRenderer(),
            new SessionState { IsOneShot = true }, failedHelp, "oneshot", ct);
    }

    private static Profile Profile(string name, string subscription, string tenant) =>
        new() { Name = name, SubscriptionId = subscription, TenantId = tenant };

    private sealed class FailingProfileService(Exception failure) : StubProfileService, IProfileService
    {
        Task<Profile?> IProfileService.GetActiveProfileAsync(CancellationToken ct) => Task.FromException<Profile?>(failure);
    }

    private sealed class WaitingProfileService : StubProfileService, IProfileService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<Profile?> IProfileService.GetActiveProfileAsync(CancellationToken ct)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return null;
        }
    }
}
