using BenchmarkDotNet.Attributes;
using Core.Cli;
using Core.Cli.Exceptions;
using Core.Cli.Parser;

namespace Cli.Benchmarks;

[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(10)]
public class HelpCommandBenchmark
{
    private ExtensionDispatcher _dispatcher = null!;

    [GlobalSetup]
    public void Setup()
    {
        _dispatcher = new ExtensionDispatcher(BenchmarkPaths.ArtifactsDir);
    }

    [Benchmark]
    public void ParseHelpCommand()
    {
        try
        {
            _dispatcher.Parse(new[] { "--help" });
        }
        catch (DispatchHelpException)
        {
            // Expected - help throws exception
        }
    }
}
