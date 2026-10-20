using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Agent.Hooks;
using Core.Agent.Hosting;
using Core.Agent.Plan;
using Core.Agent.Tools.Help;
using Core.Agent.Tools.Resource.Cache;
using Core.Agent.Tools.Resource.Sku;
using Core.Configuration.Implementation;
using Core.Configuration.Schema;
using GitHub.Copilot;
using Tests.Common.Stubs;

namespace Core.Agent.Tests;

public class SessionConfigTests
{
    [Theory]
    [InlineData("builtin", "oneshot", null, "medium")]
    [InlineData("builtin", "interactive", null, "medium")]
    [InlineData("github", "oneshot", null, "medium")]
    [InlineData("github", "interactive", null, "medium")]
    [InlineData("builtin", "oneshot", "low", "low")]
    [InlineData("builtin", "oneshot", "medium", "medium")]
    [InlineData("builtin", "oneshot", "high", "high")]
    [InlineData("builtin", "interactive", "low", "low")]
    [InlineData("builtin", "interactive", "medium", "medium")]
    [InlineData("builtin", "interactive", "high", "high")]
    [InlineData("github", "oneshot", "low", "low")]
    [InlineData("github", "oneshot", "medium", "medium")]
    [InlineData("github", "oneshot", "high", "high")]
    [InlineData("github", "interactive", "low", "low")]
    [InlineData("github", "interactive", "medium", "medium")]
    [InlineData("github", "interactive", "high", "high")]
    public async Task ConfiguredEffort_ReachesSdk_ForEveryProviderAndStyle(
        string provider, string sessionStyle, string? effort, string expectedEffort)
    {
        using var fixture = new ConfigurationFixture();
        await fixture.Service.SetConfigurationAsync("agent.provider", provider);
        await fixture.Service.SetConfigurationAsync("agent.sessionStyle", sessionStyle);
        if (effort is not null)
            await fixture.Service.SetConfigurationAsync("agent.reasoningEffort", effort);
        var auth = new RecordingAuthService();
        var options = fixture.Service.GetConfiguration<AgentOptions>();

        var config = await BuildAsync(options, auth);

        Assert.Equal(expectedEffort, config.ReasoningEffort);
        if (provider == "builtin")
        {
            Assert.Equal(options.Builtin.ModelId, config.Model);
            Assert.NotNull(config.Provider);
            Assert.Equal(options.Builtin.ModelId, config.Provider.ModelId);
            Assert.Equal(options.Builtin.ModelId, config.Provider.WireModel);
            Assert.Equal("azure", config.Provider.Type);
            Assert.Equal("responses", config.Provider.WireApi);
            Assert.Equal(1, auth.CallCount);
        }
        else
        {
            Assert.Null(config.Model);
            Assert.Null(config.Provider);
            Assert.Equal(0, auth.CallCount);
        }
    }

    [Theory]
    [InlineData("builtin", "oneshot")]
    [InlineData("builtin", "interactive")]
    [InlineData("github", "oneshot")]
    [InlineData("github", "interactive")]
    public async Task DeletedOverride_SendsMediumToSdk(string provider, string sessionStyle)
    {
        using var fixture = new ConfigurationFixture();
        await fixture.Service.SetConfigurationAsync("agent.provider", provider);
        await fixture.Service.SetConfigurationAsync("agent.sessionStyle", sessionStyle);
        await fixture.Service.SetConfigurationAsync("agent.reasoningEffort", "high");
        var configured = await BuildAsync(fixture.Service.GetConfiguration<AgentOptions>(), new RecordingAuthService());
        Assert.Equal("high", configured.ReasoningEffort);

        await fixture.Service.DeleteConfigurationAsync("agent.reasoningEffort");
        var reset = await BuildAsync(fixture.Service.GetConfiguration<AgentOptions>(), new RecordingAuthService());

        Assert.Equal("medium", reset.ReasoningEffort);
    }

