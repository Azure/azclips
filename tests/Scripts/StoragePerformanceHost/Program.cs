// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;

namespace StoragePerformance;

public sealed record Measurement(long BaselineWorkingSetBytes, long PeakWorkingSetBytes,
    long IncrementalWorkingSetBytes, long SampleCount, double ElapsedSeconds);

public sealed class MemoryMeasurement : IDisposable
{
    private readonly Process process = Process.GetCurrentProcess();
    private readonly object gate = new();
    private readonly Timer timer;
    private readonly Stopwatch watch;
    private readonly long baseline;
    private long peak;
    private long samples;

    public MemoryMeasurement()
    {
        var idle = new long[20];
        for (int index = 0; index < idle.Length; index++)
        {
            process.Refresh();
            idle[index] = process.WorkingSet64;
            Thread.Sleep(50);
        }
        Array.Sort(idle);
        baseline = idle[idle.Length / 2];
        peak = baseline;
        watch = Stopwatch.StartNew();
        timer = new Timer(_ => Sample(), null, 0, 25);
    }

    private void Sample()
    {
        lock (gate)
        {
            process.Refresh();
            peak = Math.Max(peak, process.WorkingSet64);
            samples++;
        }
    }

    public Measurement Complete()
    {
        watch.Stop();
        timer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Sample();
        // Lifetime peak also catches brief spikes between the 25 ms samples.
        peak = Math.Max(peak, process.PeakWorkingSet64);
        return new(baseline, peak, Math.Max(0, peak - baseline), samples, watch.Elapsed.TotalSeconds);
    }

    public void Dispose()
    {
        timer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        process.Dispose();
    }
}

internal sealed record CliPlan(string[][] Warmup, string[] Command, string MetricsPath);

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Specify one benchmark plan JSON file.");
        var plan = JsonSerializer.Deserialize<CliPlan>(await File.ReadAllTextAsync(args[0]))
            ?? throw new InvalidDataException("The CLI benchmark plan is empty.");
        foreach (string[] command in plan.Warmup)
            if (await Core.Cli.Program.Main(command) != 0)
                throw new InvalidOperationException("CLI benchmark warmup failed.");

        using var measurement = new MemoryMeasurement();
        if (await Core.Cli.Program.Main(plan.Command) != 0)
            throw new InvalidOperationException("CLI benchmark transfer failed.");
        var result = measurement.Complete();
        await File.WriteAllTextAsync(plan.MetricsPath, JsonSerializer.Serialize(result));
        return 0;
    }
}
