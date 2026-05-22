using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Core.Extensions;

namespace Microsoft.Azure.Cirrus.Extensions.Extension.Processors;

public class ShowExtensionProcessor : IProcessor
{
    private OutputStreamDelegate _writeOutput = null!;

    [ArgumentInfo(
    Required = true,
    Description = @"Package id or verb of the extension to show.",
    SerializedName = @"Name")]
    public string Name { get; set; } = string.Empty;

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
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw CirrusErrorException.UsageError("Name is required to show an extension.");
        }

        var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
        using var _ = telemetryService.ActivitySource?.StartActivity($"{nameof(ShowExtensionProcessor)}.{nameof(Process)}");

        var store = new FileExtensionRegistryStore();
        var registry = store.Load();

        // Accept either the full package id (exact match, case-insensitive) or the verb
        // — whichever the user has handy. Verb is what they see at the command line; package id
        // is what `extension list` and `extension add` reference.
        var match = registry.Extensions
            .FirstOrDefault(kvp =>
                string.Equals(kvp.Key, Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Value.Verb, Name, StringComparison.OrdinalIgnoreCase));

        if (match.Key is null)
        {
            throw CirrusErrorException.ExtensionNotFound(
                $"No installed extension matches '{Name}' (by package id or verb).",
                new Dictionary<string, object?> { ["name"] = Name });
        }

        return new
        {
            PackageId = match.Key,
            Verb = match.Value.Verb,
            Version = match.Value.Version,
            Pinned = match.Value.Pinned,
            Source = match.Value.Source,
            InstalledAt = match.Value.InstalledAt,
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
