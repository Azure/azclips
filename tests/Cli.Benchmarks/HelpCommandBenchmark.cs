using BenchmarkDotNet.Attributes;
using Core.Cli;

namespace Cli.Benchmarks;

[MemoryDiagnoser]
[HtmlExporter, CsvExporter, JsonExporter, XmlExporter]
[RPlotExporter] // Requires R to be installed for generating plots
public class HelpCommandBenchmark
{
    private readonly string[] _args = new[] { "--help" };

    [Benchmark]
    public async Task RunHelpCommand()
    {
        await Core.Cli.Program.Main(_args);
    }
}
