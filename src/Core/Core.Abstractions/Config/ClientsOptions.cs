// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Core.Configuration.Options;

[ConfigurationSection("clients")]
public sealed class ClientsOptions
{
    public CliClientOptions Cli { get; set; } = new();

    [ConfigurationSection("powershell")]
    public PowerShellClientOptions PowerShell { get; set; } = new();
}

public sealed class CliClientOptions
{
    public CliOutputOptions Output { get; set; } = new() { Format = "json" };
}

public sealed class PowerShellClientOptions
{
    public OutputOptions Output { get; set; } = new() { Format = "object" };
}

public sealed class OutputOptions
{
    [ConfigurationKey("format", Description = "Default PowerShell output format.", AllowedValues = ["object", "json", "table"])]
    public string Format { get; set; } = "json";
}

/// <summary>
/// CLI-only output options. Kept separate from the shared <see cref="OutputOptions"/> (used by
/// PowerShell too) so <see cref="SimpleFormat"/> doesn't leak into PowerShell's config defaults as
/// an undocumented, unsupported key.
/// </summary>
public sealed class CliOutputOptions
{
    [ConfigurationKey("format", Description = "Default CLI output format.", AllowedValues = ["json", "jsonc", "table", "tsv", "yaml", "yamlc"])]
    public string Format { get; set; } = "json";

    [ConfigurationKey("simpleFormat", Description = "Default CLI output format for scalar (string/number/bool/null) results, e.g. after a --query that narrows to a single field. Falls back to clients.cli.output.format for object/array results.", AllowedValues = ["json", "jsonc", "table", "tsv", "yaml", "yamlc"])]
    public string SimpleFormat { get; set; } = "tsv";
}
