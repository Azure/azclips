// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.AssemblyLoading;
using Core.Configuration.Abstractions;
using Core.Cli.Output;
using Core.Cli.Parser;
using Core.DependencyInjection;
using Core.Extensions;
using Core.Runtime;
using Core.Runtime.Handler;
using Core.Runtime.Recovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Config.Commands;
using Tests.Common.Stubs;
using Tests.Common.Utilities;

namespace Cli.Tests;

[Collection("Azclips Service Provider Tests")]
public class ConfigCliTests
{
    [Fact]
    public void PublicRecoveryKeys_PreserveSiblingsAndNeverExposeTriggerStorageKey()
    {
        using var fixture = new ConfigCommandFixture();
        fixture.ExecuteCli(["config", "set", "agent.recovery.style", "interactive"]);
        fixture.ExecuteCli(["config", "set", "agent.recovery", "auto"]);
        fixture.ExecuteCli(["config", "set", "agent.recovery.historySize", "3"]);
        var list = fixture.ExecuteCli(["config", "list", "--all"]).Single();
        AssertEntry(list, "agent.recovery", "auto", "global config");
        Assert.DoesNotContain("agent.recovery.trigger", list);
        var section = fixture.ExecuteCli(["config", "get", "agent"]).Single();
        AssertEntry(section, "recovery", "auto", "global config");
        AssertEntry(section, "recovery.style", "interactive", "global config");
        AssertEntry(section, "recovery.historySize", 3, "global config");
        fixture.ExecuteCli(["config", "set", "agent.recovery", "off"]);
        fixture.ExecuteCli(["config", "delete", "agent.recovery"]);
        AssertSingleEntry(fixture.ExecuteCli(["config", "get", "agent.recovery"]).Single(), "agent.recovery", "off", "default");
        Assert.Contains("interactive", fixture.ExecuteCli(["config", "get", "agent.recovery.style"]).Single());
    }

