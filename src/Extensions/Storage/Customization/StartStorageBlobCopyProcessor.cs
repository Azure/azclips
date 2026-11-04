// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Web;
using BlobModels = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Models;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed record StorageBlobCopyStartResult(string CopyId, string CopyStatus, string ETag, DateTimeOffset LastModified);

public sealed class StartStorageBlobCopyProcessor : BlobTargetWorkflowBase
{
    public StorageContext? SourceContext { get; set; }
    public string? SourceStorageAccountName { get; set; }
    public string? SourceUri { get; set; }
    public string? SourceSas { get; set; }
    public string? SourceAccountKey { get; set; }
    public bool UseSourceAccountKeyLookup { get; set; }
    public string? SourceKeyResourceGroup { get; set; }
    public string? SourceKeySubscriptionId { get; set; }
    public string SourceKeyName { get; set; } = "key1";
    public string? SourceContainer { get; set; }
    public string? SourceBlob { get; set; }
    public StorageBlob? SourceBlobObject { get; set; }
    public StorageBlobContainer? SourceContainerObject { get; set; }
    public string? SourceSnapshot { get; set; }
    public string? SourceVersionId { get; set; }
    public string? SourceShareName { get; set; }
    public string? SourcePath { get; set; }
    public StorageFileShare? SourceShareObject { get; set; }
    public StorageFileDirectory? SourceDirectoryObject { get; set; }
    public StorageFile? SourceFileObject { get; set; }
    public string? SourceShareSnapshot { get; set; }
    public bool EnableSourceFileBackupRequestIntent { get; set; }
    public string? SourceIfMatch { get; set; }
    public string? SourceIfNoneMatch { get; set; }
    public DateTimeOffset? SourceIfModifiedSince { get; set; }
    public DateTimeOffset? SourceIfUnmodifiedSince { get; set; }
    public string? SourceIfTags { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
    public string? Tier { get; set; }
    public string? RehydratePriority { get; set; }
    public bool Overwrite { get; set; }
    internal bool ReturnCopyState { get; set; }
    internal Func<StorageBlobCopyStartResult, CancellationToken, Task>? CopyStateHandler { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;

    private sealed record Source(Uri PublicUri, Uri? AuthorizedUri, StorageContext Context, StorageService Service);

    public StartStorageBlobCopyProcessor FreezePowerShellRequest()
    {
        var frozen = (StartStorageBlobCopyProcessor)MemberwiseClone();
        frozen.Metadata = BlobPowerShellOptions.Freeze(Metadata);
        frozen.Tags = BlobPowerShellOptions.Freeze(Tags);
        frozen.Overwrite = true;
        return frozen;
    }

    public string ValidatePowerShellCopy()
    {
        if (Context is null && ContainerObject is null && InputObject is null)
            throw new ArgumentException("Specify a native destination context or container.");
        var target = ResolveTarget(SourceBlobObject?.Name ?? SourceBlob ??
            SourceFileObject?.Name ?? (SourcePath is null ? null : SourcePath[(SourcePath.LastIndexOf('/') + 1)..]) ??
            (SourceUri is null ? null : Uri.UnescapeDataString(new Uri(SourceUri).Segments.Last())));
        if (target.Snapshot is not null || target.VersionId is not null)
            throw new ArgumentException("Copy must target a base blob.");
        var source = ResolveSource(target.Container.Context);
        ValidateCopySettings(source);
        using (var validation = new HttpRequestMessage()) AddMetadataHeaders(validation, Metadata);
        _ = BlobUploadWorkflowBase.EncodeTags(Tags);
        ContainerObject = target.Container; Container = target.Container.Name;
        Context = target.Container.Context; Blob = target.Name; InputObject = null;
        return target.Url;
    }

    internal void ApplyPowerShellRequest(StartStorageBlobCopyProcessor copy)
    {
        BlobPowerShellOptions.Target(this, copy);
        copy.SourceContext = SourceContext; copy.SourceStorageAccountName = SourceStorageAccountName; copy.SourceUri = SourceUri;
        copy.SourceSas = SourceSas; copy.SourceAccountKey = SourceAccountKey;
        copy.UseSourceAccountKeyLookup = UseSourceAccountKeyLookup; copy.SourceKeyResourceGroup = SourceKeyResourceGroup;
        copy.SourceKeySubscriptionId = SourceKeySubscriptionId; copy.SourceKeyName = SourceKeyName;
        copy.SourceContainer = SourceContainer; copy.SourceBlob = SourceBlob; copy.SourceBlobObject = SourceBlobObject;
        copy.SourceContainerObject = SourceContainerObject; copy.SourceSnapshot = SourceSnapshot; copy.SourceVersionId = SourceVersionId;
        copy.SourceShareName = SourceShareName; copy.SourcePath = SourcePath; copy.SourceShareObject = SourceShareObject;
        copy.SourceDirectoryObject = SourceDirectoryObject; copy.SourceFileObject = SourceFileObject;
        copy.SourceShareSnapshot = SourceShareSnapshot; copy.EnableSourceFileBackupRequestIntent = EnableSourceFileBackupRequestIntent;
        copy.SourceIfMatch = SourceIfMatch; copy.SourceIfNoneMatch = SourceIfNoneMatch;
        copy.SourceIfModifiedSince = SourceIfModifiedSince; copy.SourceIfUnmodifiedSince = SourceIfUnmodifiedSince;
        copy.SourceIfTags = SourceIfTags; copy.Metadata = Metadata; copy.Tags = Tags;
        copy.Tier = Tier; copy.RehydratePriority = RehydratePriority;
    }

    private void ValidateCopySettings(Source source)
    {
        if (Tier is not null && !IsBlockTier(Tier) && !IsPageTier(Tier) ||
            RehydratePriority is not null && (RehydratePriority is not ("High" or "Standard") || !IsBlockTier(Tier)))
            throw new ArgumentException("Specify a supported copy tier and compatible rehydration priority.");
        BlobPowerShellOptions.ValidateHeaders(LeaseId, IfMatch, IfNoneMatch, IfTags, SourceIfMatch, SourceIfNoneMatch, SourceIfTags);
        if (source.Service == StorageService.Files &&
            (SourceIfMatch is not null || SourceIfNoneMatch is not null || SourceIfModifiedSince is not null ||
             SourceIfUnmodifiedSince is not null || SourceIfTags is not null))
            throw new ArgumentException("Blob-source conditions cannot be applied to an Azure Files source.");
    }

    protected override async Task ExecuteAsync()
    {
        var destination = ResolveTarget();
        if (destination.Snapshot is not null || destination.VersionId is not null)
            throw new ArgumentException("Copy must target a base blob, not a snapshot or version.");
        var source = ResolveSource(destination.Container.Context);
        if (!Overwrite && (IfMatch is not null || IfNoneMatch is not null && IfNoneMatch != "*"))
            throw new ArgumentException("ETag overwrite conditions require Overwrite; otherwise copy requires an absent destination.");
        ValidateCopySettings(source);
        var metadata = Metadata is null ? null : new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(Metadata, StringComparer.OrdinalIgnoreCase));
        using (var validation = new HttpRequestMessage()) AddMetadataHeaders(validation, metadata);
        var frozenTags = Tags is null ? null : new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(Tags, StringComparer.Ordinal));
        string? tags = BlobUploadWorkflowBase.EncodeTags(frozenTags);
        if (!ShouldProcess(destination.Url, "Start blob copy")) return;
        if (Tier is not null && (source.Service == StorageService.Files || source.AuthorizedUri is null))
        {
            string type = source.Service == StorageService.Files ? "BlockBlob" : await ReadSourceTypeAsync(source);
            if (!(type == "BlockBlob" && IsBlockTier(Tier) || type == "PageBlob" && IsPageTier(Tier)))
                throw new ArgumentException("The requested tier does not apply to the copy source's blob type.");
        }
        Uri authorized = source.AuthorizedUri ?? await AuthorizeSourceAsync(source);
        var child = CreateBlobProcessor<BlobProcessors.StartCopyFromUrlBlobProcessor>(destination.Container, processor =>
        {
            processor.Url = destination.Url; processor.CopySource = authorized.AbsoluteUri; processor.Timeout = Timeout;
            processor.LeaseId = LeaseId; processor.Metadata = metadata; processor.BlobTagsString = tags;
            processor.Tier = Tier; processor.RehydratePriority = RehydratePriority;
            processor.IfMatch = IfMatch; processor.IfNoneMatch = Overwrite ? IfNoneMatch : "*";
            processor.IfModifiedSince = Date(IfModifiedSince); processor.IfUnmodifiedSince = Date(IfUnmodifiedSince); processor.IfTags = IfTags;
            processor.SourceIfMatch = SourceIfMatch; processor.SourceIfNoneMatch = SourceIfNoneMatch;
            processor.SourceIfModifiedSince = Date(SourceIfModifiedSince); processor.SourceIfUnmodifiedSince = Date(SourceIfUnmodifiedSince);
            processor.SourceIfTags = SourceIfTags;
        });
        using var response = await child.SendAsync();
        string? Header(string name) => response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
        string? id = Header("x-ms-copy-id"), status = Header("x-ms-copy-status");
        if (response.StatusCode != HttpStatusCode.Accepted || !Guid.TryParseExact(id, "D", out var copyId) || copyId == Guid.Empty ||
            status is not ("pending" or "success" or "failed" or "aborted") ||
            response.Headers.ETag is null || response.Content.Headers.LastModified is null)
            throw new InvalidDataException("Storage returned invalid copy-start properties. Sensitive details are omitted.");
        Token.ThrowIfCancellationRequested();
        if (ReturnCopyState)
        {
            var result = new StorageBlobCopyStartResult(id!, status,
                response.Headers.ETag.ToString(), response.Content.Headers.LastModified.Value);
            await (CopyStateHandler?.Invoke(result, Token) ?? WriteOutput(result, Token));
        }
        else await WriteOutput(new StorageBlob(destination.Name, destination.Container, new BlobModels.BlobProperties
        {
            ETag = response.Headers.ETag.ToString(),
            LastModified = response.Content.Headers.LastModified.Value.ToString("R", CultureInfo.InvariantCulture),
            CopyId = id, CopyStatus = status, CopySource = source.PublicUri.AbsoluteUri
        })
        {
            Metadata = metadata ?? StorageBlob.EmptyMap,
            Tags = frozenTags ?? StorageBlob.EmptyMap
        }, Token);
    }

