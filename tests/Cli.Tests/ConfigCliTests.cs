using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.AssemblyLoading;
using Core.Configuration.Abstractions;
using Core.Cli.Parser;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cirrus.Extensions.Config.Commands;
using Tests.Common.Stubs;

namespace Cli.Tests;

[Collection("Cirrus Service Provider Tests")]
public class ConfigCliTests
{
    [Fact]
    public void ExtensionDispatcher_ShouldRouteConfigCommandGroup()
    {
        var dispatcher = new ExtensionDispatcher();

        var invocation = dispatcher.Parse(["config", "list"]);

        Assert.Equal("Config", invocation.ExtensionName);
        // Shape B (§11.2): commandName is full-path form. Built-ins migrated in Phase 7 item 24.
        Assert.Equal("config list", invocation.CommandName);
    }

    [Fact]
    public void ArgumentParsingService_ShouldTreatBareBoolFlagsAsTrue()
    {
        var loggingService = new ConsoleLoggingService();
        var provider = new Core.Cli.CLIArgumentParsingProvider(new Core.Cli.PropertyParser(loggingService), loggingService);
        var parser = new Core.ArgumentParsing.ArgumentParsingService(provider);
        var processor = new BoolFlagProcessor();

        parser.BindArgumentsToProcessor(
            processor,
            new Dictionary<string, object> { ["all"] = new List<List<string>> { new() } },
            new Dictionary<string, ParameterMetadata>
            {
                ["all"] = new("All", typeof(bool), "Include defaults.", false, [], [])
            },
            parameterAliases: null);

        Assert.True(processor.All);
    }

    [Fact]
    public void ConfigCommands_ShouldSetGetListDeleteAndInfo()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var getOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        Assert.Contains("core.logging.level = debug [global config]", getOutput.Single());

        var listOutput = fixture.ExecuteCli(["config", "list", "--all"]);
        Assert.Contains("core.logging.level = debug [global config]", listOutput.Single());
        Assert.Contains("core.engine.extensionIndexCacheSize = 512 [default]", listOutput.Single());

        var infoOutput = fixture.ExecuteCli(["config", "info", "--keys-only"]);
        Assert.Contains("core.logging.level", infoOutput.Single());

        fixture.ExecuteCli(["config", "delete", "--key", "core.logging.level", "--force"]);

