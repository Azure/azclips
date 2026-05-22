using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class ListExtensionsProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    public IProcessor Init(ProcessorContext context)
    {
        _writeOutput = context.OutputHandler.WriteOutput;
        return this;
    }

    public async Task ProcessAsync()
    {
        var result = Process();
        await _writeOutput(result);
    }

    public object Process()
    {
        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ListExtensionsProcessor)}.{nameof(Process)}");

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        // Surface installed extensions only. Built-ins are not listed here; they're queryable via
        // a separate command in a later phase if needed.
        var items = registry.Extensions
            .OrderBy(kvp => kvp.Value.Verb, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new
            {
                PackageId = kvp.Key,
                Verb = kvp.Value.Verb,
                Version = kvp.Value.Version,
                Pinned = kvp.Value.Pinned,
                Source = kvp.Value.Source,
                InstalledAt = kvp.Value.InstalledAt,
            })
            .ToList();

        return items;
    }

    public string Process(bool Serialize)
    {
        var result = Process();
        if (Serialize)
        {
            return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        else
        {
            return result.ToString() ?? "";
        }
    }
}