    [Theory]
    [InlineData("LOW", "low")]
    [InlineData("Medium", "medium")]
    [InlineData("HIGH", "high")]
    public async Task CaseInsensitiveConfiguration_UsesSdkWireCasing(string effort, string expected)
    {
        using var fixture = new ConfigurationFixture();
        await fixture.Service.SetConfigurationAsync("agent.reasoningEffort", effort);

        var config = await BuildAsync(fixture.Service.GetConfiguration<AgentOptions>(), new RecordingAuthService());

        Assert.Equal(expected, config.ReasoningEffort);
    }

    [Theory]
    [InlineData("builtin", "oneshot", "extreme")]
    [InlineData("builtin", "interactive", "extreme")]
    [InlineData("github", "oneshot", "extreme")]
    [InlineData("github", "interactive", "extreme")]
    [InlineData("builtin", "oneshot", null)]
    [InlineData("github", "oneshot", "")]
    [InlineData("builtin", "interactive", " high ")]
    [InlineData("github", "interactive", "xhigh")]
    public async Task InvalidFileConfiguration_IsRejectedBeforeAuthentication(
        string provider, string sessionStyle, string? effort)
    {
        using var fixture = new ConfigurationFixture();
        // A hand-edited file bypasses config set's schema validation.
        await fixture.WriteAsync(new JsonObject
        {
            ["agent"] = new JsonObject
            {
                ["provider"] = provider,
                ["sessionStyle"] = sessionStyle,
                ["reasoningEffort"] = effort,
            },
        });
        var auth = new RecordingAuthService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            BuildAsync(fixture.Service.GetConfiguration<AgentOptions>(), auth));

