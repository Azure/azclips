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
    public CliOutputOptions Output { get; set; } = new();
}

public sealed class PowerShellClientOptions
{
    public OutputOptions Output { get; set; } = new() { Format = "object" };
}

public sealed class OutputOptions
{
    public string Format { get; set; } = "table";
}

public sealed class CliOutputOptions
{
    public string Format { get; set; } = "table";

    public string SimpleFormat { get; set; } = "tsv";
}
