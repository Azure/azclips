// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class BlobCliCommandBase : BlobTargetWorkflowBase
{
    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? IfModifiedSinceInput { get; set; }
    public string? IfUnmodifiedSinceInput { get; set; }

    protected T CreateTargetWorkflow<T>(Action<T>? configure = null, string? defaultName = null) where T : BlobTargetWorkflowBase, new()
    {
        string? account = StorageBlobCliAddress.ResolveAccount(StorageAccountName, BlobEndpoint, AuthMode);
        var target = ResolveTarget(defaultName, defaultAccount: account);
        var modifiedSince = ParseDate(IfModifiedSinceInput) ?? IfModifiedSince;
        var unmodifiedSince = ParseDate(IfUnmodifiedSinceInput) ?? IfUnmodifiedSince;
        StorageBlobCliAddress.ValidateContext(account, target.Container.Context);
        return CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = target.Container.Context;
            processor.Profile = target.Container.Context.Profile.Name;
            processor.Container = target.Container.Name;
            processor.Blob = target.Name;
            processor.Snapshot = target.Snapshot;
            processor.VersionId = target.VersionId;
            processor.Timeout = Timeout;
            processor.LeaseId = LeaseId;
            processor.IfMatch = IfMatch;
            processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = modifiedSince;
            processor.IfUnmodifiedSince = unmodifiedSince;
            processor.IfTags = IfTags;
            processor.DeferFailureDiagnostics = true;
            configure?.Invoke(processor);
        });
    }

    protected internal static DateTimeOffset? ParseDate(string? value)
    {
        if (value is null) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            throw new ArgumentException("Specify a valid modified/unmodified-since timestamp.");
        return date;
    }
}

public sealed class ShowStorageBlobCommand : BlobCliCommandBase
{
    public bool IncludeTags { get; set; }

    protected override Task ExecuteAsync() =>
        CreateTargetWorkflow<GetStorageBlobProcessor>(processor =>
        {
            processor.IncludeTag = IncludeTags;
            processor.ReturnCliProperties = true;
        }).ProcessAsync();
}

public sealed class TestStorageBlobExistsCommand : BlobCliCommandBase
{
    protected override Task ExecuteAsync() => CreateTargetWorkflow<TestStorageBlobExistsProcessor>().ProcessAsync();
}

public sealed class RemoveStorageBlobCommand : BlobCliCommandBase
{
    public string? DeleteSnapshots { get; set; }

    protected override Task ExecuteAsync() =>
        CreateTargetWorkflow<RemoveStorageBlobProcessor>(processor => processor.DeleteSnapshots = DeleteSnapshots).ProcessAsync();
}

public sealed class SetStorageBlobTierCommand : BlobCliCommandBase
{
    public string Tier { get; set; } = string.Empty;
    public string BlobType { get; set; } = "block";
    public string? RehydratePriority { get; set; }

    protected override Task ExecuteAsync()
    {
        string expectedType = BlobType switch
        {
            "block" => "BlockBlob",
            "page" => "PageBlob",
            _ => throw new ArgumentException("Specify blob type block or page.", nameof(BlobType))
        };
        if (!(BlobType == "block" ? SetStorageBlobTierProcessor.BlockTiers : SetStorageBlobTierProcessor.PageTiers)
                .Contains(Tier, StringComparer.Ordinal) ||
            RehydratePriority is not null && (BlobType == "page" || RehydratePriority is not ("High" or "Standard")))
            throw new ArgumentException("The tier or rehydration priority does not apply to the selected blob type.");
        return CreateTargetWorkflow<SetStorageBlobTierProcessor>(processor =>
        {
            processor.Tier = Tier;
            processor.RehydratePriority = RehydratePriority;
            processor.ExpectedBlobType = expectedType;
        }).ProcessAsync();
    }
}
