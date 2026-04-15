using System.Diagnostics;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Configuration.Abstractions;
using Core.Cli.Parser;
using Core.DependencyInjection;
using Core.Runtime;
using Core.Runtime.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cirrus.Extensions.Config.Commands;

namespace Cli.Tests;

public class ConfigCliTests
{
    [Fact]
    public void ExtensionDispatcher_ShouldRouteConfigCommandGroup()
    {
        var dispatcher = new ExtensionDispatcher();

        var invocation = dispatcher.Parse(["config", "list"]);

        Assert.Equal("Config", invocation.ExtensionName);
        Assert.Equal("list", invocation.CommandName);
    }

    [Fact]
    public void ArgumentParsingService_ShouldTreatBareBoolFlagsAsTrue()
    {
        var loggingService = new ConsoleLoggingService();
        var parser = new Core.ArgumentParsing.ArgumentParsingService(new Core.ArgumentParsing.PropertyParser(loggingService), loggingService);

        var parsedArguments = parser.ParseArguments(
            new Dictionary<string, object> { ["all"] = new List<List<string>> { new() } },
            new Dictionary<string, ParameterMetadata>
            {
                ["all"] = new("All", typeof(bool), "Include defaults.", false, [], [])
            },
            parameterAliases: null);

        Assert.True((bool)parsedArguments["All"]);
    }

    [Fact]
    public async Task ConfigCommands_ShouldSetGetListDeleteAndInfo()
    {
        using var fixture = new ConfigCommandFixture();

        var setCommand = new SetConfigCommand { Key = "core.logging.level", Value = "debug" };
        await fixture.ExecuteAsync(setCommand);

        var getCommand = new GetConfigCommand { Key = "core.logging.level" };
        var getOutput = await fixture.ExecuteAsync(getCommand);
        Assert.Contains("core.logging.level = debug [config file]", getOutput.Single());

        var listCommand = new ListConfigCommand { All = true };
        var listOutput = await fixture.ExecuteAsync(listCommand);
        Assert.Contains("core.logging.level = debug [config file]", listOutput.Single());
        Assert.Contains("core.engine.extensionIndexCacheSize = 512 [default]", listOutput.Single());

        var infoCommand = new InfoConfigCommand { KeysOnly = true };
        var infoOutput = await fixture.ExecuteAsync(infoCommand);
        Assert.Contains("core.logging.level", infoOutput.Single());

        var deleteCommand = new DeleteConfigCommand { Key = "core.logging.level" };
        await fixture.ExecuteAsync(deleteCommand);

        var getAfterDeleteCommand = new GetConfigCommand { Key = "core.logging.level" };
        var getAfterDeleteOutput = await fixture.ExecuteAsync(getAfterDeleteCommand);
        Assert.Contains("core.logging.level = info [default]", getAfterDeleteOutput.Single());
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
            CirrusServiceProvider.PrependRegistration = null;
            CirrusServiceProvider.OverrideRegistration = services =>
            {
                services.AddSingleton<IConfigurationProvider>(new TestConfigurationProvider(_configPath));
            };
            CirrusServiceProvider.Initialize(ClientType.CLI);
        }

        public async Task<IReadOnlyList<string>> ExecuteAsync(IProcessor command)
        {
            _outputs.Clear();
            command.Init(new CommandContext
            {
                CommandAuthHandler = new CommandAuthHandler
                {
                    AuthContext = new AuthContext(string.Empty, string.Empty),
                    AcquireTokenAsyncDefault = (_, _, _, _) => Task.FromResult(new AuthResult
                    {
                        AccessToken = string.Empty,
                        ExpiresOn = DateTimeOffset.MinValue,
                        Scopes = Array.Empty<string>(),
                        TenantId = string.Empty,
                        CorrelationId = Guid.Empty
                    })
                },
                CommandOutputHandler = new CommandOutputHandler
                {
                    WriteOutput = (value, _) =>
                    {
                        _outputs.Add(Convert.ToString(value) ?? string.Empty);
                        return Task.CompletedTask;
                    }
                },
                CommandTelemetryHandler = new CommandTelemetryHandler
                {
                    StartActivity = (name, kind) => new Activity(name)
                },
                HttpPipeline = new HttpPipeline()
            });

            await command.ProcessAsync();
            return _outputs.ToArray();
        }

        public void Dispose()
        {
            CirrusServiceProvider.Dispose();
            CirrusServiceProvider.PrependRegistration = null;
            CirrusServiceProvider.OverrideRegistration = null;

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