        var getAfterDeleteOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        Assert.Contains("core.logging.level = info [default]", getAfterDeleteOutput.Single());

    }

    [Fact]
    public void CliInvocation_ShouldSupportNamedConfigFlagsAcrossCrudFlow()
    {
        using var fixture = new ConfigCommandFixture();

        var setOutput = fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);
        Assert.Contains("Set core.logging.level = debug", setOutput.Single());

        var getOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        Assert.Contains("core.logging.level = debug [global config]", getOutput.Single());

        var setIntegerOutput = fixture.ExecuteCli(["config", "set", "--key", "core.http.maxRetries", "--value", "9", "--force"]);
        Assert.Contains("Set core.http.maxRetries = 9", setIntegerOutput.Single());

        var getIntegerOutput = fixture.ExecuteCli(["config", "get", "--key", "core.http.maxRetries"]);
        Assert.Contains("core.http.maxRetries = 9 [global config]", getIntegerOutput.Single());

        var listOutput = fixture.ExecuteCli(["config", "list"]);
        Assert.Contains("core.logging.level = debug [global config]", listOutput.Single());
        Assert.Contains("core.http.maxRetries = 9 [global config]", listOutput.Single());

        var deleteOutput = fixture.ExecuteCli(["config", "delete", "--key", "core.logging.level", "--force"]);
        Assert.Contains("Deleted core.logging.level", deleteOutput.Single());

        var getDeletedOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        Assert.Contains("core.logging.level = info [default]", getDeletedOutput.Single());
    }

    [Fact]
    public void CliInvocation_ShouldReturnJsonForListAndSchemaInfo()
    {
        using var fixture = new ConfigCommandFixture();

        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        var listJsonOutput = fixture.ExecuteCli(["config", "list", "--all", "--json"]);
        var listEntries = JsonNode.Parse(listJsonOutput.Single())!.AsArray();
        Assert.Contains(listEntries, entry => string.Equals(entry?["Key"]?.GetValue<string>(), "core.logging.level", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry?["Value"]?.GetValue<string>(), "debug", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry?["Source"]?.GetValue<string>(), "global config", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(listEntries, entry => string.Equals(entry?["Key"]?.GetValue<string>(), "core.engine.extensionIndexCacheSize", StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry?["Source"]?.GetValue<string>(), "default", StringComparison.OrdinalIgnoreCase));

        var infoOutput = fixture.ExecuteCli(["config", "info"]);
        var infoEntries = JsonNode.Parse(infoOutput.Single())!.AsArray();
        Assert.Contains(infoEntries, entry => string.Equals(entry?["Key"]?.GetValue<string>(), "core.logging.level", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CliInvocation_ShouldPreferEnvironmentOverGlobalConfig()
    {
        using var fixture = new ConfigCommandFixture();

        // Set global config value
        fixture.ExecuteCli(["config", "set", "--key", "core.logging.level", "--value", "debug", "--force"]);

        // Verify global config is set
        var globalOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
        Assert.Contains("core.logging.level = debug [global config]", globalOutput.Single());

        // Set environment variable (this persists for subsequent CLI invocations within the fixture)
        Environment.SetEnvironmentVariable("CIRRUS_LOG_LEVEL", "error");

        try
        {
            // Get value with environment override
            var envOutput = fixture.ExecuteCli(["config", "get", "--key", "core.logging.level"]);
            Assert.Contains("core.logging.level = error [environment]", envOutput.Single());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CIRRUS_LOG_LEVEL", null);
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
        Assert.Contains("defaults.group = my-rg [global config]", getOutput.Single());

        // Verify it appears in list
        var listOutput = fixture.ExecuteCli(["config", "list"]);
        Assert.Contains("defaults.group = my-rg [global config]", listOutput.Single());
    }

    [Fact]
    public void CliInvocation_ShouldRejectUnknownKey_WhenForceIsFalse()
    {
        using var fixture = new ConfigCommandFixture();

        // Attempt to set an unknown key without --force should throw
        var exception = Assert.Throws<ArgumentException>(() =>
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
        Assert.Contains("made.up.key = value [global config]", getOutput.Single());
    }

    private sealed class ConfigCommandFixture : IDisposable
    {
        private static readonly string[] s_environmentKeys =
        [
            "CIRRUS_LOG_LEVEL",
            "ZIPKIN_ENDPOINT",
            "PS_HTTP_MAX_RETRIES",
            "AZURE_PS_HTTP_MAX_RETRIES",
            "PS_HTTP_MAX_RETRIES_FOR_429",
            "AZURE_PS_HTTP_MAX_RETRIES_FOR_429"
        ];

        private readonly string _configPath;
        private readonly List<string> _outputs = [];
        private readonly Dictionary<string, string?> _originalEnvironment = new(StringComparer.OrdinalIgnoreCase);

        public ConfigCommandFixture()
        {
            var directory = Path.Combine(Path.GetTempPath(), "cirrus-config-cli-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _configPath = Path.Combine(directory, "config.json");

            foreach (var key in s_environmentKeys)
            {
                _originalEnvironment[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, null);
            }

            CirrusServiceProvider.Dispose();
            CirrusServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider>(new StubExtensionLoader(typeof(SetConfigCommand).Assembly, "Config"));
                services.AddSingleton<IConfigurationProvider>(new TestConfigurationProvider(_configPath));
                services.AddSingleton<ITelemetryService, StubTelemetryService>();
                services.AddSingleton<IAuthContextService, StubAuthContextService>();
                services.AddSingleton<IAuthService, StubAuthService>();
                // Register CLI-specific services for argument parsing
                services.AddSingleton<Core.Cli.PropertyParser>();
                services.AddSingleton<Core.Abstractions.IArgumentParsingProvider, Core.Cli.CLIArgumentParsingProvider>();
            });
            CirrusServiceProvider.Initialize(ClientType.CLI);
        }

        public IReadOnlyList<string> ExecuteCli(string[] args)
        {
            _outputs.Clear();

            var dispatcher = new ExtensionDispatcher();
            var invocation = dispatcher.Parse(args);
            var engine = CirrusServiceProvider.GetRequiredService<IEngine>();

            var streams = new OutputStreams(
                WriteOutput: (value, _) =>
                {
                    _outputs.Add(Convert.ToString(value) ?? string.Empty);
                    return Task.CompletedTask;
                },
                WriteVerbose: (_, _) => Task.CompletedTask,
                WriteDebug: (_, _) => Task.CompletedTask,
                WriteWarning: (_, _) => Task.CompletedTask,
                WriteError: (_, _) => Task.CompletedTask,
                WriteInformation: (_, _) => Task.CompletedTask);

            using ISession session = engine.OpenExecutionSession(invocation, streams, CancellationToken.None);
            session.Run();

            return _outputs.ToArray();
        }

        public void Dispose()
        {
            CirrusServiceProvider.Dispose();

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
        while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "cirrus.slnx")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot);
        }

        Assert.False(string.IsNullOrEmpty(repoRoot), "Unable to locate repository root containing cirrus.slnx from test assembly location.");

        var psModulePath = Path.Combine(repoRoot!, "src", "PS", "Config", "Config.psd1");
        Assert.True(File.Exists(psModulePath), $"PowerShell module manifest not found at {psModulePath}");

        // Verify binary cmdlet declarations in manifest
        var manifestContent = File.ReadAllText(psModulePath);

        var expectedCmdlets = new[]
        {
            "Get-CirrusConfig",
            "Set-CirrusConfig",
            "Remove-CirrusConfig",
            "Get-CirrusConfigList",
            "Get-CirrusConfigInfo"
        };

        foreach (var cmdlet in expectedCmdlets)
        {
            Assert.Contains(cmdlet, manifestContent);
        }

        Assert.Contains("Microsoft.Azure.Cirrus.Cmdlets.Config.dll", manifestContent);
        Assert.Contains("FunctionsToExport = @()", manifestContent);
    }

    [Fact]
    public void PowerShellModule_ManifestShouldSpecifyAllRequiredDependencies()
    {
        // Resolve path from test assembly location
        var testAssemblyPath = typeof(ConfigCliTests).Assembly.Location;
        var repoRoot = Path.GetDirectoryName(testAssemblyPath);
        while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "cirrus.slnx")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot);
        }

        Assert.False(string.IsNullOrEmpty(repoRoot), "Unable to locate repository root containing cirrus.slnx from test assembly location.");

        var psModulePath = Path.Combine(repoRoot!, "src", "PS", "Config", "Config.psd1");
        Assert.True(File.Exists(psModulePath), $"PowerShell module manifest not found at {psModulePath}");

        var manifestContent = File.ReadAllText(psModulePath);

        // Verify key assembly dependencies are listed
        Assert.Contains("Core.Abstractions.dll", manifestContent);
        Assert.Contains("Core.Configuration.dll", manifestContent);
        Assert.Contains("Core.Engine.dll", manifestContent);
        Assert.Contains("Core.DependencyInjection.dll", manifestContent);
        Assert.Contains("Microsoft.Azure.Cirrus.Extensions.Config.dll", manifestContent);

        // Verify cmdlets are exported
        Assert.Contains("Get-CirrusConfig", manifestContent);
        Assert.Contains("Set-CirrusConfig", manifestContent);
        Assert.Contains("Remove-CirrusConfig", manifestContent);
        Assert.Contains("Get-CirrusConfigList", manifestContent);
        Assert.Contains("Get-CirrusConfigInfo", manifestContent);
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
