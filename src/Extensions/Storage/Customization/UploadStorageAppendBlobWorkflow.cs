// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using BlobProcessors = Microsoft.Azure.Azclips.Extensions.Storage.Blob.Processors;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public sealed class UploadStorageAppendBlobWorkflow : StorageDataPlaneProcessorBase, IBlobUploadProcessor
{
    public string? StorageAccountName { get; set; }
    public string? Container { get; set; }
    public StorageBlobContainer? ContainerObject { get; set; }
    public int? Timeout { get; set; }
    public string? Blob { get; set; }
    public StorageBlob? InputObject { get; set; }
    public string? Snapshot { get; set; }
    public string? VersionId { get; set; }
    public string? LeaseId { get; set; }
    public string? IfMatch { get; set; }
    public string? IfNoneMatch { get; set; }
    public DateTimeOffset? IfModifiedSince { get; set; }
    public DateTimeOffset? IfUnmodifiedSince { get; set; }
    public string? IfTags { get; set; }
    public string File { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public bool ValidateContent { get; set; }
    public int Concurrency { get; set; } = 2;
    public int ChunkSize { get; set; } = StorageWireFormat.MaximumChunkBytes;
    public string? ContentType { get; set; }
    public string? ContentEncoding { get; set; }
    public string? ContentLanguage { get; set; }
    public string? ContentDisposition { get; set; }
    public string? CacheControl { get; set; }
    public byte[]? ContentMd5 { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    public IReadOnlyDictionary<string, string>? Tags { get; set; }
    public string? Tier { get; set; }
    internal Func<string, string, bool> ShouldProcess { get; set; } = (_, _) => true;
    protected override StorageService Service => StorageService.Blob;
    public override Task ProcessAsync() => ProcessWorkflowAsync(ExecuteAsync);

    public long? MaxSizeCondition { get; set; }
    internal bool SkipEmptyUpload { get; set; }

    private async Task ExecuteAsync()
    {
        var (target, sourcePath, tags, _) = BlobUploadOperations.ResolveUpload(this, this, StorageWireFormat.MaximumChunkBytes, [], appendToExisting: true);
        if (MaxSizeCondition is < 0) throw new ArgumentException("The maximum append size must be nonnegative.");
        if (!ShouldProcess(target.Url, "Upload Append Blob")) return;
        await using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1, FileOptions.Asynchronous | FileOptions.RandomAccess);
        long length = source.Length;
        // The legacy CLI SDK returns an empty result without contacting Storage for an empty append upload.
        if (SkipEmptyUpload && length == 0)
        {
            Token.ThrowIfCancellationRequested();
            await WriteOutput(new StorageAppendUploadNoOp(), Token);
            return;
        }
        long blocks = length == 0 ? 0 : (length - 1) / ChunkSize + 1;
        if (blocks > 50000 || MaxSizeCondition is { } maximum && length > maximum)
            throw new ArgumentException("The source exceeds the append block-count or maximum-size limit.");

        AppendState? state = null;
        if (!Overwrite)
        {
            try
            {
                var properties = BlobTargetOperations.PropertiesProcessor(this, target);
                properties.DeferFailureDiagnostics = true;
                using var response = await properties.SendAsync();
                state = ReadState(response);
            }
            catch (HttpRequestException exception) when (BlobTargetOperations.IsMissingBlob(exception))
            {
                // Only a missing blob permits creation; a missing container or denied request remains an error.
            }
        }
        if (state is not null && (blocks > 50000 - state.Blocks ||
            length > long.MaxValue - state.Length || MaxSizeCondition is { } limit && length > limit - state.Length))
            throw new ArgumentException("The existing blob plus source exceeds the append block-count or maximum-size limit.");
        bool created = state is null;
        if (created)
        {
            var create = BlobContainerOperations.CreateProcessor<BlobProcessors.CreateAppendBlobProcessor>(this, target.Container, processor =>
            {
                processor.Url = target.Url; processor.Timeout = Timeout; processor.BlobType = "AppendBlob"; processor.ContentLength = 0;
                processor.BlobContentType = ContentType; processor.BlobContentEncoding = ContentEncoding;
                processor.BlobContentLanguage = ContentLanguage; processor.BlobContentDisposition = ContentDisposition;
                processor.BlobCacheControl = CacheControl; processor.BlobContentMd5 = ContentMd5;
                processor.Metadata = Metadata; processor.BlobTagsString = tags;
                processor.LeaseId = LeaseId; processor.IfMatch = IfMatch; processor.IfNoneMatch = Overwrite ? IfNoneMatch : "*";
                processor.IfModifiedSince = BlobTargetOperations.Date(IfModifiedSince); processor.IfUnmodifiedSince = BlobTargetOperations.Date(IfUnmodifiedSince);
                processor.IfTags = IfTags;
            });
            using var response = await create.SendAsync();
            state = new AppendState(0, 0, BlobUploadOperations.RequiredEtag(response), BlobUploadOperations.ReadVersionId(response));
        }
        else if (Metadata is not null || Tags is not null || ContentType is not null || ContentEncoding is not null ||
            ContentLanguage is not null || ContentDisposition is not null || CacheControl is not null || ContentMd5 is not null)
            await Publisher.Publish(Events.Verbose, "Appending retains the existing blob's content settings, metadata and tags.");

        var current = state ?? throw new InvalidOperationException("Append Blob initialization did not produce a resource state.");
        long initialLength = current.Length;
        for (long offset = 0; offset < length;)
        {
            Token.ThrowIfCancellationRequested();
            byte[] bytes = await StorageWireFormat.ReadFileChunkAsync(source, offset, (int)Math.Min(ChunkSize, length - offset), Token);
            var attempts = new RequestAttemptState();
            var append = BlobContainerOperations.CreateProcessor<BlobProcessors.AppendBlockAppendBlobProcessor>(this, target.Container, processor =>
            {
                processor.Url = target.Url; processor.Timeout = Timeout; processor.Body = bytes; processor.ContentLength = bytes.Length;
                processor.AppendPosition = current.Length; processor.MaxSize = MaxSizeCondition;
                processor.IfMatch = current.ETag; processor.LeaseId = LeaseId;
                processor.TransactionalContentMd5 = ValidateContent ? StorageWireFormat.ContentMd5(bytes) : null;
                processor.AttemptState = attempts;
                processor.DeferFailureDiagnostics = true;
            });
            try
            {
                using var response = await append.SendAsync();
                if (HeaderLong(response, "x-ms-blob-append-offset") != current.Length ||
                    HeaderLong(response, "x-ms-blob-committed-block-count") != current.Blocks + 1)
                    throw new InvalidDataException("Storage returned unexpected append position or block-count headers.");
                current = new AppendState(current.Length + bytes.Length, current.Blocks + 1,
                    BlobUploadOperations.RequiredEtag(response), BlobUploadOperations.ReadVersionId(response));
            }
            catch (HttpRequestException exception) when (!Token.IsCancellationRequested && MayHaveCompleted(exception, attempts))
            {
                current = await ReconcileAsync(target, current, bytes);
            }
            offset += bytes.Length;
            await BlobUploadOperations.PublishUploadProgressAsync(this, offset, length, completed: false);
        }
        Token.ThrowIfCancellationRequested();
        if (source.Length != length) throw new IOException("The source file length changed during upload.");
        await BlobUploadOperations.FinishUploadAsync(this, target, initialLength + length, current.ETag, "AppendBlob", current.VersionId,
            includeRequestedTags: created);
    }

    private async Task<AppendState> ReconcileAsync(BlobTarget target, AppendState previous, byte[] bytes)
    {
        var properties = BlobContainerOperations.CreateProcessor<BlobProcessors.GetPropertiesBlobProcessor>(this, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.LeaseId = LeaseId;
        });
        AppendState observed;
        using (var response = await properties.SendAsync()) observed = ReadState(response);
        long end = previous.Length + bytes.Length;
        if (observed.Length != end || observed.Blocks != previous.Blocks + 1 || observed.ETag == previous.ETag)
            throw new InvalidDataException("The ambiguous append could not be verified; the upload is incomplete.");
        var download = BlobContainerOperations.CreateProcessor<BlobProcessors.DownloadBlobProcessor>(this, target.Container, processor =>
        {
            processor.Url = target.Url; processor.Timeout = Timeout; processor.LeaseId = LeaseId;
            processor.IfMatch = observed.ETag;
            processor.Range = string.Create(CultureInfo.InvariantCulture, $"bytes={previous.Length}-{end - 1}");
        });
        using var content = await download.SendAsync();
        var range = content.Content.Headers.ContentRange;
        if (content.StatusCode != HttpStatusCode.PartialContent || range?.Unit != "bytes" ||
            range.From != previous.Length || range.To != end - 1 || range.Length != end ||
            content.Content.Headers.ContentLength != bytes.Length || content.Headers.ETag?.ToString() != observed.ETag)
            throw new InvalidDataException("Storage returned an invalid append-verification range.");
        byte[] actual = await StorageWireFormat.ReadChunkAsync(content.Content, Token);
        if (!actual.AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("The ambiguous append bytes did not match; the upload is incomplete.");
        await Publisher.Publish(Events.Verbose, "Verified an ambiguous append using pinned blob length, block count and range bytes.");
        return observed;
    }

    private static bool MayHaveCompleted(HttpRequestException exception, RequestAttemptState attempts) =>
        attempts.Attempts > 0 && (exception.StatusCode is null || exception.StatusCode == HttpStatusCode.RequestTimeout ||
            exception.StatusCode >= HttpStatusCode.InternalServerError &&
            exception.StatusCode is not (HttpStatusCode.NotImplemented or HttpStatusCode.HttpVersionNotSupported) ||
            attempts.HadAmbiguousResponse && exception.StatusCode == HttpStatusCode.PreconditionFailed &&
            exception.Data["ServiceErrorCode"] is "ConditionNotMet" or "AppendPositionConditionNotMet");

    private static AppendState ReadState(HttpResponseMessage response)
    {
        var properties = StorageResourceProperties.FromResponse(response);
        if (properties.ContentLength is not { } length || length < 0 ||
            !properties.Headers.TryGetValue("x-ms-blob-type", out string? type) || type != "AppendBlob")
            throw new InvalidDataException("The selected resource is not a valid Append Blob.");
        long blocks = HeaderLong(response, "x-ms-blob-committed-block-count");
        if (blocks > 50000) throw new InvalidDataException("Storage returned an invalid append block count.");
        return new AppendState(length, (int)blocks, BlobUploadOperations.RequiredEtag(response), BlobUploadOperations.ReadVersionId(response));
    }

    private static long HeaderLong(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            string[] items = values.Take(2).ToArray();
            if (items.Length == 1 && long.TryParse(items[0], NumberStyles.None, CultureInfo.InvariantCulture, out long value))
                return value;
        }
        throw new InvalidDataException("Storage returned an invalid append position or block-count header.");
    }

    private sealed record AppendState(long Length, int Blocks, string ETag, string? VersionId);
}

internal sealed record StorageAppendUploadNoOp;
