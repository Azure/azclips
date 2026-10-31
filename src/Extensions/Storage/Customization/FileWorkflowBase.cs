// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using FileProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Files.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class FileShareWorkflowBase : StorageDataPlaneProcessorBase
{
    public string? StorageAccountName { get; set; }
    public string? ShareName { get; set; }
    public StorageFileShare? ShareObject { get; set; }
    public StorageFileDirectory? DirectoryObject { get; set; }
    public string? Path { get; set; }
    public string? ShareSnapshot { get; set; }
    public bool EnableFileBackupRequestIntent { get; set; }
    public bool? AllowTrailingDot { get; set; }
    public int? Timeout { get; set; }
    protected sealed override StorageService Service => StorageService.Files;
    protected sealed override HttpMethod Method => HttpMethod.Get;
    protected sealed override string CreateRequestUri() =>
        throw new InvalidOperationException("Files workflows execute through their atomic child processors.");

    protected StorageFileDirectory ResolveDirectory(string? relativePath = null, StorageFileShare? nativeShare = null)
    {
        if (Timeout is <= 0) throw new ArgumentException("Timeout must be positive.");
        var selected = nativeShare ?? DirectoryObject?.Share ?? ShareObject;
        if (selected is not null && ShareObject is not null &&
            (selected.Name != ShareObject.Name || selected.ShareSnapshot != ShareObject.ShareSnapshot ||
             selected.Context.StorageAccountName != ShareObject.Context.StorageAccountName ||
             selected.Context.Profile != ShareObject.Context.Profile ||
             selected.Context.EnableFileBackupRequestIntent != ShareObject.Context.EnableFileBackupRequestIntent))
            throw new ArgumentException("The native Files resource and share inputs refer to different resources.");
        var context = selected?.Context ?? Context;
        if (context is null)
        {
            StorageContext.ValidateProfile(ResolvedProfile);
            context = new StorageContext(StorageAccountName ?? throw new ArgumentException("Specify a Storage context or account name."),
                ResolvedProfile!, EnableFileBackupRequestIntent);
        }
        StorageContext.ValidateProfile(context.Profile);
        if (Context is not null && (Context.StorageAccountName != context.StorageAccountName || Context.Profile != context.Profile) ||
            StorageAccountName is not null && StorageAccountName != context.StorageAccountName)
            throw new ArgumentException("The selected account/profile conflicts with the Files resource context.");
        if (!context.EnableFileBackupRequestIntent && EnableFileBackupRequestIntent)
            context = new StorageContext(context.StorageAccountName, context.Profile, enableFileBackupRequestIntent: true);
        if (!context.EnableFileBackupRequestIntent)
            throw new ArgumentException("Azure Files OAuth requires explicit EnableFileBackupRequestIntent.");
        string share = selected?.Name ?? ShareName ?? throw new ArgumentException("Specify a share name or native share/directory.");
        if (ShareName is not null && ShareName != share ||
            ShareSnapshot is not null && selected?.ShareSnapshot is not null && ShareSnapshot != selected.ShareSnapshot ||
            (ShareSnapshot ?? selected?.ShareSnapshot) == string.Empty)
            throw new ArgumentException("The share name or snapshot conflicts with the native resource.");
        string parent = DirectoryObject?.Path.Trim('/') ?? string.Empty;
        string child = relativePath?.Trim('/') ?? string.Empty;
        string path = parent.Length == 0 ? child : child.Length == 0 ? parent : parent + "/" + child;
        var result = new StorageFileDirectory(path, new StorageFileShare(share, context, ShareSnapshot ?? selected?.ShareSnapshot));
        _ = result.Uri;
        return result;
    }

    protected T CreateFileProcessor<T>(StorageFileDirectory directory, Action<T> configure) where T : StorageDataPlaneProcessorBase, new() =>
        CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = directory.Context;
            processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
            configure(processor);
        });

    protected async Task<bool> IsDirectoryAsync(StorageFileDirectory target)
    {
        try
        {
            var probe = CreateFileProcessor<FileProcessors.GetPropertiesDirectoryProcessor>(target, processor =>
            {
                processor.Url = target.Uri.AbsoluteUri;
                processor.Timeout = Timeout;
                processor.AllowTrailingDot = AllowTrailingDot;
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

    protected abstract Task ExecuteAsync();
    public sealed override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);
}
