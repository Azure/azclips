// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Version;
using Core.Configuration.Options;
using Xunit;

namespace Cli.Tests;

/// <summary>
/// Offline, deterministic tests for the host-side version cold-path that replaced the
/// <c>Extensions.Version</c> processor. <see cref="VersionInformationProvider"/> gathers the same
/// data (CLI version, runtime, paths) as before; <see cref="RuntimeVersionDispatcher"/> renders it
/// as JSON (for <c>az version</c>) or text (for <c>az --version</c>). Path resolution is driven by
/// an injected <see cref="IConfigurationService"/>, so no DI container is required.
/// </summary>
public class VersionInformationProviderTests
{
    private static readonly string ConfigFilePath =
        Path.Combine(Path.GetTempPath(), "azclips-version-tests", Guid.NewGuid().ToString("N"), "config.json");

    /// <summary>The CLI version string is read from the entry/fallback assembly and is never blank.</summary>
    [Fact]
    public void Gather_ShouldPopulateCliVersion()
    {
        var info = VersionInformationProvider.Gather(new FakeConfigurationService(ConfigFilePath));

        Assert.False(string.IsNullOrWhiteSpace(info.AzCli));
    }

    /// <summary>Runtime details come from cross-platform RuntimeInformation APIs and are always present.</summary>
    [Fact]
    public void Gather_ShouldPopulateRuntimeInformation()
    {
        var info = VersionInformationProvider.Gather(new FakeConfigurationService(ConfigFilePath));

        Assert.False(string.IsNullOrWhiteSpace(info.Runtime.DotNet));
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime.Os));
        Assert.False(string.IsNullOrWhiteSpace(info.Runtime.Architecture));
    }

    /// <summary>The reported config path is the directory of the configuration service's file path.</summary>
    [Fact]
    public void Gather_ShouldReportConfigDirectory_FromConfigurationService()
    {
        var info = VersionInformationProvider.Gather(new FakeConfigurationService(ConfigFilePath));

        Assert.Equal(Path.GetDirectoryName(ConfigFilePath), info.Paths.ConfigLocation);
    }

    /// <summary>
    /// The reported extensions path is the versioned installation directory the extension loaders use,
    /// read from <see cref="AssemblyLoadingOptions"/> so it honors user config overrides.
    /// </summary>
    [Fact]
    public void Gather_ShouldReportExtensionsDirectory_FromAssemblyLoadingOptions()
    {
        var extensionsDir = Path.Combine(Path.GetTempPath(), "azclips-extensions", Guid.NewGuid().ToString("N"));
        var options = new AssemblyLoadingOptions { InstalledExtensionsRoot = extensionsDir };

        var info = VersionInformationProvider.Gather(new FakeConfigurationService(ConfigFilePath, options));

        Assert.Equal(extensionsDir, info.Paths.ExtensionsLocation);
    }

    /// <summary>Gathering without a configuration service surfaces the error instead of masking it.</summary>
    [Fact]
    public void Gather_ShouldThrow_WhenConfigurationServiceIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => VersionInformationProvider.Gather(null!));
    }

    /// <summary>
    /// JSON mode (<c>az version</c>) serializes the structured object with camelCase property names,
    /// matching how the old command's output was formatted.
    /// </summary>
    [Fact]
    public void Render_Json_ShouldWriteStructuredCamelCasePayload()
    {
        using var writer = new StringWriter();
        var dispatcher = new RuntimeVersionDispatcher(new FakeConfigurationService(ConfigFilePath), writer);

        var exitCode = dispatcher.Render(VersionOutputFormat.Json);

        var output = writer.ToString();
        Assert.Equal(0, exitCode);
        Assert.Contains("\"azCli\"", output);
        Assert.Contains("\"runtime\"", output);
        Assert.Contains("\"paths\"", output);
    }

    [Fact]
    public void Render_Json_ShouldApplyQuery_WhenProvided()
    {
        using var writer = new StringWriter();
        var dispatcher = new RuntimeVersionDispatcher(new FakeConfigurationService(ConfigFilePath), writer);

        var exitCode = dispatcher.Render(VersionOutputFormat.Json, "azCli");

        var output = writer.ToString();
        Assert.Equal(0, exitCode);
        Assert.Equal("\"" + VersionInformationProvider.Gather(new FakeConfigurationService(ConfigFilePath)).AzCli + "\"", output.Trim());
    }

    /// <summary>
    /// Text mode (<c>az --version</c>) renders a human-readable layout led by the CLI version and the
    /// runtime/paths sections — the same information as JSON, different presentation.
    /// </summary>
    [Fact]
    public void Render_Text_ShouldWriteHumanReadableLayout()
    {
        using var writer = new StringWriter();
        var dispatcher = new RuntimeVersionDispatcher(new FakeConfigurationService(ConfigFilePath), writer);

        var exitCode = dispatcher.Render(VersionOutputFormat.Text);

        var output = writer.ToString();
        Assert.Equal(0, exitCode);
        Assert.StartsWith("AzCli ", output);
        Assert.Contains("Runtime", output);
        Assert.Contains("Paths", output);
    }

    /// <summary>
    /// Render writes the formatted payload with exactly one trailing newline.
    /// </summary>
    [Fact]
    public void Render_Text_ShouldEndWithExactlyOneTrailingNewline()
    {
        using var writer = new StringWriter();
        var dispatcher = new RuntimeVersionDispatcher(new FakeConfigurationService(ConfigFilePath), writer);

        dispatcher.Render(VersionOutputFormat.Text);

        var output = writer.ToString();
        Assert.EndsWith(Environment.NewLine, output);

        // After the single terminating newline, the content must not end with another line break.
        var withoutFinalNewline = output[..^Environment.NewLine.Length];
        Assert.False(
            withoutFinalNewline.EndsWith('\n') || withoutFinalNewline.EndsWith('\r'),
            "Version text output should end with exactly one newline, not a trailing blank line.");
    }

    // Minimal IConfigurationService whose only meaningful behavior is returning a fixed config file
    // path; the provider only calls GetConfigFilePath().
    private sealed class FakeConfigurationService(string configFilePath, AssemblyLoadingOptions? assemblyLoadingOptions = null) : IConfigurationService
    {
        public string GetConfigFilePath() => configFilePath;

        public T GetConfiguration<T>(string? key = null, T? defaultValue = null) where T : class, new()
        {
            if (typeof(T) == typeof(AssemblyLoadingOptions) && assemblyLoadingOptions is not null)
            {
                return (T)(object)assemblyLoadingOptions;
            }

            return defaultValue ?? new T();
        }

        public Task SetConfigurationAsync(string key, object? value, bool force = false, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteConfigurationAsync(string key, bool force = false, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<Dictionary<string, object?>> ListConfigurationAsync(bool includeDefaults = false, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<object?> ResolveDefaultParameterAsync(string name, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IConfigurationSchema GetSchema() => throw new NotImplementedException();
    }
}
