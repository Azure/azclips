using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
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
        Assert.Contains("core.logging.level = debug [global config]", getOutput.Single());

        var listCommand = new ListConfigCommand { All = true };
        var listOutput = await fixture.ExecuteAsync(listCommand);
        Assert.Contains("core.logging.level = debug [global config]", listOutput.Single());
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
                services.AddSingleton<IExtensionLoader>(new StubExtensionLoader(typeof(SetConfigCommand).Assembly, "Config"));
                services.AddSingleton<ITelemetryService, StubTelemetryService>();
                services.AddSingleton<IAuthContextService, StubAuthContextService>();
                services.AddSingleton<IAuthService, StubAuthService>();
            };
            CirrusServiceProvider.Initialize(ClientType.CLI);
        }

        public IReadOnlyList<string> ExecuteCli(string[] args)
        {
            _outputs.Clear();

            var dispatcher = new ExtensionDispatcher();
            var invocation = dispatcher.Parse(args);
            var engine = CirrusServiceProvider.GetRequiredService<IEngine>();

            engine.Process(invocation, (value, _) =>
            {
                _outputs.Add(Convert.ToString(value) ?? string.Empty);
                return Task.CompletedTask;
            });

            return _outputs.ToArray();
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

    [Fact]
    public void PowerShellModule_ShouldImportAndExportFiveCmdlets()
    {
        // Resolve path from test assembly location
        var testAssemblyPath = typeof(ConfigCliTests).Assembly.Location;
        var repoRoot = Path.GetDirectoryName(testAssemblyPath);
        while (!string.IsNullOrEmpty(repoRoot) && !File.Exists(Path.Combine(repoRoot, "cirrus.slnx")))
        {
            repoRoot = Path.GetDirectoryName(repoRoot);
        }
        var psModulePath = Path.Combine(repoRoot!, "src", "PS", "Config", "Config.psd1");
        Assert.True(File.Exists(psModulePath), $"PowerShell module manifest not found at {psModulePath}");

        // Verify module file structure
        var moduleDir = Path.GetDirectoryName(psModulePath)!;
        var exportsDir = Path.Combine(moduleDir, "exports");
        Assert.True(Directory.Exists(exportsDir), "Module exports directory should exist");

        var expectedCmdlets = new[]
        {
            "Get-CirrusConfig.ps1",
            "Set-CirrusConfig.ps1",
            "Remove-CirrusConfig.ps1",
            "Get-CirrusConfigList.ps1",
            "Get-CirrusConfigInfo.ps1"
        };

        foreach (var cmdlet in expectedCmdlets)
        {
            var cmdletPath = Path.Combine(exportsDir, cmdlet);
            Assert.True(File.Exists(cmdletPath), $"Cmdlet file should exist: {cmdlet}");
            var content = File.ReadAllText(cmdletPath);
            Assert.Contains("function ", content);
            Assert.Contains("param (", content);
        }
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
        var psModulePath = Path.Combine(repoRoot!, "src", "PS", "Config", "Config.psd1");
        Assert.True(File.Exists(psModulePath), $"PowerShell module manifest not found at {psModulePath}");
        
        var manifestContent = File.ReadAllText(psModulePath);

        // Verify key assembly dependencies are listed
        Assert.Contains("Core.Abstractions.dll", manifestContent);
        Assert.Contains("Core.Configuration.dll", manifestContent);
        Assert.Contains("Core.Engine.dll", manifestContent);
        Assert.Contains("Core.DependencyInjection.dll", manifestContent);
        Assert.Contains("Microsoft.Azure.Cirrus.Extensions.Config.dll", manifestContent);

        // Verify functions are exported
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