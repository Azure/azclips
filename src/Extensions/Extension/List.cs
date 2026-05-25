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

        // Phase 8.5: split the listing into live + stale so the user sees missing-payload entries
        // separately (e.g. when they manually deleted `~/.cirrus/extensions/<pkg>/<ver>/`). Live
        // entries are routable; stale entries are advisory until the user removes them.
        var reconciled = RegistryReconciler.Reconcile(registry, store.InstallRoot);

        var live = reconciled.Live.Extensions
            .OrderBy(kvp => kvp.Value.Verb, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => new
            {
                PackageId = kvp.Key,
                Verb = kvp.Value.Verb,
                // Phase 9: a multi-namespace extension contributes to more than one verb;
                // surface the full list so `extension list` can render Compute under both
                // `vm` and `disk` without duplicating the entry.
                Namespaces = kvp.Value.Namespaces.Count > 0
                    ? kvp.Value.Namespaces.ToArray()
                    : (kvp.Value.Verb is null ? Array.Empty<string>() : new[] { kvp.Value.Verb }),
                Version = kvp.Value.Version,
                Pinned = kvp.Value.Pinned,
                Source = kvp.Value.Source,
                InstalledAt = kvp.Value.InstalledAt,
                Status = "live",
            })
            .ToList();

        var stale = reconciled.Stale
            .OrderBy(s => s.PackageId, StringComparer.OrdinalIgnoreCase)
            .Select(s => new
            {
                PackageId = s.PackageId,
                Version = s.Version,
                ExpectedPath = s.ExpectedPath,
                Reason = s.Reason,
                Status = "stale",
            })
            .ToList();

        return new
        {
            Live = live,
            Stale = stale,
        };
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
