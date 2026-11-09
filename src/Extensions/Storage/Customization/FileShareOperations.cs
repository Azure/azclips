// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

internal static class FileShareOperations
{

    internal static StorageFileDirectory Resolve(IFileShareOptions options, StorageDataPlaneProcessorBase owner, string? relativePath = null, StorageFileShare? nativeShare = null)
    {
        if (options.Timeout is <= 0) throw new ArgumentException("Timeout must be positive.");
        var selected = nativeShare ?? options.DirectoryObject?.Share ?? options.ShareObject;
        if (selected is not null && options.ShareObject is not null &&
            (selected.Name != options.ShareObject.Name || selected.ShareSnapshot != options.ShareObject.ShareSnapshot ||
             selected.Context.StorageAccountName != options.ShareObject.Context.StorageAccountName ||
             selected.Context.Profile != options.ShareObject.Context.Profile ||
             selected.Context.EnableFileBackupRequestIntent != options.ShareObject.Context.EnableFileBackupRequestIntent))
            throw new ArgumentException("The native Files resource and share inputs refer to different resources.");
        var context = selected?.Context ?? options.Context;
        if (context is null)
        {
            var profile = owner.ResolvedProfile;
            StorageContext.ValidateProfile(profile);
            context = new StorageContext(options.StorageAccountName ?? throw new ArgumentException("Specify a Storage context or account name."),
                profile!, options.EnableFileBackupRequestIntent);
        }
        StorageContext.ValidateProfile(context.Profile);
        if (options.Context is not null && (options.Context.StorageAccountName != context.StorageAccountName || options.Context.Profile != context.Profile) ||
            options.StorageAccountName is not null && options.StorageAccountName != context.StorageAccountName)
            throw new ArgumentException("The selected account/profile conflicts with the Files resource context.");
        if (!context.EnableFileBackupRequestIntent && options.EnableFileBackupRequestIntent)
            context = new StorageContext(context.StorageAccountName, context.Profile, enableFileBackupRequestIntent: true);
        if (!context.EnableFileBackupRequestIntent)
            throw new ArgumentException("Azure Files OAuth requires explicit EnableFileBackupRequestIntent.");
        string share = selected?.Name ?? options.ShareName ?? throw new ArgumentException("Specify a share name or native share/directory.");
        if (options.ShareName is not null && options.ShareName != share ||
            options.ShareSnapshot is not null && selected?.ShareSnapshot is not null && options.ShareSnapshot != selected.ShareSnapshot ||
            (options.ShareSnapshot ?? selected?.ShareSnapshot) == string.Empty)
            throw new ArgumentException("The share name or snapshot conflicts with the native resource.");
        string parent = options.DirectoryObject?.Path.Trim('/') ?? string.Empty;
        string child = relativePath?.Trim('/') ?? string.Empty;
        string path = parent.Length == 0 ? child : child.Length == 0 ? parent : parent + "/" + child;
        var result = new StorageFileDirectory(path, new StorageFileShare(share, context, options.ShareSnapshot ?? selected?.ShareSnapshot));
        _ = result.Uri;
        return result;
    }

    internal static T CreateProcessor<T>(StorageDataPlaneProcessorBase owner, StorageFileDirectory directory, Action<T> configure) where T : StorageDataPlaneProcessorBase, new() =>
        owner.CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = directory.Context;
            processor.DeferFailureDiagnostics = true;
            configure(processor);
        });

    internal static async Task<bool> IsDirectoryAsync<T>(T owner, StorageFileDirectory target)
        where T : StorageDataPlaneProcessorBase, IFileShareOptions
    {
        try
        {
            var probe = CreateProcessor<FileProcessors.GetPropertiesDirectoryProcessor>(owner, target, processor =>
            {
                processor.Url = target.Uri.AbsoluteUri;
                processor.Timeout = owner.Timeout;
                processor.AllowTrailingDot = owner.AllowTrailingDot;
                processor.DeferFailureDiagnostics = true;
            });
            using var response = await probe.SendAsync();
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidDataException("Storage did not confirm directory properties.");
            return true;
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden ||
            exception.StatusCode == HttpStatusCode.NotFound && exception.Data["ServiceErrorCode"] is "ResourceNotFound" or "ParentNotFound")
        {
            return false;
        }
    }

}
