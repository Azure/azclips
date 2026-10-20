// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Cli.Parser;
using Core.Configuration.Options;

namespace Core.Cli.Output;

/// <summary>Where the effective complex-result output format came from.</summary>
internal enum OutputFormatSource
{
    /// <summary>Built-in default (no <c>--output</c> flag and no user-set <c>clients.cli.output.format</c>).</summary>
    Default,
    /// <summary>User-set <c>clients.cli.output.format</c> (stored config or environment).</summary>
    UserConfig,
    /// <summary>Explicit <c>--output</c> flag.</summary>
    Flag,
}

internal static class OutputFormatPolicy
{
    internal const string FormatConfigKey = "clients.cli.output.format";

    /// <summary>
    /// Resolves where the effective format comes from. A user-set format only counts when it was
    /// stored by the user (not the <see cref="ClientsOptions"/> property default) and parses.
    /// </summary>
    public static OutputFormatSource ResolveFormatSource(bool userSetFormat, ClientsOptions options, OutputFormat? flagOverride)
    {
        if (flagOverride is not null)
        {
            return OutputFormatSource.Flag;
        }

        return userSetFormat && TryParse(options.Cli.Output.Format, out _)
            ? OutputFormatSource.UserConfig
            : OutputFormatSource.Default;
    }

    /// <summary>
    /// True when <c>clients.cli.output.format</c> has a non-default source (stored or environment).
    /// </summary>
    public static bool IsFormatUserSet(IConfigurationService configurationService)
    {
        if (configurationService is ISourceAwareConfigurationService sourceAware)
        {
            var values = sourceAware.ListConfigurationWithSourcesAsync().GetAwaiter().GetResult();
            return values.Values.Any(v =>
                string.Equals(v.Key.Replace(':', '.'), FormatConfigKey, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(v.Source, "default", StringComparison.OrdinalIgnoreCase));
        }

        var stored = configurationService.ListConfigurationAsync(includeDefaults: false).GetAwaiter().GetResult();
        return stored.Keys.Any(k => string.Equals(k.Replace(':', '.'), FormatConfigKey, StringComparison.OrdinalIgnoreCase));
    }

    public static OutputFormat ResolveEffectiveFormat(IConfigurationService configurationService, OutputFormat? flagOverride)
        => ResolveEffectiveFormat(configurationService.GetConfiguration<ClientsOptions>(), flagOverride);

    public static OutputFormat ResolveEffectiveFormat(ClientsOptions options, OutputFormat? flagOverride)
        => flagOverride ?? (TryParse(options.Cli.Output.Format, out var configured) ? configured : OutputFormat.Json);

    public static OutputFormat ResolveEffectiveSimpleFormat(IConfigurationService configurationService, OutputFormat? flagOverride)
        => ResolveEffectiveSimpleFormat(configurationService.GetConfiguration<ClientsOptions>(), flagOverride);

    /// <summary>
    /// Same precedence as <see cref="ResolveEffectiveFormat(ClientsOptions, OutputFormat?)"/> but reads
    /// <c>clients.cli.output.simpleFormat</c> -- the default applied to scalar (string/number/bool/null)
    /// results, e.g. after a <c>--query</c> that narrows a complex result to a single field. An explicit
    /// <c>--output</c> flag still wins over both this and the complex-result default.
    /// </summary>
    public static OutputFormat ResolveEffectiveSimpleFormat(ClientsOptions options, OutputFormat? flagOverride)
        => flagOverride ?? (TryParse(options.Cli.Output.SimpleFormat, out var configured) ? configured : OutputFormat.Tsv);

    /// <summary>
    /// Shared string-&gt;<see cref="OutputFormat"/> parser used by both the <c>--output</c> flag
    /// (<see cref="Parser.ParseAction"/>, which throws a usage error on failure) and
    /// <c>clients.cli.output.format</c> config resolution (which falls back to the default on
    /// failure instead) -- one parser, two failure policies.
    /// </summary>
    internal static bool TryParse(string? value, out OutputFormat format)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "json": format = OutputFormat.Json; return true;
            case "jsonc": format = OutputFormat.Jsonc; return true;
            case "table": format = OutputFormat.Table; return true;
            case "tsv": format = OutputFormat.Tsv; return true;
            case "yaml": format = OutputFormat.Yaml; return true;
            case "yamlc": format = OutputFormat.Yamlc; return true;
            default: format = default; return false;
        }
    }
}
