// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Configuration.Options;
using Core.Abstractions;
using Moq;

namespace Cli.Tests;

public class OutputFormatPolicyTests
{
    [Theory]
    [InlineData("json", true, OutputFormat.Json)]
    [InlineData("jsonc", true, OutputFormat.Jsonc)]
    [InlineData("table", true, OutputFormat.Table)]
    [InlineData("tsv", true, OutputFormat.Tsv)]
    [InlineData("yaml", true, OutputFormat.Yaml)]
    [InlineData("yamlc", true, OutputFormat.Yamlc)]
    [InlineData("JSON", true, OutputFormat.Json)]
    [InlineData("plain", false, default(OutputFormat))]
    [InlineData("xml", false, default(OutputFormat))]
    [InlineData(null, false, default(OutputFormat))]
    [InlineData("", false, default(OutputFormat))]
    public void TryParse_ShouldReturnExpectedResult(string? value, bool expectedSuccess, OutputFormat expectedFormat)
    {
        var success = OutputFormatPolicy.TryParse(value, out var format);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedFormat, format);
        }
    }

    [Fact]
    public void ResolveEffectiveFormat_ShouldPreferFlagOverride_WhenGiven()
    {
        var options = new ClientsOptions { Cli = new CliClientOptions { Output = new CliOutputOptions { Format = "yaml" } } };

        var effective = OutputFormatPolicy.ResolveEffectiveFormat(options, OutputFormat.Json);

        Assert.Equal(OutputFormat.Json, effective);
    }

    [Fact]
    public void ResolveEffectiveFormat_ShouldFallBackToConfig_WhenFlagNotGiven()
    {
        var options = new ClientsOptions { Cli = new CliClientOptions { Output = new CliOutputOptions { Format = "yaml" } } };

        var effective = OutputFormatPolicy.ResolveEffectiveFormat(options, flagOverride: null);

        Assert.Equal(OutputFormat.Yaml, effective);
    }

    [Fact]
    public void ResolveEffectiveFormat_ShouldFallBackToJson_WhenConfigValueIsInvalid()
    {
        var options = new ClientsOptions { Cli = new CliClientOptions { Output = new CliOutputOptions { Format = "not-a-format" } } };

        var effective = OutputFormatPolicy.ResolveEffectiveFormat(options, flagOverride: null);

        Assert.Equal(OutputFormat.Json, effective);
    }

    [Fact]
    public void ResolveEffectiveSimpleFormat_ShouldPreferFlagOverride_WhenGiven()
    {
        var options = new ClientsOptions { Cli = new CliClientOptions { Output = new CliOutputOptions { SimpleFormat = "yaml" } } };

        var effective = OutputFormatPolicy.ResolveEffectiveSimpleFormat(options, OutputFormat.Json);

        Assert.Equal(OutputFormat.Json, effective);
    }

    [Fact]
    public void ResolveEffectiveSimpleFormat_ShouldFallBackToConfig_WhenFlagNotGiven()
    {
        var options = new ClientsOptions { Cli = new CliClientOptions { Output = new CliOutputOptions { SimpleFormat = "yaml" } } };

        var effective = OutputFormatPolicy.ResolveEffectiveSimpleFormat(options, flagOverride: null);

        Assert.Equal(OutputFormat.Yaml, effective);
    }

    [Fact]
    public void ResolveEffectiveSimpleFormat_ShouldFallBackToTsv_WhenConfigValueIsInvalid()
    {
        var options = new ClientsOptions { Cli = new CliClientOptions { Output = new CliOutputOptions { SimpleFormat = "not-a-format" } } };

        var effective = OutputFormatPolicy.ResolveEffectiveSimpleFormat(options, flagOverride: null);

        Assert.Equal(OutputFormat.Tsv, effective);
    }

    [Fact]
    public void ResolveFormatSource_FlagThenUserConfigThenDefault()
    {
        var options = new ClientsOptions();
        Assert.Equal(OutputFormatSource.Default, OutputFormatPolicy.ResolveFormatSource(false, options, null));
        Assert.Equal(OutputFormatSource.UserConfig, OutputFormatPolicy.ResolveFormatSource(true, options, null));
        Assert.Equal(OutputFormatSource.Flag, OutputFormatPolicy.ResolveFormatSource(true, options, OutputFormat.Yaml));
        options.Cli.Output.Format = "invalid";
        Assert.Equal(OutputFormatSource.Default, OutputFormatPolicy.ResolveFormatSource(true, options, null));
        Assert.Equal(OutputFormatSource.Flag, OutputFormatPolicy.ResolveFormatSource(false, options, OutputFormat.Json));
    }

    [Theory]
    [InlineData("CLIENTS:CLI:OUTPUT:FORMAT", false)]
    [InlineData("CLIENTS.CLI.OUTPUT.FORMAT", false)]
    [InlineData("CLIENTS:CLI:OUTPUT:FORMAT", true)]
    [InlineData("CLIENTS.CLI.OUTPUT.FORMAT", true)]
    public void IsFormatUserSet_NormalizesSnapshotKeys_WithOrWithoutSourceSupport(string key, bool sourceAware)
    {
        var service = new Mock<IConfigurationService>();
        if (sourceAware)
        {
            service.As<ISourceAwareConfigurationService>()
                .Setup(s => s.ListConfigurationWithSourcesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, ConfigurationValue>
                {
                    [key] = new(key, "yaml", "global config")
                });
        }
        else
        {
            service.Setup(s => s.ListConfigurationAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object?> { [key] = "yaml" });
        }

        Assert.True(OutputFormatPolicy.IsFormatUserSet(service.Object));
    }
}