        Assert.Contains("agent.reasoningEffort", exception.Message);
        Assert.Contains("low | medium | high", exception.Message);
        Assert.Equal(0, auth.CallCount);
    }

    [Theory]
    [InlineData("builtin")]
    [InlineData("github")]
    public async Task Effort_DoesNotChangeConfiguredProviderSettings(string provider)
    {
        var options = new AgentOptions
        {
            Provider = provider,
            ReasoningEffort = "high",
            Builtin = new BuiltinProviderOptions
            {
                ModelId = "custom-deployment",
                Endpoint = "https://example.invalid/openai/",
                ApiVersion = "test-api-version",
            },
        };
        var auth = new RecordingAuthService();
        using var cts = new CancellationTokenSource();

        var config = await BuildAsync(options, auth, ct: cts.Token);

        Assert.Equal("high", config.ReasoningEffort);
        if (provider == "builtin")
        {
            Assert.Equal("custom-deployment", config.Model);
            Assert.NotNull(config.Provider);
            Assert.Equal("custom-deployment", config.Provider.ModelId);
            Assert.Equal("custom-deployment", config.Provider.WireModel);
            Assert.Equal("https://example.invalid/openai/", config.Provider.BaseUrl);
            Assert.Equal("test-api-version", config.Provider.Azure?.ApiVersion);
            Assert.Equal("test-access-token", config.Provider.BearerToken);
            Assert.Equal(new[] { BuiltinProviderOptions.ArmScope }, auth.Scopes);
            Assert.False(auth.PromptOnError);
            Assert.Equal(cts.Token, auth.Token);
        }
        else
        {
            Assert.Null(config.Model);
            Assert.Null(config.Provider);
            Assert.Equal(0, auth.CallCount);
        }
    }

    [Theory]
    [InlineData("oneshot", true)]
    [InlineData("interactive", false)]
    public async Task Effort_PreservesSessionSettingsAndCallbacks(string sessionStyle, bool oneShot)
    {
        var renderer = new EventRenderer();
        var options = new AgentOptions { Provider = "github", SessionStyle = sessionStyle, ReasoningEffort = "low" };
        using var cts = new CancellationTokenSource();

        var config = await BuildAsync(options, new RecordingAuthService(), renderer, cts.Token);

        Assert.True(Guid.TryParse(config.SessionId, out _));
        Assert.Equal(HomePaths.GetDefaultHomeRoot(), config.ConfigDirectory);
        Assert.True(config.Streaming);
        Assert.Null(config.AvailableTools);
        Assert.NotNull(config.SystemMessage);
        Assert.Equal(SystemMessageMode.Customize, config.SystemMessage.Mode);
        var sections = config.SystemMessage.Sections!;
        Assert.Equal(SectionOverrideAction.Replace, sections[SystemMessageSection.Identity].Action);
        Assert.Equal("test system prompt", sections[SystemMessageSection.Identity].Content);
        Assert.Equal(SectionOverrideAction.Remove, sections[SystemMessageSection.Tone].Action);
        Assert.Equal(SectionOverrideAction.Remove, sections[SystemMessageSection.CodeChangeRules].Action);
        Assert.Equal(SectionOverrideAction.Remove, sections[SystemMessageSection.CustomInstructions].Action);
        Assert.Equal(oneShot, sections.ContainsKey(SystemMessageSection.ToolInstructions));
        Assert.Equal(oneShot, sections.ContainsKey(SystemMessageSection.RuntimeInstructions));
        Assert.Contains(config.Tools!, tool => tool.Name == "get_command");
        Assert.Contains(config.Tools!, tool => tool.Name == "propose_plan");
        Assert.Contains(config.Tools!, tool => tool.Name == "submit_response");
        Assert.Equal((Action<SessionEvent>)renderer.Forward, config.OnEvent);
        Assert.NotNull(config.Hooks?.OnSessionStart);
        Assert.NotNull(config.Hooks?.OnUserPromptSubmitted);
        Assert.NotNull(config.Hooks?.OnPreToolUse);
        var gate = await config.Hooks!.OnPreToolUse!(new PreToolUseHookInput
        {
            ToolName = "bash",
            ToolArgs = JsonSerializer.SerializeToElement(new { command = "echo test" }),
        }, default!);
        Assert.Equal("deny", gate?.PermissionDecision);

        Assert.NotNull(config.OnPermissionRequest);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await config.OnPermissionRequest!(new PermissionRequestCustomTool
            {
                ToolName = "mutating_tool",
                ToolDescription = "Test mutation requiring approval",
            }, default!));
    }

    [Fact]
    public async Task ReadOfAFileThatTheFailedCommandNames_IsApprovedWithoutPrompting()
    {
        var directory = Directory.CreateTempSubdirectory("azclips-session-files-").FullName;
        using var cts = new CancellationTokenSource();
        try
        {
            var template = Path.Combine(directory, "web-app.bicep");
            File.WriteAllText(template, "param name string");
            var options = new AgentOptions { Provider = "github", SessionStyle = "oneshot" };
            var state = new SessionState
            {
                IsOneShot = true,
                FailedCommandFiles = FailedCommandFiles.From("deployment group create --template-file web-app.bicep", directory),
            };
            var config = await BuildAsync(options, new RecordingAuthService(), ct: cts.Token, state: state);

#pragma warning disable GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var pending = config.OnPermissionRequest!(
                new PermissionRequestRead { Path = template, Intention = "Read the template" }, default!);
            // A read that went to the approval prompt would wait there until the session ends.
            Assert.True(pending == await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10))),
                "The read of the named file went to the approval prompt.");
            Assert.Equal(GitHub.Copilot.Rpc.PermissionDecision.ApproveOnce().Kind, (await pending).Kind);
