// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class BlobCliOrchestratorBase : BlobTargetWorkflowBase
{
    public string AuthMode { get; set; } = "login";
    public string? BlobEndpoint { get; set; }
    public string? IfModifiedSinceInput { get; set; }
    public string? IfUnmodifiedSinceInput { get; set; }

    protected void ResolveCliTarget(string? defaultName = null)
    {
        string? account = StorageBlobCliAddress.ResolveAccount(StorageAccountName, BlobEndpoint, AuthMode);
        var target = ResolveTarget(defaultName, defaultAccount: account);
        var modifiedSince = ParseDate(IfModifiedSinceInput) ?? IfModifiedSince;
        var unmodifiedSince = ParseDate(IfUnmodifiedSinceInput) ?? IfUnmodifiedSince;
        StorageBlobCliAddress.ValidateContext(account, target.Container.Context);
        Context = target.Container.Context;
        Profile = target.Container.Context.Profile.Name;
        Container = target.Container.Name;
        Blob = target.Name;
        Snapshot = target.Snapshot;
        VersionId = target.VersionId;
        IfModifiedSince = modifiedSince;
        IfUnmodifiedSince = unmodifiedSince;
    }

    protected T CreateTargetWorkflow<T>(Action<T>? configure = null, string? defaultName = null) where T : BlobTargetWorkflowBase, new()
    {
        ResolveCliTarget(defaultName);
        return CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = Context;
            processor.Profile = Profile;
            processor.Container = Container;
            processor.Blob = Blob;
            processor.Snapshot = Snapshot;
            processor.VersionId = VersionId;
            processor.Timeout = Timeout;
            processor.LeaseId = LeaseId;
            processor.IfMatch = IfMatch;
            processor.IfNoneMatch = IfNoneMatch;
            processor.IfModifiedSince = IfModifiedSince;
            processor.IfUnmodifiedSince = IfUnmodifiedSince;
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
