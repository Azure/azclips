// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class BlobCliOperations
{

    internal static void ResolveTarget<T>(T owner, string? defaultName = null)
        where T : StorageDataPlaneProcessorBase, IBlobCliProcessor
    {
        ResolveTargetSettings(owner, owner, defaultName);
    }

    private static void ResolveTargetSettings(StorageDataPlaneProcessorBase owner, IBlobCliProcessor options, string? defaultName)
    {
        string? account = StorageBlobCliAddress.ResolveAccount(options.StorageAccountName, options.BlobEndpoint, options.AuthMode);
        var target = BlobTargetOperations.Resolve(options, owner, defaultName, defaultAccount: account);
        var modifiedSince = ParseDate(options.IfModifiedSinceInput) ?? options.IfModifiedSince;
        var unmodifiedSince = ParseDate(options.IfUnmodifiedSinceInput) ?? options.IfUnmodifiedSince;
        StorageBlobCliAddress.ValidateContext(account, target.Container.Context);
        options.Context = target.Container.Context;
        options.Profile = target.Container.Context.Profile.Name;
        options.Container = target.Container.Name;
        options.Blob = target.Name;
        options.Snapshot = target.Snapshot;
        options.VersionId = target.VersionId;
        options.IfModifiedSince = modifiedSince;
        options.IfUnmodifiedSince = unmodifiedSince;
    }

    internal static T CreateWorkflow<T>(StorageDataPlaneProcessorBase owner, IBlobCliProcessor options,
        Action<T>? configure = null, string? defaultName = null,
        Core.Abstractions.Runtime.OutputStreamDelegate? output = null)
        where T : StorageDataPlaneProcessorBase, IBlobTargetOptions, new()
    {
        ResolveTargetSettings(owner, options, defaultName);
        return owner.CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = options.Context;
            processor.Profile = options.Profile;
            processor.Container = options.Container;
            processor.Blob = options.Blob;
            processor.Snapshot = options.Snapshot;
            processor.VersionId = options.VersionId;
            processor.Timeout = options.Timeout;
            processor.LeaseId = options.LeaseId;
            processor.IfMatch = options.IfMatch;
            processor.IfNoneMatch = options.IfNoneMatch;
            processor.IfModifiedSince = options.IfModifiedSince;
            processor.IfUnmodifiedSince = options.IfUnmodifiedSince;
            processor.IfTags = options.IfTags;
            processor.DeferFailureDiagnostics = true;
            processor.OutputOverride = output;
            configure?.Invoke(processor);
        });
    }

    internal static DateTimeOffset? ParseDate(string? value)
    {
        if (value is null) return null;
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
            throw new ArgumentException("Specify a valid modified/unmodified-since timestamp.");
        return date;
    }
}
