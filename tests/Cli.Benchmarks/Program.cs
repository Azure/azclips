using System.IO;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Cli.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithArtifactsPath(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "Debug", "Benchmark"))
            .WithOptions(ConfigOptions.DisableOptimizationsValidator); // 允许 Debug 模式运行

        var summary = BenchmarkRunner.Run<HelpCommandBenchmark>(config);
    }
}
