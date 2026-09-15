// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Core.Configuration.Options;

public sealed class ClientsOptions
{
    public CliClientOptions Cli { get; set; } = new();

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
    public string Format { get; set; } = "json";
}

/// <summary>
/// CLI-only output options. Kept separate from the shared <see cref="OutputOptions"/> (used by
/// PowerShell too) so <see cref="SimpleFormat"/> doesn't leak into PowerShell's config defaults as
/// an undocumented, unsupported key.
/// </summary>
public sealed class CliOutputOptions
{
    public string Format { get; set; } = "json";

    public string SimpleFormat { get; set; } = "tsv";
}
