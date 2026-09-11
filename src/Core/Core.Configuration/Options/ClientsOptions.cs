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
    public OutputOptions Output { get; set; } = new() { Format = "json" };
}

public sealed class PowerShellClientOptions
{
    public OutputOptions Output { get; set; } = new() { Format = "object" };
}

public sealed class OutputOptions
{
    public string Format { get; set; } = "json";
}
