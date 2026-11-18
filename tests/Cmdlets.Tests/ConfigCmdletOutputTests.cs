// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

extern alias CmdletsConfig;

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Text.Json.Nodes;
using Core.Abstractions;
using Core.Configuration.Abstractions;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Cmdlets.Accounts;
using Microsoft.Azure.Azclips.Extensions.Config.Commands;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Tests.Common.Stubs;
using GetAzConfigCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigCmdlet;
using GetAzConfigInfoCmdlet = CmdletsConfig::Microsoft.Azure.Azclips.Cmdlets.Config.GetAzConfigInfoCmdlet;

namespace Cmdlets.Tests;

[Collection("Azclips Service Provider Tests")]
public class ConfigCmdletOutputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void GetConfig_ShouldListDefaultsUnlessConfiguredOnly(bool? configuredOnly)
    {
        using var fixture = new ConfigRunspace();
        fixture.Shell.AddCommand("Get-AzConfig");
        if (configuredOnly.HasValue)
        {
            fixture.Shell.AddParameter("ConfiguredOnly", new SwitchParameter(configuredOnly.Value));
        }

        var output = fixture.Shell.Invoke();

        Assert.Empty(fixture.Shell.Streams.Error);
        Assert.Equal(configuredOnly == true ? 2 : 3, output.Count);
        Assert.Contains(output, entry => Equals(entry.Properties["Key"].Value, "core.logging.level")
            && Equals(entry.Properties["Value"].Value, "error")
            && Equals(entry.Properties["Source"].Value, "environment"));
        Assert.Contains(output, entry => Equals(entry.Properties["Key"].Value, "telemetry.enabled")
            && Equals(entry.Properties["Value"].Value, true)
            && Equals(entry.Properties["Source"].Value, "global config"));
        Assert.Equal(configuredOnly != true, output.Any(entry => Equals(entry.Properties["Source"].Value, "default")));
        var keys = output.Select(entry => (string)entry.Properties["Key"].Value).ToArray();
        Assert.Equal(keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase), keys);
    }

    [Theory]
    [InlineData("core.logging.level", "core.logging.level")]
    [InlineData("core.logging", "level")]
    public void GetConfig_ShouldPreserveKeyAndSectionReads(string key, string expectedKey)
    {
        using var fixture = new ConfigRunspace();
        fixture.Shell.AddCommand("Get-AzConfig").AddParameter("Key", key);

        var entry = Assert.Single(fixture.Shell.Invoke());

        Assert.Empty(fixture.Shell.Streams.Error);
        Assert.Equal(expectedKey, entry.Properties["Key"].Value);
        Assert.Equal("error", entry.Properties["Value"].Value);
        Assert.Equal("environment", entry.Properties["Source"].Value);
    }

    [Fact]
    public void GetConfig_ShouldRejectCombiningKeyAndConfiguredOnly()
    {
        using var fixture = new ConfigRunspace();
        fixture.Shell.AddCommand("Get-AzConfig")
            .AddParameter("Key", "core")
            .AddParameter("ConfiguredOnly");

        Assert.Throws<ParameterBindingException>(() => fixture.Shell.Invoke());
    }

    [Theory]
    [InlineData(typeof(GetAzConfigCmdlet))]
    [InlineData(typeof(GetAzConfigInfoCmdlet))]
    public void ReadCmdlets_ShouldEnumerateStructuredOutput(Type cmdletType)
    {
        var cmdlet = Activator.CreateInstance(cmdletType);
        var enumerateOutput = cmdletType.GetProperty(
            "EnumerateOutput",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(enumerateOutput);
        Assert.True((bool)enumerateOutput.GetValue(cmdlet)!);
    }

    [Fact]
    public void GetConfig_ShouldNotExposeAsJson()
    {
        Assert.Null(typeof(GetAzConfigCmdlet).GetProperty("AsJson"));
    }

    [Fact]
    public void GetConfigCmdlets_ShouldDeclareStructuredOutputTypes()
    {
        AssertOutputType<GetAzConfigCmdlet>("Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigEntry");
        AssertOutputType<GetAzConfigInfoCmdlet>(
            "Microsoft.Azure.Azclips.Extensions.Config.Models.ConfigKeyInfo",
            typeof(string).FullName!);
    }

    private static void AssertOutputType<TCmdlet>(params string[] expectedTypeNames)
    {
        var outputType = typeof(TCmdlet).GetCustomAttribute<OutputTypeAttribute>();

        Assert.NotNull(outputType);
        Assert.Equal(expectedTypeNames, outputType.Type.Select(type => type.Name));
    }

    private sealed class ConfigRunspace : IDisposable
    {
        private readonly Runspace _runspace;
        public PowerShell Shell { get; }

        public ConfigRunspace()
        {
            var source = new Mock<ISourceAwareConfigurationService>();
            source.Setup(service => service.ListConfigurationWithSourcesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, ConfigurationValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["telemetry.enabled"] = new("telemetry.enabled", true, "global config"),
                    ["core.logging.level"] = new("core.logging.level", "error", "environment"),
                    ["core.http.maxRetries"] = new("core.http.maxRetries", 3, "default")
                });
            var provider = new Mock<IConfigurationProvider>();
            provider.Setup(service => service.GetConfigFilePath())
                .Returns(Path.Combine(Path.GetTempPath(), $"azclips-config-{Guid.NewGuid():N}.json"));
            provider.Setup(service => service.ReadAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JsonObject());

            AzclipsServiceProvider.Dispose();
            AzclipsServiceProvider.OverrideRegistration(services =>
            {
                services.AddSingleton<IAssemblyLoadingProvider>(new StubExtensionLoader(typeof(GetConfigCommand).Assembly, "Config"));
                services.AddSingleton(provider.Object);
                services.AddSingleton(source.Object);
                services.AddSingleton<ITelemetryService, StubTelemetryService>();
                services.AddSingleton<IProfileService, StubProfileService>();
                services.AddSingleton<IAuthService, StubAuthService>();
                services.AddSingleton<IArgumentParsingProvider, PSArgumentParsingProvider>();
            });
            AzclipsServiceProvider.Initialize(
                new ClientMetadata(ClientType.PowerShell, "00000000-0000-0000-0000-000000000000"));

            var state = InitialSessionState.CreateDefault2();
            state.Commands.Add(new SessionStateCmdletEntry("Get-AzConfig", typeof(GetAzConfigCmdlet), null));
            _runspace = RunspaceFactory.CreateRunspace(state);
            _runspace.Open();
            Shell = PowerShell.Create();
            Shell.Runspace = _runspace;
        }

        public void Dispose()
        {
            Shell.Dispose();
            _runspace.Dispose();
            AzclipsServiceProvider.Dispose();
        }
    }
}