#pragma warning restore GHCP001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            // Any other file still goes to the approval prompt, which stops when the session is canceled.
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await config.OnPermissionRequest!(new PermissionRequestRead
                {
                    Path = Path.Combine(directory, "other.bicep"), Intention = "Read a file",
                }, default!));
        }
        finally
        {
            cts.Cancel();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderAuthenticationCancellation_IsNotConvertedToAnError()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var auth = new RecordingAuthService();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BuildAsync(new AgentOptions(), auth, ct: cts.Token));

        Assert.Equal(cts.Token, exception.CancellationToken);
        Assert.Equal(1, auth.CallCount);
    }

    [Fact]
    public async Task UnknownProvider_StillReportsExistingError()
    {
        var auth = new RecordingAuthService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            BuildAsync(new AgentOptions { Provider = "unknown" }, auth));

        Assert.Contains("Unknown agent.provider value: 'unknown'. Expected: builtin | github.", exception.Message);
        Assert.Equal(0, auth.CallCount);
    }

    [Fact]
    public async Task SdkModelError_IsForwardedWithoutChangingEffort()
    {
        var events = new List<AgentEvent>();
        var renderer = new EventRenderer { OnEvent = events.Add };
        var config = await BuildAsync(
            new AgentOptions { Provider = "github", ReasoningEffort = "high" }, new RecordingAuthService(), renderer);

        config.OnEvent!(new SessionErrorEvent
        {
            Data = new SessionErrorData { ErrorType = "model_error", Message = "Model does not support reasoning effort." },
        });

        var error = Assert.IsType<SessionErrored>(Assert.Single(events));
        Assert.Equal("model_error", error.ErrorType);
        Assert.Equal("Model does not support reasoning effort.", error.Message);
        Assert.Equal("high", config.ReasoningEffort);
    }

    private static Task<SessionConfig> BuildAsync(
        AgentOptions options, IAuthService auth, EventRenderer? renderer = null, CancellationToken ct = default,
        SessionState? state = null)
    {
        const string azExePath = @"C:\test-only\az.exe";
        var profile = new StubProfileService();
        return ChatAgent.BuildSessionConfigAsync(
            options, auth, "test system prompt",
            new CachedResourceTools(new ArmDetailsClient(auth), profile),
            new HelpTools(azExePath),
            new AvailableLocationsTool(auth, profile),
            new SubmitResponseTool(),
            new ProposePlanTool(),
            azExePath,
            new ConsoleApprovalPrompt(),
            renderer ?? new EventRenderer(),
            state ?? new SessionState { IsOneShot = !options.IsInteractive },
            failedHelp: null,
            entrypoint: options.IsInteractive ? "inline" : "oneshot",
            ct);
    }

    private sealed class RecordingAuthService : IAuthService
    {
        public int CallCount { get; private set; }
        public string[]? Scopes { get; private set; }
        public bool PromptOnError { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<AuthResult> AcquireTokenSilentlyAsync(
            string[]? scopes, string? tenantId, bool promptOnError = false,
            CancellationToken cancellationToken = default, Profile? profile = null)
        {
            CallCount++;
            Scopes = scopes;
            PromptOnError = promptOnError;
            Token = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AuthResult
            {
                AccessToken = "test-access-token",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
                Scopes = scopes ?? [],
                TenantId = string.Empty,
            });
        }

        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ConfigurationFixture : Core.Configuration.Abstractions.IConfigurationProvider, IDisposable
    {
        private readonly string _directory = Path.Combine(AppContext.BaseDirectory, "session-config-tests", Guid.NewGuid().ToString("N"));

        public IConfigurationService Service { get; }

        public ConfigurationFixture()
        {
            Directory.CreateDirectory(_directory);
            var schema = new ConfigurationSchema();
            Service = new ConfigurationService(this, schema, new DefaultConfigurationValidator(schema));
        }

        public string GetConfigFilePath() => Path.Combine(_directory, "config.json");

        public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default) =>
            File.Exists(GetConfigFilePath())
                ? JsonNode.Parse(await File.ReadAllTextAsync(GetConfigFilePath(), cancellationToken))!.AsObject()
                : [];

        public Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default) =>
            File.WriteAllTextAsync(GetConfigFilePath(), configuration.ToJsonString(), cancellationToken);

        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }
}
