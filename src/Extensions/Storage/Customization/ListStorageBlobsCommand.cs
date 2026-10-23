// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using Core.Abstractions.Runtime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class ListStorageBlobsCommand : BlobContainerWorkflowBase
{
    private static readonly (char Flag, string Dataset)[] s_includes =
        [('s', "snapshots"), ('m', "metadata"), ('c', "copy"), ('d', "deleted"), ('v', "versions"), ('t', "tags")];

    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? Prefix { get; set; }
    public string? Delimiter { get; set; }
    public string? Include { get; set; }
    public string? Marker { get; set; }
    public string NumResults { get; set; } = "5000";
    public bool ShowNextMarker { get; set; }

    protected override async Task ExecuteAsync()
    {
        long? limit = NumResults == "*" ? null :
            long.TryParse(NumResults, NumberStyles.None, CultureInfo.InvariantCulture, out long count) && count > 0
                ? count
                : throw new ArgumentException("NumResults must be a positive 64-bit integer or *.", nameof(NumResults));
        if (Include?.Any(flag => !s_includes.Any(option => option.Flag == flag)) == true)
            throw new ArgumentException("Include supports combined c, d, m, s, t and v flags only.", nameof(Include));
        string[] includes = s_includes.Where(option => Include?.Contains(option.Flag) == true)
            .Select(option => option.Dataset).ToArray();
        bool includeTags = includes.Contains("tags", StringComparer.Ordinal);
        bool showNextMarker = ShowNextMarker;
        string? account = StorageBlobCliAddress.ResolveAccount(StorageAccountName, BlobEndpoint, AuthMode);
        var container = ResolveContainer(defaultAccount: account);
        StorageBlobCliAddress.ValidateContext(account, container.Context);
        var results = new List<JsonElement>();
        string? nextMarker = null;
        var child = CreateExtendedProcessor<ListStorageBlobsProcessor>(processor =>
        {
            processor.Context = container.Context;
            processor.Profile = container.Context.Profile.Name;
            processor.Container = container.Name;
            processor.Prefix = Prefix;
            processor.Delimiter = string.IsNullOrEmpty(Delimiter) ? null : Delimiter;
            processor.Include = includes.Length == 0 ? null : includes;
            processor.Marker = Marker;
            processor.NumResults = limit;
            processor.Timeout = Timeout;
            processor.DeferFailureDiagnostics = true;
            processor.EntryHandler = (entry, token) =>
            {
                token.ThrowIfCancellationRequested();
                results.Add(StorageBlobCliOutput.ProjectEntry(entry, includeTags));
                return Task.CompletedTask;
            };
            processor.NextMarkerHandler = marker => nextMarker = marker;
        });
        await child.ProcessAsync();
        Token.ThrowIfCancellationRequested();
        if (showNextMarker)
            results.Add(JsonSerializer.SerializeToElement(new StorageBlobNextMarker(nextMarker)));
        else if (nextMarker is not null)
            await Publisher.Publish(Events.Warning, "Next Marker: " + nextMarker);
        Token.ThrowIfCancellationRequested();
        await WriteOutput(results.ToArray(), Token);
    }
}