    private Source ResolveSource(StorageContext destination)
    {
        if (SourceUri is not null && (SourceBlobObject is not null || SourceContainerObject is not null || SourceContainer is not null ||
            SourceBlob is not null || SourceFileObject is not null || SourceDirectoryObject is not null || SourceShareObject is not null ||
            SourceShareName is not null || SourcePath is not null))
            throw new ArgumentException("Choose a source URI or a named/native source, not both.");
        bool file = SourceFileObject is not null || SourceDirectoryObject is not null || SourceShareObject is not null ||
            SourceShareName is not null || SourcePath is not null;
        Uri? supplied = null;
        if (SourceUri is not null)
        {
            if (!Uri.TryCreate(SourceUri, UriKind.Absolute, out supplied))
                throw new ArgumentException("Specify an absolute public-cloud Storage source URI.");
            file = supplied.IdnHost.EndsWith(".file.core.windows.net", StringComparison.OrdinalIgnoreCase);
        }
        StorageService service = file ? StorageService.Files : StorageService.Blob;
        var nativeShare = SourceFileObject?.Share ?? SourceDirectoryObject?.Share ?? SourceShareObject;
        var nativeContainer = SourceBlobObject?.Container ?? SourceContainerObject;
        if (file && (nativeContainer is not null || SourceContainer is not null || SourceBlob is not null ||
            SourceSnapshot is not null || SourceVersionId is not null) ||
            !file && SourceShareSnapshot is not null || SourceFileObject is not null && SourceDirectoryObject is not null)
            throw new ArgumentException("Blob and Files source families cannot be combined.");
        var nativeContext = file ? nativeShare?.Context : nativeContainer?.Context;
        var query = ParseSourceQuery(supplied?.Query ?? "");
        if (SourceSas is not null)
        {
            if (string.IsNullOrWhiteSpace(SourceSas))
                throw new ArgumentException("Specify a nonempty source SAS.");
            if (query.AllKeys.Any(key => !new[] { "snapshot", "versionid", "sharesnapshot" }.Contains(key, StringComparer.OrdinalIgnoreCase)))
                throw new ArgumentException("A separate source SAS cannot replace an existing authorization query.");
            var sas = ParseSourceQuery(SourceSas.TrimStart('?'));
            foreach (string? key in sas.AllKeys)
            {
                if (query.GetValues(key) is not null && query[key] != sas[key])
                    throw new ArgumentException("Source SAS parameters conflict with the selected resource.");
                query[key] = sas[key];
            }
        }
        var publicQuery = HttpUtility.ParseQueryString("");
        string? snapshot = file ? SourceShareSnapshot ?? nativeShare?.ShareSnapshot ?? query["sharesnapshot"] :
            SourceSnapshot ?? SourceBlobObject?.Snapshot ?? query["snapshot"];
        string? version = SourceVersionId ?? SourceBlobObject?.VersionId ?? query["versionid"];
        if (snapshot == string.Empty || version == string.Empty || snapshot is not null && version is not null ||
            file && (query["snapshot"] is not null || query["versionid"] is not null) ||
            !file && query["sharesnapshot"] is not null ||
            (file ? nativeShare?.ShareSnapshot : SourceBlobObject?.Snapshot) is { } nativeSnapshot && nativeSnapshot != snapshot ||
            SourceBlobObject?.VersionId is { } nativeVersion && nativeVersion != version)
            throw new ArgumentException("Copy source selectors conflict.");
        string selector = file ? "sharesnapshot" : "snapshot";
        if (query[selector] is { } uriSnapshot && uriSnapshot != snapshot || query["versionid"] is { } uriVersion && uriVersion != version)
            throw new ArgumentException("Copy source URI selectors conflict.");
        if (snapshot is not null) publicQuery[selector] = snapshot;
        if (version is not null) publicQuery["versionid"] = version;
        Uri? publicUri = supplied is null ? null : new UriBuilder(supplied) { Query = publicQuery.ToString() }.Uri;
        string? uriAccount = publicUri is null ? null : StorageContext.ValidatePublicUri(publicUri, service);
        var context = nativeContext ?? SourceContext ?? new StorageContext(
            SourceStorageAccountName ?? uriAccount ?? destination.StorageAccountName, ResolvedProfile!,
            enableFileBackupRequestIntent: file && EnableSourceFileBackupRequestIntent);
        StorageContext.ValidateProfile(context.Profile);
        if (SourceContext is not null && (SourceContext.StorageAccountName != context.StorageAccountName || SourceContext.Profile != context.Profile) ||
            SourceStorageAccountName is not null && SourceStorageAccountName != context.StorageAccountName ||
            uriAccount is not null && uriAccount != context.StorageAccountName)
            throw new ArgumentException("The source account/profile conflicts with the selected source.");
        if (file && !context.EnableFileBackupRequestIntent && EnableSourceFileBackupRequestIntent)
            context = new StorageContext(context.StorageAccountName, context.Profile, enableFileBackupRequestIntent: true);
        bool presigned = !string.IsNullOrEmpty(query["sig"]);
        if (SourceSas is not null && !presigned)
            throw new ArgumentException("The supplied source SAS must include a nonempty signature.");
        if (!presigned)
        {
            bool keyOptions = SourceAccountKey is not null || UseSourceAccountKeyLookup ||
                SourceKeyResourceGroup is not null || SourceKeySubscriptionId is not null || SourceKeyName != "key1";
            if (!file && keyOptions)
                throw new ArgumentException("Account-key source signing is supported for Azure Files sources only.");
            if (file)
            {
                if (SourceAccountKey is null && !UseSourceAccountKeyLookup)
                    throw new ArgumentException("Normal Files-to-Blob copy requires a source service SAS, SourceAccountKey, or explicit UseSourceAccountKeyLookup.");
                if (SourceAccountKey is not null && UseSourceAccountKeyLookup)
                    throw new ArgumentException("Choose a supplied source account key or ARM key lookup, not both.");
                if (!UseSourceAccountKeyLookup && (SourceKeyResourceGroup is not null || SourceKeySubscriptionId is not null || SourceKeyName != "key1"))
                    throw new ArgumentException("Source key lookup settings require explicit UseSourceAccountKeyLookup.");
                if (UseSourceAccountKeyLookup)
                {
                    _ = ListStorageAccountKeysProcessor.ValidateAddress(context.Profile, SourceKeySubscriptionId,
                        SourceKeyResourceGroup ?? "", context.StorageAccountName);
                    if (string.IsNullOrWhiteSpace(SourceKeyName))
                        throw new ArgumentException("Specify the source account key name to retrieve.");
                }
                else
                {
                    byte[] key = StorageCopyAuthorization.DecodeAccountKey(SourceAccountKey!);
                    CryptographicOperations.ZeroMemory(key);
                }
            }
        }
        if (file && SourceShareObject is not null && nativeShare is not null &&
            (nativeShare.Name != SourceShareObject.Name || nativeShare.ShareSnapshot != SourceShareObject.ShareSnapshot ||
             !SameContext(nativeShare.Context, SourceShareObject.Context)) ||
            !file && SourceContainerObject is not null && nativeContainer is not null &&
            (nativeContainer.Name != SourceContainerObject.Name || !SameContext(nativeContainer.Context, SourceContainerObject.Context)))
            throw new ArgumentException("Native copy source parents conflict.");
        string parent = file ? nativeShare?.Name ?? SourceShareName ?? "" : nativeContainer?.Name ?? SourceContainer ?? "";
        string directory = SourceDirectoryObject?.Path.Trim('/') ?? "";
        string path = file ? SourceFileObject?.Path ?? (directory.Length == 0 ? SourcePath ?? "" :
            directory + "/" + SourcePath?.Trim('/')) : SourceBlobObject?.Name ?? SourceBlob ?? "";
        if (file) path = path.TrimStart('/');
        if (publicUri is null)
        {
            if (path.Length == 0 || file && (SourceFileObject is null && string.IsNullOrEmpty(SourcePath) || path.EndsWith('/') ||
                    SourcePath?.EndsWith('/') == true) ||
                file && SourceShareName is not null && SourceShareName != parent ||
                !file && SourceContainer is not null && SourceContainer != parent ||
                file && SourceFileObject is not null && SourcePath is not null && SourcePath != path ||
                !file && SourceBlobObject is not null && SourceBlob is not null && SourceBlob != path)
                throw new ArgumentException("Specify a file/blob path consistent with its native source.");
            var resource = file ? context.GetFileUri(parent, path) : context.GetBlobUri(parent, path);
            publicUri = new UriBuilder(resource) { Query = publicQuery.ToString() }.Uri;
        }
        if (publicUri.AbsolutePath.IndexOf('/', 1) is var separator && (separator < 0 || separator == publicUri.AbsolutePath.Length - 1))
            throw new ArgumentException("The source URI must identify a file or blob.");
        if (!presigned)
        {
            foreach (string? key in query.AllKeys)
                if (!string.Equals(key, selector, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, "versionid", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Unsigned source URIs support resource selectors only.");
        }
        else
        {
            if (SourceSas is not null)
                foreach (string? key in publicQuery.AllKeys)
                {
                    if (query.GetValues(key) is not null && query[key] != publicQuery[key])
                        throw new ArgumentException("Source SAS selectors conflict with the selected resource.");
                    query[key] = publicQuery[key];
                }
            if (query["sig"]!.Any(character => character < 0x20 || character > 0x7e) ||
            string.IsNullOrEmpty(query["sv"]) || query["sp"]?.Contains('r') != true && string.IsNullOrEmpty(query["si"]) ||
            query[selector] != snapshot || query["versionid"] != version)
                throw new ArgumentException("The supplied source SAS must include a version and read permission or policy, and match the selected resource.");
            if (file && query["sks"] is not null)
                throw new ArgumentException("Normal Files-to-Blob copy requires a service or account SAS, not a Files user-delegation SAS.");
        }
        return new Source(publicUri, presigned ? new UriBuilder(publicUri) { Query = query.ToString() }.Uri : null, context, service);
    }

    private static NameValueCollection ParseSourceQuery(string value)
    {
        var query = HttpUtility.ParseQueryString(value);
        foreach (string? key in query.AllKeys)
            if (string.IsNullOrWhiteSpace(key) || query.GetValues(key)?.Length != 1)
                throw new ArgumentException("Source query parameters require unique, nonempty names.");
        return query;
    }

    private T SourceProcessor<T>(Source source, Action<T> configure) where T : StorageDataPlaneProcessorBase, new() =>
        CreateExtendedProcessor<T>(processor =>
        {
            processor.Context = source.Context; processor.Profile = source.Context.Profile.Name;
            processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
            configure(processor);
        });

    private async Task<string> ReadSourceTypeAsync(Source source)
    {
        var child = SourceProcessor<BlobProcessors.GetPropertiesBlobProcessor>(source, processor =>
        {
            processor.Url = source.PublicUri.AbsoluteUri; processor.Timeout = Timeout;
        });
        using var response = await child.SendAsync();
        string? type = response.Headers.TryGetValues("x-ms-blob-type", out var values) ? string.Join(",", values) : null;
        return type is "BlockBlob" or "PageBlob" or "AppendBlob" ? type :
            throw new InvalidDataException("Storage did not return a valid copy-source blob type.");
    }

    private async Task<Uri> AuthorizeSourceAsync(Source source)
    {
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        DateTimeOffset start = now.AddMinutes(-5), expiry = now.AddDays(1);
        if (source.Service == StorageService.Files)
        {
            string? accountKey = SourceAccountKey;
            if (UseSourceAccountKeyLookup)
            {
                StorageAccountKey[] keys = [];
                var lookup = CreateExtendedProcessor<ListStorageAccountKeysProcessor>(processor =>
                {
                    processor.Context = null; processor.Profile = source.Context.Profile.Name;
                    processor.AccountName = source.Context.StorageAccountName; processor.ResourceGroupName = SourceKeyResourceGroup!;
                    processor.SubscriptionId = SourceKeySubscriptionId;
                    processor.DeferFailureDiagnostics = DeferFailureDiagnostics;
                    processor.OutputOverride = (output, _) =>
                    {
                        keys = output is StorageAccountKey[] values ? values :
                            throw new InvalidDataException("Storage returned an invalid source key collection.");
                        return Task.CompletedTask;
                    };
                }, source.Context.Profile);
                await lookup.ProcessAsync();
                accountKey = keys.SingleOrDefault(key => string.Equals(key.KeyName, SourceKeyName, StringComparison.OrdinalIgnoreCase))?.Value
                    ?? throw new InvalidDataException("Storage did not return the requested source account key.");
            }
            Token.ThrowIfCancellationRequested();
            return StorageCopyAuthorization.SignFileServiceSas(source.PublicUri, source.Context, accountKey!, start, expiry);
        }
        var child = SourceProcessor<BlobProcessors.GetUserDelegationKeyServiceProcessor>(source, processor =>
        {
            processor.Url = source.Context.BlobEndpoint.AbsoluteUri; processor.Timeout = Timeout;
            processor.Start = StorageCopyAuthorization.Time(start); processor.Expiry = StorageCopyAuthorization.Time(expiry);
        });
        using var response = await child.SendAsync();
        if (response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException("Storage did not return a source delegation key.");
        var delegationKey = await StorageWireFormat.ReadResponseAsync<BlobModels.UserDelegationKey>(response, Token);
        try { return StorageCopyAuthorization.Sign(source.PublicUri, source.Context, source.Service, delegationKey, start, expiry); }
        finally { CryptographicOperations.ZeroMemory(delegationKey.Value); }
    }

    private static bool SameContext(StorageContext first, StorageContext second) =>
        first.StorageAccountName == second.StorageAccountName && first.Profile == second.Profile &&
        first.EnableFileBackupRequestIntent == second.EnableFileBackupRequestIntent;
    private static bool IsBlockTier(string? tier) => tier is not null && SetStorageBlobTierProcessor.BlockTiers.Contains(tier, StringComparer.Ordinal);
    private static bool IsPageTier(string? tier) => tier is not null && SetStorageBlobTierProcessor.PageTiers.Contains(tier, StringComparer.Ordinal);
}