    [Fact]
    public void ExistingPowerShellConfigCommands_UseTheSameCoordinatedMutationPath()
    {
        using var fixture = new ConfigCommandFixture(ClientType.PowerShell);
        fixture.ExecutePowerShell("Set-AzConfig", new() { ["Key"] = "agent.recovery.style", ["Value"] = "interactive" });
        fixture.ExecutePowerShell("Set-AzConfig", new() { ["Key"] = "agent.recovery", ["Value"] = "off" });
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(fixture.ConfigPath)!, "failure-history", EncryptedFailureHistoryStore.ControlFileName)));
        fixture.ExecutePowerShell("Remove-AzConfig", new() { ["Key"] = "agent.recovery" });
        AssertSingleEntry(fixture.ExecutePowerShell("Get-AzConfig", new() { ["Key"] = "agent.recovery" }).Single(),
            "agent.recovery", "off", "default");
        Assert.Contains("interactive",
            fixture.ExecutePowerShell("Get-AzConfig", new() { ["Key"] = "agent.recovery.style" }).Single());
    }

    [Fact]
    public void ArgumentParsingService_ShouldTreatBareBoolFlagsAsTrue()
    {
        var loggingService = new ConsoleLoggingService();
        var provider = new Core.Cli.CLIArgumentParsingProvider(new Core.Cli.PropertyParser(loggingService), loggingService);
        var parser = new Core.ArgumentParsing.ArgumentParsingService(provider, new ProfileService(new ScopedLoggingService()));
        var processor = new BoolFlagProcessor();

        parser.BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { ["all"] = new List<List<string>> { new() } },
            new Dictionary<string, ParameterMetadata>
            {
                ["all"] = new("All", "all", typeof(bool), "Include defaults.", false, [], [])
            },
            parameterAliases: null,
            profile: null);

        Assert.True(processor.All);
    }

    [Fact]
    public void ConfigCommands_ShouldSetGetListDeleteAndInfo()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var getOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        AssertConfigEntry(getOutput, "core.logging.level", "debug", "global config");

        var listOutput = fixture.ExecuteCli(["config", "list", "--all"]);
        AssertConfigEntry(listOutput, "core.logging.level", "debug", "global config");
        AssertConfigEntry(listOutput, "core.engine.extensionIndexCacheSize", 512, "default");

        var infoOutput = fixture.ExecuteCli(["config", "info", "--keys-only"]);
        Assert.Contains("core.logging.level", infoOutput.Single());

        fixture.ExecuteCli(["config", "delete", "--key", "core.logging.level", "--force"]);

        var getAfterDeleteOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        AssertConfigEntry(getAfterDeleteOutput, "core.logging.level", "warn", "default");

    }

    [Theory]
    [InlineData("low")]
    [InlineData("medium")]
    [InlineData("high")]
    public void ConfigCommands_ShouldExposeAgentReasoningEffortAndRestoreMedium(string effort)
    {
        using var fixture = new ConfigCommandFixture();

        var defaultOutput = fixture.ExecuteCli(["config", "get", "--key", "agent.reasoningEffort"]);
        AssertConfigEntry(defaultOutput, "agent.reasoningEffort", "medium", "default");

        var listOutput = fixture.ExecuteCli(["config", "list", "--all"]);
        AssertConfigEntry(listOutput, "agent.reasoningEffort", "medium", "default");

        var infoOutput = fixture.ExecuteCli(["config", "info", "--keys-only"]);
        Assert.Contains("agent.reasoningEffort", infoOutput.Single());

        fixture.ExecuteCli(["config", "set", "--key", "agent.reasoningEffort", "--value", effort]);

        var configuredOutput = fixture.ExecuteCli(["config", "get", "--key", "agent.reasoningEffort"]);
        AssertConfigEntry(configuredOutput, "agent.reasoningEffort", effort, "global config");

        fixture.ExecuteCli(["config", "delete", "--key", "agent.reasoningEffort"]);

        var resetOutput = fixture.ExecuteCli(["config", "get", "--key", "agent.reasoningEffort"]);
        AssertConfigEntry(resetOutput, "agent.reasoningEffort", "medium", "default");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigCommands_ShouldRejectInvalidAgentReasoningEffort_EvenWithForce(bool force)
    {
        using var fixture = new ConfigCommandFixture();
        string[] args = ["config", "set", "--key", "agent.reasoningEffort", "--value", "extreme"];
        if (force)
        {
            args = [.. args, "--force"];
        }

        var exception = Assert.Throws<InvalidInputException>(() => fixture.ExecuteCli(args));

        Assert.Contains("Value 'extreme' is not allowed for 'agent.reasoningEffort'.", exception.Message);
        var output = fixture.ExecuteCli(["config", "get", "--key", "agent.reasoningEffort"]);
        AssertConfigEntry(output, "agent.reasoningEffort", "medium", "default");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfigCommands_ShouldRejectNullEffortWithoutReplacingExistingValue(bool force)
    {
        using var fixture = new ConfigCommandFixture();
        fixture.ExecuteCli(["config", "set", "--key", "agent.reasoningEffort", "--value", "high"]);
        string[] args = ["config", "set", "--key", "agent.reasoningEffort", "--value", "null"];
        if (force)
            args = [.. args, "--force"];

        var error = Assert.Throws<InvalidInputException>(() => fixture.ExecuteCli(args));

        Assert.Contains("agent.reasoningEffort", error.Message);
        Assert.Contains("cannot be null", error.Message);
        AssertConfigEntry(fixture.ExecuteCli(["config", "get", "--key", "agent.reasoningEffort"]),
            "agent.reasoningEffort", "high", "global config");
    }

    [Fact]
    public void CliInvocation_ShouldSupportPositionalKeyAndValue()
    {
        // Regression test for `az config set core.output.color always` (no --key/--value flags).
        using var fixture = new ConfigCommandFixture();

        var setOutput = fixture.ExecuteCli(["config", "set", "core.output.color", "always", "--force"]);
        Assert.Contains("Set core.output.color = always", setOutput.Single());

        var getOutput = fixture.ExecuteCli(["config", "get", "core.output.color"]);
        AssertConfigEntry(getOutput, "core.output.color", "always", "global config");

        var deleteOutput = fixture.ExecuteCli(["config", "delete", "core.output.color", "--force"]);
        Assert.Contains("Deleted core.output.color", deleteOutput.Single());
    }

    [Fact]
    public void CliInvocation_ShouldSupportNamedConfigFlagsAcrossCrudFlow()
    {
        using var fixture = new ConfigCommandFixture();

        var setOutput = fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);
        Assert.Contains("Set core.logging.level = debug", setOutput.Single());

        var getOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        AssertConfigEntry(getOutput, "core.logging.level", "debug", "global config");

        var setIntegerOutput = fixture.ExecuteCli(["config", "set", "--key", "core.http.maxRetries", "--value", "9", "--force"]);
        Assert.Contains("Set core.http.maxRetries = 9", setIntegerOutput.Single());

        var getIntegerOutput = fixture.ExecuteCli(["config", "get", "--key", "core.http.maxRetries"]);
        AssertConfigEntry(getIntegerOutput, "core.http.maxRetries", 9, "global config");

        var listOutput = fixture.ExecuteCli(["config", "list"]);
        AssertConfigEntry(listOutput, "core.logging.level", "debug", "global config");
        AssertConfigEntry(listOutput, "core.http.maxRetries", 9, "global config");

        var deleteOutput = fixture.ExecuteCli(["config", "delete", "--key", "core.logging.level", "--force"]);
        Assert.Contains("Deleted core.logging.level", deleteOutput.Single());

        var getDeletedOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        AssertConfigEntry(getDeletedOutput, "core.logging.level", "warn", "default");
    }

    [Fact]
    public void CliInvocation_ShouldReturnStructuredListAndSchemaInfo()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var listJsonOutput = fixture.ExecuteCli(["config", "list", "--all", "--output", "json"]);
        var listEntries = JsonNode.Parse(listJsonOutput.Single())!.AsArray();
        Assert.Contains(listEntries, entry => string.Equals(entry?["key"]?.GetValue<string>(), "core.logging.level", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry?["value"]?.GetValue<string>(), "debug", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry?["source"]?.GetValue<string>(), "global config", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(listEntries, entry => string.Equals(entry?["key"]?.GetValue<string>(), "core.engine.extensionIndexCacheSize", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry?["source"]?.GetValue<string>(), "default", StringComparison.OrdinalIgnoreCase));

        var infoOutput = fixture.ExecuteCli(["config", "info"]);
        Assert.DoesNotContain("\\u0027", infoOutput.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'auto'", infoOutput.Single(), StringComparison.Ordinal);
        var infoEntries = JsonNode.Parse(infoOutput.Single())!.AsArray();
        Assert.Contains(infoEntries, entry => string.Equals(entry?["key"]?.GetValue<string>(), "core.logging.level", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CliInvocation_ShouldFormatConfigCommandsAsTables()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var getOutput = fixture.ExecuteCli(["config", "get", "core.logging.level", "--output", "table"]).Single();
        Assert.Contains("key", getOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value", getOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("source", getOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("core.logging.level", getOutput);
        Assert.Contains("debug", getOutput);

        var listOutput = fixture.ExecuteCli(["config", "list", "--all", "--output", "table"]).Single();
        Assert.Contains("core.logging.level", listOutput);
        Assert.Contains("core.engine.extensionIndexCacheSize", listOutput);

        var infoOutput = fixture.ExecuteCli(["config", "info", "--output", "table"]).Single();
        Assert.Contains("description", infoOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("core.logging.level", infoOutput);
    }

    [Fact]
    public void CliInvocation_ShouldConstrainConfigTableToTerminalWidth()
    {
        using var fixture = new ConfigCommandFixture();

        var output = fixture.ExecuteCli(["config", "info", "--output", "table"], terminalWidth: 60).Single();

        Assert.All(
            output.Split('\n'),
            line => Assert.True(line.Length <= 60, $"Expected line at most 60 characters, but got {line.Length}: {line}"));
    }

    [Fact]
    public void CliInvocation_ShouldPreferEnvironmentOverGlobalConfig()
    {
        using var fixture = new ConfigCommandFixture();

        // Set global config value
        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        // Verify global config is set
        var globalOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        AssertConfigEntry(globalOutput, "core.logging.level", "debug", "global config");

        // Set environment variable (this persists for subsequent CLI invocations within the fixture)
        Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", "error");

        try
        {
            // Get value with environment override
            var envOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
            AssertConfigEntry(envOutput, "core.logging.level", "error", "environment");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", null);
        }
    }

    [Fact]
    public void CliInvocation_ShouldGetSectionValues_WhenKeyIsSection()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);
        fixture.ExecuteCli(["config", "set", "--key", "core.http.maxRetries", "--value", "9", "--force"]);

        var output = fixture.ExecuteCli(["config", "get", "--key", "core"]);
        AssertConfigEntry(output, "logging.level", "debug", "global config");
        AssertConfigEntry(output, "http.maxRetries", 9, "global config");
    }

    [Fact]
    public void CliInvocation_ShouldReturnRelativeEntries_WhenGettingSubsection()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var output = fixture.ExecuteCli(["config", "get", "--key", "core.logging", "--output", "json"]);

        AssertConfigEntry(output, "level", "debug", "global config");
    }

    [Fact]
    public void CliInvocation_ShouldReturnRelativeEntries_WhenGettingSection()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);
        fixture.ExecuteCli(["config", "set", "--key", "core.http.maxRetries", "--value", "9", "--force"]);

        var output = fixture.ExecuteCli(["config", "get", "--key", "core", "--output", "json"]);

        AssertConfigEntry(output, "logging.level", "debug", "global config");
        AssertConfigEntry(output, "http.maxRetries", 9, "global config");
    }

    [Fact]
    public void CliInvocation_ShouldReturnStructuredOutput_WhenGettingSingleKey()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var output = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);

        Assert.IsType<JsonObject>(JsonNode.Parse(output.Single()));
        AssertConfigEntry(output, "core.logging.level", "debug", "global config");
    }

    [Fact]
    public void CliInvocation_ShouldThrow_WhenGettingUnknownSection()
    {
        using var fixture = new ConfigCommandFixture();

        var exception = Assert.Throws<InvalidInputException>(() =>
            fixture.ExecuteCli(["config", "get", "--key", "coreasdf"]));

        Assert.Contains("Configuration key 'coreasdf' was not found.", exception.Message);
    }

    [Fact]
    public void CliInvocation_ShouldPreferEnvironmentOverGlobalConfig_WhenGettingSection()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);
        Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", "error");

        try
        {
            var output = fixture.ExecuteCli(["config", "get", "--key", "core"]);
            AssertConfigEntry(output, "logging.level", "error", "environment");
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", null);
        }
    }

    [Fact]
    public void CliInvocation_ShouldHandleScalarThenNestedKeys_WhenGettingSection()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging", "--value", "plain", "--force"]);

        var output = fixture.ExecuteCli(["config", "get", "--key", "core"]);

        AssertConfigEntry(output, "logging.level", "warn", "default");
    }

    [Fact]
    public void CliInvocation_ShouldPreserveJsonNodeValues_WhenEnvironmentOverrideIsJson()
    {
        using var fixture = new ConfigCommandFixture();

        Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", "{\"kind\":\"structured\",\"enabled\":true}");

        try
        {
            var output = fixture.ExecuteCli(["config", "get", "--key", "core", "--output", "json"]);
            var entries = JsonNode.Parse(output.Single())!.AsArray();
            var loggingEntry = Assert.Single(
                entries,
                entry => entry?["key"]?.GetValue<string>() == "logging.level");

            Assert.Equal("structured", loggingEntry?["value"]?["kind"]?.GetValue<string>());
            Assert.True(loggingEntry?["value"]?["enabled"]?.GetValue<bool>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("AZCLIPS_LOG_LEVEL", null);
        }
    }

    [Fact]
    public void CliInvocation_ShouldResolveDefaultsNamespace()
    {
        using var fixture = new ConfigCommandFixture();

        // Set a defaults parameter
        var setOutput = fixture.ExecuteCli(["config", "set", "--key", "defaults.group", "--value", "my-rg", "--force"]);
        Assert.Contains("Set defaults.group = my-rg", setOutput.Single());

        // Retrieve the defaults parameter
        var getOutput = fixture.ExecuteCli(["config", "get", "--key", "defaults.group"]);
        AssertConfigEntry(getOutput, "defaults.group", "my-rg", "global config");

        // Verify it appears in list
        var listOutput = fixture.ExecuteCli(["config", "list"]);
        AssertConfigEntry(listOutput, "defaults.group", "my-rg", "global config");
    }

    [Theory]
    [InlineData("core.output.color", "auto", "adsf", "auto, always, never")]
    [InlineData("core.logging.level", "warn", "verbose", "debug, info, warn, error")]
    public void CliInvocation_ShouldRejectInvalidValue_WithAllowedValues_WithoutChangingConfiguration(
        string key, string validValue, string invalidValue, string allowedValues)
    {
        using var fixture = new ConfigCommandFixture();
        fixture.ExecuteCli(["config", "set", key, validValue]);

        var exception = Assert.Throws<InvalidInputException>(() =>
            fixture.ExecuteCli(["config", "set", key, invalidValue]));

        Assert.Equal($"Value '{invalidValue}' is not allowed for '{key}'. Allowed values: {allowedValues}.", exception.Message);
        Assert.Equal("InvalidInput", exception.Code);
        Assert.Equal(2, exception.ExitCode);
        AssertConfigEntry(fixture.ExecuteCli(["config", "get", key]), key, validValue, "global config");
    }

    [Fact]
    public void CliInvocation_ShouldRejectUnknownKey_WhenForceIsFalse()
    {
        using var fixture = new ConfigCommandFixture();

        // Attempt to set an unknown key without --force should throw
        var exception = Assert.Throws<InvalidInputException>(() =>
            fixture.ExecuteCli(["config", "set", "--key", "made.up.key", "--value", "value"])
        );

        Assert.Contains("Unknown configuration key 'made.up.key'", exception.Message);
        Assert.Contains("force option", exception.Message);
    }

    [Fact]
    public void CliInvocation_ShouldAllowUnknownKey_WhenForceIsTrue()
    {
        using var fixture = new ConfigCommandFixture();

        // Set an unknown key with --force
        var setOutput = fixture.ExecuteCli(["config", "set", "--key", "made.up.key", "--value", "value", "--force"]);
        Assert.Contains("Set made.up.key = value", setOutput.Single());

        // Retrieve it
        var getOutput = fixture.ExecuteCli(["config", "get", "--key", "made.up.key"]);
        AssertConfigEntry(getOutput, "made.up.key", "value", "global config");
    }

    [Fact]
    public void CliInvocation_ShouldRejectRemovedJsonParameter()
    {
        using var fixture = new ConfigCommandFixture();

        var exception = Assert.Throws<UsageErrorException>(() =>
            fixture.ExecuteCli(["config", "get", "core.logging.level", "--json"]));

        Assert.Contains("Unable to find parameter: json", exception.Message);
    }

    private static void AssertEntry(string output, string key, object expectedValue, string expectedSource) =>
        AssertConfigEntry([output], key, expectedValue, expectedSource);

    private static void AssertSingleEntry(string output, string key, object expectedValue, string expectedSource)
    {
        Assert.IsType<JsonObject>(JsonNode.Parse(output));
        AssertConfigEntry([output], key, expectedValue, expectedSource);
    }

    private static void AssertConfigEntry(
        IReadOnlyList<string> output,
        string key,
        object expectedValue,
        string expectedSource)
    {
        var result = JsonNode.Parse(output.Single());
        var entry = result switch
        {
            JsonObject configEntry when configEntry["key"]?.GetValue<string>() == key => configEntry,
            JsonArray entries => Assert.Single(entries, item => item?["key"]?.GetValue<string>() == key),
            _ => null
        };

        Assert.NotNull(entry);
        Assert.True(
            JsonNode.DeepEquals(JsonValue.Create(expectedValue), entry?["value"]),
            $"Expected value '{expectedValue}', but got '{entry?["value"]}'.");
        Assert.Equal(expectedSource, entry?["source"]?.GetValue<string>());
    }

    private sealed class ConfigCommandFixture : IDisposable
    {
        private static readonly string[] s_environmentKeys =
        [
            "AZCLIPS_LOG_LEVEL",
            "PS_HTTP_MAX_RETRIES",
            "AZURE_PS_HTTP_MAX_RETRIES",
            "PS_HTTP_MAX_RETRIES_FOR_429",
            "AZURE_PS_HTTP_MAX_RETRIES_FOR_429",
            "AZCLIPS_AGENT_RECOVERY",
            "AZCLIPS_AGENT_RECOVERY_STYLE",
            "AZCLIPS_AGENT_RECOVERY_HISTORY_SIZE"
        ];

        private readonly string _configPath;
        private readonly List<string> _outputs = [];
        private readonly List<string> _warnings = [];
        private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.OrdinalIgnoreCase);

        public string ConfigPath => _configPath;
        public IReadOnlyList<string> Warnings => _warnings;

        public ConfigCommandFixture(ClientType clientType = ClientType.CLI)
        {
            var directory = Path.GetFullPath(Path.Combine(".test-artifacts", "azclips-config-cli-tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            _configPath = Path.Combine(directory, "config.json");

            foreach (var key in s_environmentKeys)
            {
                _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, null);
            }

            AzclipsServiceProvider.Dispose();
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider>(new StubExtensionLoader(typeof(SetConfigCommand).Assembly, "Config"));
                services.AddSingleton<IConfigurationProvider>(new TestConfigurationProvider(_configPath));
                services.AddSingleton(new FailureHistoryStateStore(new FailureHistoryFiles(Path.Combine(directory, "failure-history"))));
                services.AddSingleton<ITelemetryService, StubTelemetryService>();
                services.AddSingleton<IProfileService, StubProfileService>();
                services.AddSingleton<IAuthService, StubAuthService>();
                // Register CLI-specific services for argument parsing
                services.AddSingleton<Core.Cli.PropertyParser>();
                services.AddSingleton<Core.Abstractions.IArgumentParsingProvider, Core.Cli.CLIArgumentParsingProvider>();
            });
            AzclipsServiceProvider.Initialize(
                new ClientMetadata(clientType, "00000000-0000-0000-0000-000000000000"));
        }

        public IReadOnlyList<string> ExecuteCli(string[] args, int? terminalWidth = null)
        {
            var globalArgs = GlobalArgParser.Parse(args);
            var dispatcher = BuildDispatcher();
            return ExecuteInvocation(dispatcher.Parse(globalArgs.Positional.ToArray()), globalArgs, terminalWidth);
        }

        public IReadOnlyList<string> ExecutePowerShell(string commandName, Dictionary<string, object> parameters) =>
            ExecuteInvocation(new CommandInvocation("Config", commandName, parameters, false, "Config", "0.0.0"),
                GlobalArgParser.Parse([]), null);

        private IReadOnlyList<string> ExecuteInvocation(CommandInvocation invocation, GlobalArgs globalArgs, int? terminalWidth)
        {
            _outputs.Clear();
            _warnings.Clear();
            var engine = AzclipsServiceProvider.GetRequiredService<IEngine>();
            var format = globalArgs.Output ?? OutputFormat.Json;
            var simpleFormat = globalArgs.Output ?? OutputFormat.Tsv;

            var streams = new OutputStreams(
                WriteOutput: (value, _) =>
                {
                    _outputs.Add(CliOutputFormatter.FormatValue(
                        value,
                        format,
                        simpleFormat,
                        colorPolicyAllowsColor: false,
                        globalArgs.Query,
                        terminalWidth));
                    return Task.CompletedTask;
                },
                WriteVerbose: (_, _) => Task.CompletedTask,
                WriteDebug: (_, _) => Task.CompletedTask,
                WriteWarning: (value, _) =>
                {
                    _warnings.Add(Convert.ToString(value) ?? string.Empty);
                    return Task.CompletedTask;
                },
                WriteError: (_, _) => Task.CompletedTask,
                WriteInformation: (_, _) => Task.CompletedTask);

            using ISession session = engine.OpenExecutionSession(invocation, streams, CancellationToken.None);
            session.Run();

            return _outputs.ToArray();
        }

        public void Dispose()
        {
            AzclipsServiceProvider.Dispose();

            foreach (var entry in _originalEnvironment)
            {
                Environment.SetEnvironmentVariable(entry.Key, entry.Value);
            }

            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static ExtensionDispatcher BuildDispatcher()
        {
            var registry = new ExtensionRegistry
            {
                Extensions =
                {
                    ["Microsoft.Azure.Azclips.Extensions.Config"] = new ExtensionRegistryEntry
                    {
                        Version = "0.0.0",
                        Contributions =
                        {
                            new Contribution { Verb = "config", Path = "" },
                            new Contribution { Verb = "config", Path = "get" },
                            new Contribution { Verb = "config", Path = "set" },
                            new Contribution { Verb = "config", Path = "delete" },
                            new Contribution { Verb = "config", Path = "list" },
                            new Contribution { Verb = "config", Path = "info" },
                        },
                    },
                },
            };
            var resolver = new ExtensionResolver(new InMemoryRegistryStore(registry));
            return new ExtensionDispatcher(resolver, () => LeafIndex.Build(registry));
        }
    }

    private sealed class BoolFlagProcessor : IProcessor
    {
        public bool All { get; set; }

        public IProcessor Init(ProcessorContext context) => this;

        public Task ProcessAsync() => Task.CompletedTask;
    }

    [Fact]
    public void PowerShellModule_ShouldDeclareFiveBinaryCmdlets()
    {
        // Resolve path from test assembly location
        var testAssemblyPath = typeof(ConfigCliTests).Assembly.Location;
        var repoRoot = Path.GetDirectoryName(testAssemblyPath);
        while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "azclips.slnx")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot);
        }

        Assert.False(string.IsNullOrEmpty(repoRoot), "Unable to locate repository root containing azclips.slnx from test assembly location.");

        var psModulePath = Path.Combine(repoRoot!, "src", "PS", "Config", "Config.psd1");
        Assert.True(File.Exists(psModulePath), $"PowerShell module manifest not found at {psModulePath}");

        // Verify binary cmdlet declarations in manifest
        var manifestContent = File.ReadAllText(psModulePath);

        var expectedCmdlets = new[]
        {
            "Get-AzConfig",
            "Set-AzConfig",
            "Remove-AzConfig",
            "Get-AzConfigList",
            "Get-AzConfigInfo"
        };

        foreach (var cmdlet in expectedCmdlets)
        {
            Assert.Contains(cmdlet, manifestContent);
        }

        Assert.Contains("Microsoft.Azure.Azclips.Cmdlets.Config.dll", manifestContent);
        Assert.Contains("FunctionsToExport = @()", manifestContent);
    }

    [Fact]
    public void PowerShellModule_ManifestShouldSpecifyAllRequiredDependencies()
    {
        // Resolve path from test assembly location
        var testAssemblyPath = typeof(ConfigCliTests).Assembly.Location;
        var repoRoot = Path.GetDirectoryName(testAssemblyPath);
        while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "azclips.slnx")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot);
        }

        Assert.False(string.IsNullOrEmpty(repoRoot), "Unable to locate repository root containing azclips.slnx from test assembly location.");

        var psModulePath = Path.Combine(repoRoot!, "src", "PS", "Config", "Config.psd1");
        Assert.True(File.Exists(psModulePath), $"PowerShell module manifest not found at {psModulePath}");

        var manifestContent = File.ReadAllText(psModulePath);

        // Verify key assembly dependencies are listed
        Assert.Contains("Core.Abstractions.dll", manifestContent);
        Assert.Contains("Core.Configuration.dll", manifestContent);
        Assert.Contains("Core.Engine.dll", manifestContent);
        Assert.Contains("Core.DependencyInjection.dll", manifestContent);
        Assert.Contains("Microsoft.Azure.Azclips.Extensions.Config.dll", manifestContent);

        // Verify cmdlets are exported
        Assert.Contains("Get-AzConfig", manifestContent);
        Assert.Contains("Set-AzConfig", manifestContent);
        Assert.Contains("Remove-AzConfig", manifestContent);
        Assert.Contains("Get-AzConfigList", manifestContent);
        Assert.Contains("Get-AzConfigInfo", manifestContent);
    }

    [Fact]
    public void ExtensionIndex_ShouldExposeTranslatedPowerShellParameters()
    {
        var extensionIndex = new Microsoft.Azure.Azclips.Extensions.Config.ExtensionIndex();

        var getMetadata = extensionIndex.GetCommandMetadata("Get-AzConfig", ClientType.PowerShell);
        Assert.DoesNotContain("Json", getMetadata.Parameters.Keys);

        var listMetadata = extensionIndex.GetCommandMetadata("Get-AzConfigList", ClientType.PowerShell);
        Assert.Contains("All", listMetadata.Parameters.Keys);
        Assert.DoesNotContain("Json", listMetadata.Parameters.Keys);
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
            return string.IsNullOrWhiteSpace(json) ? [] : JsonNode.Parse(json) as JsonObject ?? [];
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
