// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Cli.Output;
using Core.Cli.Parser;
using Core.Cli.Help;
using Core.Runtime;

namespace Cli.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ColorPolicy"/> — precedence between env vars,
/// <c>core.output.color</c> config, <c>TERM=dumb</c>, and target-stream redirection.
///
/// Uses the non-parallel test collection because both <see cref="ColorPolicy.Initialize"/>
/// and env-var mutation touch process-wide state.
/// </summary>
[Collection("Azclips Service Provider Tests")]
public class ColorPolicyTests
{
    [Theory]
    [InlineData("auto",    ColorMode.Auto)]
    [InlineData("always",  ColorMode.Always)]
    [InlineData("never",   ColorMode.Never)]
    [InlineData("AUTO",    ColorMode.Auto)]
    [InlineData("Always",  ColorMode.Always)]
    [InlineData("  never", ColorMode.Never)]
    [InlineData(null,      ColorMode.Auto)]
    [InlineData("",        ColorMode.Auto)]
    [InlineData("invalid", ColorMode.Auto)]
    public void Parse_MapsConfigValueToColorMode(string? configuredValue, ColorMode expected)
    {
        Assert.Equal(expected, ColorPolicy.Parse(configuredValue));
    }

    [Fact]
    public void NoColorEnv_BeatsEverything_IncludingModeAlwaysAndForceColor()
    {
        using var _ = new EnvScope(("NO_COLOR", "1"), ("FORCE_COLOR", "1"));
        using var __ = new ModeScope(ColorMode.Always);

        Assert.False(ColorPolicy.ShouldEmitColor(targetIsRedirected: false));
    }

    [Fact]
    public void ForceColorEnv_BeatsModeNever_AndBeatsTermDumb_AndBeatsRedirection()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", "1"), ("TERM", "dumb"));
        using var __ = new ModeScope(ColorMode.Never);

        Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
    }

    [Fact]
    public void ModeAlways_ForcesColor_EvenWhenRedirected()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", null), ("TERM", null));
        using var __ = new ModeScope(ColorMode.Always);

        Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
    }

    [Fact]
    public void ModeNever_SuppressesColor_EvenOnLiveTerminal()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", null), ("TERM", null));
        using var __ = new ModeScope(ColorMode.Never);

        Assert.False(ColorPolicy.ShouldEmitColor(targetIsRedirected: false));
    }

    [Fact]
    public void ModeAuto_FallsThroughToTermDumbCheck()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", null), ("TERM", "dumb"));
        using var __ = new ModeScope(ColorMode.Auto);

        Assert.False(ColorPolicy.ShouldEmitColor(targetIsRedirected: false));
    }

    [Fact]
    public void ModeAuto_FallsThroughToRedirectionCheck()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", null), ("TERM", null));
        using var __ = new ModeScope(ColorMode.Auto);

        Assert.False(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
    }

    [Fact]
    public void ModeAuto_LiveTerminalNoEnv_EmitsColor()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", null), ("TERM", null));
        using var __ = new ModeScope(ColorMode.Auto);

        Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: false));
    }

    [Fact]
    public void ModeAlways_ColorsJsonc_ButNeverJson()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", null), ("TERM", null));
        using var __ = new ModeScope(ColorMode.Always);
        var allowsColor = ColorPolicy.ShouldEmitColor(targetIsRedirected: false);
        var value = new { Name = "rg1" };

        Assert.Contains('\u001b', CliOutputFormatter.FormatValue(value, OutputFormat.Jsonc, allowsColor));
        Assert.DoesNotContain('\u001b', CliOutputFormatter.FormatValue(value, OutputFormat.Json, allowsColor));
    }

    [Fact]
    public void NoColorEnv_RendersJsoncAsPlainJson_EvenWhenModeAlways()
    {
        using var _ = new EnvScope(("NO_COLOR", "1"), ("FORCE_COLOR", null), ("TERM", null));
        using var __ = new ModeScope(ColorMode.Always);
        var allowsColor = ColorPolicy.ShouldEmitColor(targetIsRedirected: false);
        var value = new { Name = "rg1" };

        Assert.Equal(
            CliOutputFormatter.FormatValue(value, OutputFormat.Json, allowsColor),
            CliOutputFormatter.FormatValue(value, OutputFormat.Jsonc, allowsColor));
    }

    [Theory]
    [InlineData(ColorMode.Auto, false)]
    [InlineData(ColorMode.Auto, true)]
    [InlineData(ColorMode.Always, false)]
    [InlineData(ColorMode.Always, true)]
    [InlineData(ColorMode.Never, false)]
    [InlineData(ColorMode.Never, true)]
    public void ConsoleFailure_SuppressesStyling_EvenWhenForced(ColorMode mode, bool forceColor)
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", forceColor ? "1" : null), ("TERM", null));
        using var __ = new ModeScope(mode);
        using var errors = new StringWriter();
        var supportsColor = WindowsConsole.Initialize(false, false, _ => 5, errors);

        ColorPolicy.Initialize(mode, supportsColor);

        Assert.False(ColorPolicy.ShouldEmitColor(targetIsRedirected: false));
        Assert.False(ColorPolicy.ShouldEmitColor(targetIsRedirected: true));
        Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(Console.Out));
        Assert.False(RuntimeHelpDispatcher.ShouldBoldHeaders(Console.Error));
        var value = new { Name = "rg1" };
        var allowsColor = ColorPolicy.ShouldEmitColor(Console.IsOutputRedirected);
        Assert.Equal(
            CliOutputFormatter.FormatValue(value, OutputFormat.Json),
            CliOutputFormatter.FormatValue(value, OutputFormat.Jsonc, allowsColor));
        Assert.Equal(
            CliOutputFormatter.FormatValue(value, OutputFormat.Yaml),
            CliOutputFormatter.FormatValue(value, OutputFormat.Yamlc, allowsColor));
        Assert.NotEmpty(errors.ToString());
    }

    [Fact]
    public void Initialize_SupportedConsoleRestoresStyling()
    {
        using var _ = new EnvScope(("NO_COLOR", null), ("FORCE_COLOR", "1"));
        using var __ = new ModeScope(ColorMode.Auto);
        ColorPolicy.Initialize(ColorMode.Auto, supportsColor: false);

        ColorPolicy.Initialize(ColorMode.Auto);

        Assert.True(ColorPolicy.ShouldEmitColor(targetIsRedirected: false));
    }

    // Saves and restores env vars around a test. Restoring on Dispose keeps process-wide state clean
    // even when tests fail; matches the collection's DisableParallelization guarantee.
    private sealed class EnvScope : IDisposable
    {
        private readonly (string Name, string? Original)[] _saved;

        public EnvScope(params (string Name, string? Value)[] vars)
        {
            _saved = new (string, string?)[vars.Length];
            for (var i = 0; i < vars.Length; i++)
            {
                _saved[i] = (vars[i].Name, Environment.GetEnvironmentVariable(vars[i].Name));
                Environment.SetEnvironmentVariable(vars[i].Name, vars[i].Value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, original) in _saved)
            {
                Environment.SetEnvironmentVariable(name, original);
            }
        }
    }

    // Saves and restores ColorPolicy._mode via Initialize.
    private sealed class ModeScope : IDisposable
    {
        private readonly ColorMode _previous;

        public ModeScope(ColorMode mode)
        {
            _previous = ColorPolicy.CurrentMode;
            ColorPolicy.Initialize(mode);
        }

        public void Dispose() => ColorPolicy.Initialize(_previous);
    }
}
