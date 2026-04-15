namespace Core.Configuration.Options;

public sealed class HostsOptions
{
    public CliHostOptions Cli { get; set; } = new();

    public PowerShellHostOptions PowerShell { get; set; } = new();
}

public sealed class CliHostOptions
{
    public OutputOptions Output { get; set; } = new() { Format = "table" };
}

public sealed class PowerShellHostOptions
{
    public OutputOptions Output { get; set; } = new() { Format = "object" };
}

public sealed class OutputOptions
{
    public string Format { get; set; } = "table";
}
