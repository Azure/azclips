// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.Configuration.Implementation;
using Core.Configuration.Options;
using Core.Configuration.Schema;
using Moq;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class ConfigurationServiceTests
{
    [Fact]
    public async Task ConfigurationReaders_WaitForSharingConflict_ThenReadStoredValues()
    {
        var home = Directory.CreateTempSubdirectory("azclips-config-reader-tests-").FullName;
        var previousHome = Environment.GetEnvironmentVariable("AZCLIPS_HOME");
        try
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", home);
            var provider = new FileConfigurationProvider();
            await provider.WriteAsync(new JsonObject
            {
                ["agent"] = new JsonObject { ["enabled"] = true, ["excludedCommands"] = "seed" }
            });
            var schema = new ConfigurationSchema();
            var service = new ConfigurationService(provider, schema, new DefaultConfigurationValidator(schema));
            using var blocked = new FileStream(provider.GetConfigFilePath(), FileMode.Open, FileAccess.Read,
                OperatingSystem.IsWindows() ? FileShare.None : FileShare.Read);
            var rawRead = provider.ReadAsync();
            var typedRead = Task.Run(() => service.GetConfiguration<AgentOptions>());
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    await Task.Delay(150);
                    Assert.False(rawRead.IsCompleted);
                    Assert.False(typedRead.IsCompleted);
                }
            }
            finally
            {
                blocked.Dispose();
                await Task.WhenAll(rawRead, typedRead);
            }
            Assert.Equal("seed", (await rawRead)["agent"]!["excludedCommands"]!.GetValue<string>());
            Assert.Equal("seed", (await typedRead).ExcludedCommands);
            Assert.True((await typedRead).Enabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", previousHome);
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task FileConfigurationProvider_CanceledWrite_PreservesDocumentAndCleansTemporaryFile()
    {
        var home = Directory.CreateTempSubdirectory("azclips-config-cancellation-tests-").FullName;
        var previousHome = Environment.GetEnvironmentVariable("AZCLIPS_HOME");
        try
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", home);
            var provider = new FileConfigurationProvider();
            await provider.WriteAsync(new JsonObject { ["value"] = "old" });
            using (var blocked = new FileStream(provider.GetConfigFilePath(), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var cancellation = new CancellationTokenSource();
                if (OperatingSystem.IsWindows())
                    cancellation.CancelAfter(150);
                else
                    cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                    provider.WriteAsync(new JsonObject { ["value"] = "new" }, cancellation.Token));
            }
            Assert.Equal("old", (await provider.ReadAsync())["value"]!.GetValue<string>());
            Assert.Empty(Directory.GetFiles(home, "config.*.tmp"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", previousHome);
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task FileConfigurationProvider_WriteAsync_WaitsForReaderThenReplacesDocument()
    {
        var home = Directory.CreateTempSubdirectory("azclips-config-snapshot-tests-").FullName;
        var previousHome = Environment.GetEnvironmentVariable("AZCLIPS_HOME");
        try
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", home);
            var provider = new FileConfigurationProvider();
            await provider.WriteAsync(new JsonObject { ["value"] = "old" });
            var path = provider.GetConfigFilePath();
            Task write;
            using (var snapshot = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                write = provider.WriteAsync(new JsonObject { ["value"] = "new" });
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        await Task.Delay(150);
                        Assert.False(write.IsCompleted, "The writer must wait for the Windows reader to close.");
                    }
                    using var reader = new StreamReader(snapshot);
                    var original = JsonNode.Parse(await reader.ReadToEndAsync())!;
                    Assert.Equal("old", original["value"]!.GetValue<string>());
                }
                finally
                {
                    snapshot.Dispose();
                    await write;
                }
            }
            Assert.Equal("new", (await provider.ReadAsync())["value"]!.GetValue<string>());
            Assert.Empty(Directory.GetFiles(home, "config.*.tmp"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_HOME", previousHome);
            Directory.Delete(home, recursive: true);
        }
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldRoundTripTypedOptions()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.logging.level", "debug");

        var options = service.GetConfiguration<CoreOptions>();

        Assert.Equal("debug", options.Logging.Level);
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldRoundTripPowerShellOutputFormat()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        Assert.Equal("object", service.GetConfiguration<ClientsOptions>().PowerShell.Output.Format);

        await service.SetConfigurationAsync("clients.powershell.output.format", "json");

        Assert.Equal("json", service.GetConfiguration<ClientsOptions>().PowerShell.Output.Format);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldPruneEmptyParents()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.logging.level", "debug");
        await service.DeleteConfigurationAsync("core.logging.level");

        var values = await service.ListConfigurationAsync();
        var json = await fixture.Provider.ReadAsync();

        Assert.DoesNotContain("core.logging.level", values.Keys);
        Assert.False(json.ContainsKey("core"));
    }

    [Fact]
    public async Task GetConfiguration_ShouldPreferEnvironmentOverrides()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("core.logging.level", "info");

        Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", "error");

        try
        {
            var options = service.GetConfiguration<CoreOptions>();

            Assert.Equal("error", options.Logging.Level);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", null);
        }
    }

    [Fact]
    public async Task GetConfiguration_ShouldAcceptColonDelimitedKey()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("core.logging.level", "warn");

        var exception = Record.Exception(() => service.GetConfiguration<CoreOptions>("core:logging:level"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldUseSchemaCanonicalKeyCasing_ForKnownKeys()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.http.maxretriesfor429", 7);

        var values = await service.ListConfigurationAsync();

        Assert.Contains("core.http.maxRetriesFor429", values.Keys);
    }

    [Fact]
    public async Task GetConfiguration_ShouldThrowClearError_WhenConfigFileContainsInvalidJson()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), "{ invalid json }");

        var exception = Assert.Throws<InvalidInputException>(() => service.GetConfiguration<CoreOptions>());

        Assert.Contains(fixture.Provider.GetConfigFilePath(), exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("invalid json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldIncludeDefaults_WhenRequested()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        Assert.Equal(512, values["core.engine.extensionIndexCacheSize"]);
        Assert.Equal(3, values["core.http.maxRetries"]);
    }

    [Fact]
    public async Task ListConfigurationWithSourcesAsync_ShouldReturnOneSourceAwareSnapshot()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("core.logging.level", "debug");
        var values = await service.ListConfigurationWithSourcesAsync();

        Assert.Equal("debug", values["core.logging.level"].Value);
        Assert.Equal("global config", values["core.logging.level"].Source);
        Assert.Equal("oneshot", values["agent.sessionStyle"].Value);
        Assert.Equal("default", values["agent.sessionStyle"].Source);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldExposeExactlyTheSchemasAgentKeys()
    {
        // Both schema and defaults now traverse configuration attributes. Their agent key sets
        // must agree, and the shared ARM scope must remain non-configurable.
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        var exposed = values.Keys
            .Where(k => k.StartsWith("agent.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var schema = new ConfigurationSchema().GetAllKeys()
            .Select(e => e.Key)
            .Where(k => k.StartsWith("agent.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(schema, exposed, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("agent.builtin.scope", exposed, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResolveDefaultParameterAsync_ShouldReturnDefaultsValue_WhenConfigured()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("defaults.group", "my-rg");

        var value = await service.ResolveDefaultParameterAsync("group");

        Assert.Equal("my-rg", value);
    }


    [Fact]
    public void GetConfiguration_ShouldDefaultAgentDisabled_OnEmptyConfig()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var options = service.GetConfiguration<AgentOptions>();

        // PP ships agent off; the user opts in via `agent.enabled=true`. If this
        // flips, a fresh install would auto-spawn the agent on any failed command.
        Assert.False(options.Enabled);
        Assert.Equal("oneshot", options.SessionStyle);
        Assert.Equal("builtin", options.Provider);
        Assert.Equal("gpt-5.4-mini", options.Builtin.ModelId);
    }

    [Fact]
    public async Task GetConfiguration_ShouldUseConfiguredBuiltinModel_InsteadOfDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.builtin.modelId", "custom-deployment");

        var options = service.GetConfiguration<AgentOptions>();

        Assert.Equal("custom-deployment", options.Builtin.ModelId);
        Assert.Equal("custom-deployment", Core.Telemetry.RecoveryTelemetry.GetConfiguredModel(options));
    }

    [Fact]
    public void GetConfiguration_ShouldDefaultBuiltinModelToMini_OnEmptyConfig()
    {
        using var fixture = new TempConfigurationFixture();
        var options = fixture.CreateService().GetConfiguration<AgentOptions>();

        Assert.Equal("builtin", options.Provider);
        Assert.Equal("gpt-5.4-mini", options.Builtin.ModelId);
    }

    [Theory]
    [InlineData("gpt-5.4")]
    [InlineData("custom-deployment")]
    public async Task GetConfiguration_ShouldPreserveExplicitBuiltinModel(string model)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("agent.builtin.modelId", model);

        Assert.Equal(model, service.GetConfiguration<AgentOptions>().Builtin.ModelId);
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        Assert.Equal(model, values["agent.builtin.modelId"]);
    }

    [Fact]
    public async Task DeleteConfigurationAsync_ShouldRestoreBuiltinModelDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.builtin.modelId", "gpt-5.4");

        await service.DeleteConfigurationAsync("agent.builtin.modelId");

        Assert.Equal("gpt-5.4-mini", service.GetConfiguration<AgentOptions>().Builtin.ModelId);
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        Assert.Equal("gpt-5.4-mini", values["agent.builtin.modelId"]);
    }

    [Fact]
    public async Task GetConfiguration_ShouldPreserveGithubProviderSelection()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.provider", "github");

        Assert.Equal("github", service.GetConfiguration<AgentOptions>().Provider);
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        Assert.Equal("github", values["agent.provider"]);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldReportBuiltinModelDefault()
    {
        using var fixture = new TempConfigurationFixture();
        var values = await fixture.CreateService().ListConfigurationAsync(includeDefaults: true);

        Assert.Equal("gpt-5.4-mini", values["agent.builtin.modelId"]);
    }

    [Fact]
    public void GetConfiguration_ShouldDefaultCompletionEnabled_OnEmptyConfig()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var options = service.GetConfiguration<CompletionOptions>();

        // Completion is opt-out: sourcing a completion script works out of the box.
        Assert.True(options.Enabled);
    }

    [Fact]
    public async Task SetConfigurationAsync_ShouldRoundTripCompletionEnabled()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("completion.enabled", false);

        var options = service.GetConfiguration<CompletionOptions>();

        Assert.False(options.Enabled);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldReportCompletionEnabledDefaultTrue()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        // Schema/list default must match the POCO default (CompletionOptions.Enabled=true).
        Assert.Equal(true, values["completion.enabled"]);
    }

    [Fact]
    public async Task ListConfigurationAsync_ShouldReportAgentEnabledDefaultFalse()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var values = await service.ListConfigurationAsync(includeDefaults: true);

        // Schema default must match the POCO default (AgentOptions.Enabled=false);
        // a drift between the two is the "two source-of-truth defaults" anti-pattern.
        Assert.Equal(false, values["agent.enabled"]);
    }

    [Fact]
    public async Task AgentReasoningEffort_ShouldDefaultToMedium_InTypedOptionsAndListing()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        var options = service.GetConfiguration<AgentOptions>();
        var values = await service.ListConfigurationAsync(includeDefaults: true);
        var overrides = await service.ListConfigurationAsync();

        Assert.Equal("medium", options.ReasoningEffort);
        Assert.Equal("medium", values["agent.reasoningEffort"]);
        Assert.DoesNotContain("agent.reasoningEffort", overrides.Keys);
        Assert.Equal("builtin", options.Provider);
        Assert.Equal(new BuiltinProviderOptions().ModelId, options.Builtin.ModelId);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public async Task AgentReasoningEffort_ShouldRoundTripAndResetToMedium(string effort)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();

        await service.SetConfigurationAsync("agent.reasoningEffort", effort);

        Assert.Equal(effort, service.GetConfiguration<AgentOptions>().ReasoningEffort);
        Assert.Equal(effort, (await service.ListConfigurationAsync(includeDefaults: true))["agent.reasoningEffort"]);
        var json = await fixture.Provider.ReadAsync();
        Assert.Equal(effort, json["agent"]?["reasoningEffort"]?.GetValue<string>());

        await service.DeleteConfigurationAsync("agent.reasoningEffort");

        Assert.Equal("medium", service.GetConfiguration<AgentOptions>().ReasoningEffort);
        Assert.Equal("medium", (await service.ListConfigurationAsync(includeDefaults: true))["agent.reasoningEffort"]);
        Assert.DoesNotContain("agent.reasoningEffort", (await service.ListConfigurationAsync()).Keys);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("xhigh")]
    [InlineData("")]
    [InlineData(" medium ")]
    [InlineData(null)]
    [InlineData(1)]
    [InlineData(true)]
    public async Task AgentReasoningEffort_ShouldRejectInvalidOverrides_WithoutChangingConfiguration(object? effort)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.reasoningEffort", "high");

        var exception = await Assert.ThrowsAsync<InvalidInputException>(() =>
            service.SetConfigurationAsync("agent.reasoningEffort", effort));

        Assert.Contains("agent.reasoningEffort", exception.Message);
        Assert.Equal("high", service.GetConfiguration<AgentOptions>().ReasoningEffort);
    }


    [Fact]
    public async Task UpdateListConfigurationAsync_ConcurrentWritersPreserveBothEntries()
    {
        using var fixture = new TempConfigurationFixture();
        var first = fixture.CreateService();
        var second = fixture.CreateService();

        await Task.WhenAll(
            first.UpdateListConfigurationAsync("agent.excludedCommands", current => CommandExclusionList.Add(current, "Login")),
            second.UpdateListConfigurationAsync("agent.excludedCommands", current => CommandExclusionList.Add(current, "profile show")));

        var entries = CommandExclusionList.Split(first.GetConfiguration<AgentOptions>().ExcludedCommands);
        Assert.Equal(2, entries.Count);
        Assert.Contains("Login", entries);
        Assert.Contains("profile show", entries);
    }

    [Fact]
    public async Task UpdateListConfigurationAsync_StartsFromStoredValueAndPreservesOtherKeys()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.enabled", true);

        await service.UpdateListConfigurationAsync("AGENT:EXCLUDEDCOMMANDS", current =>
        {
            Assert.Empty(current);
            return "Login,unknown command";
        });

        var stored = await service.ListConfigurationAsync();
        Assert.Equal("Login,unknown command", stored["agent.excludedCommands"]);
        Assert.Equal(true, stored["agent.enabled"]);
    }

    [Theory]
    [InlineData("core.logging.level")]
    [InlineData("unknown.key")]
    public async Task UpdateListConfigurationAsync_RejectsNonListKeys(string key)
    {
        using var fixture = new TempConfigurationFixture();
        await Assert.ThrowsAsync<InvalidInputException>(() =>
            fixture.CreateService().UpdateListConfigurationAsync(key, _ => "login"));
        Assert.False(File.Exists(fixture.Provider.GetConfigFilePath()));
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("[\"login\"]")]
    [InlineData("[\"unused,login,unused\"]")]
    [InlineData("[\"unused;login;unused\"]")]
    [InlineData("{\"path\":\"login\"}")]
    public async Task MalformedExclusionList_DoesNotBreakReadingOtherAgentOptions(string listJson)
    {
        using var fixture = new TempConfigurationFixture();
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(),
            "{\"agent\":{\"enabled\":true,\"excludedCommands\":" + listJson + "}}");
        var service = fixture.CreateService();
        var options = service.GetConfiguration<AgentOptions>();
        Assert.True(options.Enabled);
        Assert.Throws<FormatException>(() => Core.Cli.Program.ReadExcludedCommands(service));

        var previous = Environment.GetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE");
        Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", "1");
        try
        {
            var read = false;
            var telemetry = new Mock<ITelemetryService>();
            var result = await Core.Cli.Program.TryAgentFallbackAsync(
                () => options, telemetry.Object, ["login"], "failure", null, null, null,
                getExcludedCommands: () =>
                {
                    read = true;
                    return Core.Cli.Program.ReadExcludedCommands(service);
                },
                commandTokens: ["login"]);
            Assert.True(read);
            Assert.Null(result);
            telemetry.Verify(t => t.StartRootActivity(It.IsAny<DateTimeOffset>(), It.IsAny<string?>()), Times.Never);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_IN_AGENT_MODE", previous);
        }
    }

    [Fact]
    public async Task FormatSource_DistinguishesPropertyDefaultFromStoredJson()
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        Assert.Equal("json", service.GetConfiguration<ClientsOptions>().Cli.Output.Format);
        Assert.False(Core.Cli.Output.OutputFormatPolicy.IsFormatUserSet(service));

        await service.SetConfigurationAsync("clients.cli.output.format", "json");

        Assert.True(Core.Cli.Output.OutputFormatPolicy.IsFormatUserSet(service));
    }

    [Theory]
    [InlineData("""{"AGENT":{"EXCLUDEDCOMMANDS":"Login,logout","enabled":true}}""")]
    [InlineData("""{"agent":{"ExcludedCommands":"Login,logout","enabled":true}}""")]
    [InlineData("""{"AGENT.EXCLUDEDCOMMANDS":"Login,logout","agent:enabled":true}""")]
    [InlineData("""{"AGENT:EXCLUDEDCOMMANDS":"Login,logout","agent:enabled":true}""")]
    [InlineData("""{"AGENT":{"EXCLUDEDCOMMANDS":"Login,logout"},"agent:enabled":true}""")]
    public async Task ListMutations_PreserveLogicalStoredKeysAcrossCasingAndDelimiters(string json)
    {
        using var fixture = new TempConfigurationFixture();
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), json);
        var service = fixture.CreateService();
        Assert.Equal("Login,logout", Core.Cli.Program.ReadExcludedCommands(service));

        await service.UpdateListConfigurationAsync("agent.excludedCommands", current => CommandExclusionList.Add(current, "profile"));
        Assert.Equal("Login,logout,profile", service.GetConfiguration<AgentOptions>().ExcludedCommands);
        await service.UpdateListConfigurationAsync("AGENT:EXCLUDEDCOMMANDS", current => CommandExclusionList.Remove(current, "LOGIN"));
        Assert.Equal("logout,profile", service.GetConfiguration<AgentOptions>().ExcludedCommands);
        Assert.True(service.GetConfiguration<AgentOptions>().Enabled);
        var stored = await service.ListConfigurationAsync();
        Assert.Equal(2, stored.Count);
        Assert.Equal("logout,profile", stored["agent.excludedCommands"]);

        await service.SetConfigurationAsync("AGENT:EXCLUDEDCOMMANDS", "replacement");
        Assert.Equal("replacement", service.GetConfiguration<AgentOptions>().ExcludedCommands);
        await service.DeleteConfigurationAsync("agent.excludedCommands");
        Assert.Empty(service.GetConfiguration<AgentOptions>().ExcludedCommands);
        Assert.True(service.GetConfiguration<AgentOptions>().Enabled);
        Assert.Single(await service.ListConfigurationAsync());
    }

    [Theory]
    [InlineData("""{"AGENT":{"EXCLUDEDCOMMANDS":"Login"}}""")]
    [InlineData("""{"AGENT.EXCLUDEDCOMMANDS":"Login"}""")]
    [InlineData("""{"AGENT:EXCLUDEDCOMMANDS":"Login"}""")]
    public async Task DeleteConfigurationAsync_RemovesOriginalNoncanonicalStoredKey(string json)
    {
        using var fixture = new TempConfigurationFixture();
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), json);
        var service = fixture.CreateService();
        await service.DeleteConfigurationAsync("agent.excludedCommands");
        Assert.Empty(await fixture.Provider.ReadAsync());
        Assert.Empty(service.GetConfiguration<AgentOptions>().ExcludedCommands);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("yaml")]
    public async Task FlatColonKeys_AgreeWithTypedOptionsAndProvenance(string format)
    {
        using var fixture = new TempConfigurationFixture();
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(),
            $$"""{"agent:enabled":true,"AGENT:EXCLUDEDCOMMANDS":"login","CLIENTS:CLI:OUTPUT:FORMAT":"{{format}}"}""");
        var service = fixture.CreateService();
        Assert.Equal("login", service.GetConfiguration<AgentOptions>().ExcludedCommands);
        Assert.Equal("login", Core.Cli.Program.ReadExcludedCommands(service));
        Assert.Equal(format, service.GetConfiguration<ClientsOptions>().Cli.Output.Format);
        Assert.True(Core.Cli.Output.OutputFormatPolicy.IsFormatUserSet(service));
        var sources = await service.ListConfigurationWithSourcesAsync();
        Assert.Equal("global config", sources["clients.cli.output.format"].Source);
        Assert.Equal(format, sources["clients.cli.output.format"].Value);
        Assert.Single(sources.Keys, k => k.Equals("clients.cli.output.format", StringComparison.OrdinalIgnoreCase));

        var telemetry = new Mock<ITelemetryService>();
        using var skipped = new System.Diagnostics.Activity("test-skip");
        telemetry.Setup(t => t.StartRootActivity(It.IsAny<DateTimeOffset>(), "CLI.Activity.AiRecoverySkipped"))
            .Returns(skipped);
        var result = await Core.Cli.Program.TryAgentFallbackAsync(
            () => service.GetConfiguration<AgentOptions>(), telemetry.Object, ["login"], "failure", null, null, null,
            getExcludedCommands: () => Core.Cli.Program.ReadExcludedCommands(service),
            commandTokens: ["login"], commandResolved: true);
        Assert.Null(result);
        telemetry.Verify(t => t.StartRootActivity(It.IsAny<DateTimeOffset>(), "CLI.Activity.AiRecoverySkipped"), Times.Once);
        Assert.Equal("excluded", skipped.GetTagItem(TelemetryTags.RecoverySkipReason));
    }

    [Theory]
    [InlineData("""{"AGENT":{"EXCLUDEDCOMMANDS":"Login,logout","enabled":true}}""")]
    [InlineData("""{"agent":{"ExcludedCommands":"Login,logout","enabled":true}}""")]
    [InlineData("""{"AGENT.EXCLUDEDCOMMANDS":"Login,logout","agent:enabled":true}""")]
    [InlineData("""{"AGENT:EXCLUDEDCOMMANDS":"Login,logout","agent:enabled":true}""")]
    public async Task Remove_PreservesEntriesFromOriginalNoncanonicalStoredKey(string json)
    {
        using var fixture = new TempConfigurationFixture();
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), json);
        var service = fixture.CreateService();
        await service.UpdateListConfigurationAsync("agent.excludedCommands", current => CommandExclusionList.Remove(current, "LOGIN"));
        Assert.Equal("logout", service.GetConfiguration<AgentOptions>().ExcludedCommands);
        Assert.True(service.GetConfiguration<AgentOptions>().Enabled);
        Assert.Equal(2, (await service.ListConfigurationAsync()).Count);
    }

    [Theory]
    [InlineData("[\"unused,login,unused\"]")]
    [InlineData("{\"unused\":\"login\"}")]
    [InlineData("null")]
    public async Task JsonLookingString_RemainsTextForExclusionReadsAndMutations(string text)
    {
        using var fixture = new TempConfigurationFixture();
        var service = fixture.CreateService();
        await service.SetConfigurationAsync("agent.excludedCommands", text);
        Assert.Equal(text, Core.Cli.Program.ReadExcludedCommands(service));
        await service.UpdateListConfigurationAsync("agent.excludedCommands", current =>
        {
            Assert.Equal(text, current);
            return CommandExclusionList.Add(current, "profile");
        });
        Assert.Equal(CommandExclusionList.Add(text, "profile"), Core.Cli.Program.ReadExcludedCommands(service));
    }

    [Theory]
    [InlineData("[\"unused,login,unused\"]")]
    [InlineData("{\"path\":\"login\"}")]
    [InlineData("42")]
    public async Task ListMutation_RejectsMalformedStoredValuesWithoutChangingTheDocument(string value)
    {
        using var fixture = new TempConfigurationFixture();
        var json = """{"agent":{"excludedCommands":""" + value + "}}";
        await File.WriteAllTextAsync(fixture.Provider.GetConfigFilePath(), json);
        var service = fixture.CreateService();

        await Assert.ThrowsAsync<InvalidInputException>(() =>
            service.UpdateListConfigurationAsync("agent.excludedCommands", current => CommandExclusionList.Add(current, "profile")));
        Assert.Equal(json, await File.ReadAllTextAsync(fixture.Provider.GetConfigFilePath()));
    }

    private sealed class TempConfigurationFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "azclips-config-tests", Guid.NewGuid().ToString("N"));

        public TempConfigurationFixture()
        {
            Directory.CreateDirectory(_directory);
            Provider = new TestConfigurationProvider(Path.Combine(_directory, "config.json"));
        }

        public TestConfigurationProvider Provider { get; }

        public ConfigurationService CreateService()
        {
            var schema = new ConfigurationSchema();
            var validator = new DefaultConfigurationValidator(schema);
            return new ConfigurationService(Provider, schema, validator);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed class TestConfigurationProvider(string configFilePath) : IConfigurationProvider
    {
        public string GetConfigFilePath() => configFilePath;

        public async Task<JsonObject> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(configFilePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(configFilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonNode.Parse(json) as JsonObject ?? [];
        }

        public async Task WriteAsync(JsonObject configuration, CancellationToken cancellationToken = default)
        {
            var directory = Path.GetDirectoryName(configFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(configFilePath, configuration.ToJsonString(new() { WriteIndented = true }), cancellationToken);
        }
    }
}
