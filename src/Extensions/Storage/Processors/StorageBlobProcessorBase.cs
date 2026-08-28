// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Processors;

public abstract class StorageBlobProcessorBase : IProcessor
{
    private static readonly string[] s_storageScopes = ["https://storage.azure.com/.default"];
    private const string StorageApiVersion = "2023-11-03";

    protected IRuntimeHttpPipeline HttpPipeline { get; private set; } = null!;
    protected IEventPublisher Publisher { get; private set; } = null!;
    protected OutputStreamDelegate WriteOutput { get; private set; } = null!;
    protected Profile? ResolvedProfile { get; private set; }

    public string AccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string? Profile { get; set; }

    public IProcessor Init(ProcessorContext context)
    {
        HttpPipeline = context.HttpPipeline;
        Publisher = context.Publisher;
        WriteOutput = context.OutputHandler.WriteOutput;
        ResolvedProfile = context.Profile;
        return this;
    }

    public abstract Task ProcessAsync();

    protected T CreateExtendedProcessor<T>(Action<T>? configure = null)
        where T : StorageBlobProcessorBase, new()
    {
        T child = new()
        {
            HttpPipeline = HttpPipeline,
            Publisher = Publisher,
            WriteOutput = WriteOutput,
            ResolvedProfile = ResolvedProfile,
        };
        configure?.Invoke(child);
        return child;
    }

    protected HttpRequestMessage CreateRequest(HttpMethod method)
    {
        ValidateStorageTarget(requireBlob: true);
        return CreateRequest(method, BuildBlobUri());
    }

    protected HttpRequestMessage CreateContainerRequest(HttpMethod method, string query)
    {
        ValidateStorageTarget(requireBlob: false);
        return CreateRequest(method, new UriBuilder(BuildContainerUri()) { Query = query }.Uri);
    }

    protected HttpRequestMessage CreateRequest(HttpMethod method, Uri requestUri, bool requireProfile = true)
    {
        if (requireProfile && ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required. Run 'az login' or pass --profile.");
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Options.Set(RuntimeHttpRequestOptions.Scopes, s_storageScopes);
        request.Headers.TryAddWithoutValidation("x-ms-version", StorageApiVersion);
        request.Headers.TryAddWithoutValidation(
            "x-ms-date",
            DateTimeOffset.UtcNow.ToString("R", CultureInfo.InvariantCulture));
        return request;
    }

    protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, bool requireProfile = true)
    {
        if (requireProfile && ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required. Run 'az login' or pass --profile.");
        }

        HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
        await Publisher.Publish(Events.TracingHttpResponseMessage, response);
        return response;
    }

    protected static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : response.Content?.Headers.TryGetValues(name, out values) == true
                ? values.FirstOrDefault()
                : null;

    protected Uri BuildBlobUri()
    {
        string escapedBlobName = string.Join(
            "/",
            BlobName.Split('/', StringSplitOptions.None).Select(Uri.EscapeDataString));
        return new Uri(
            $"{BuildContainerUri()}/{escapedBlobName}",
            UriKind.Absolute);
    }

    private Uri BuildContainerUri() => new(
        $"https://{AccountName}.blob.core.windows.net/{Uri.EscapeDataString(ContainerName)}",
        UriKind.Absolute);

    protected void ValidateStorageTarget(bool requireBlob)
    {
        if (ResolvedProfile is null)
        {
            throw new InvalidOperationException("An active profile is required. Run 'az login' or pass --profile.");
        }

        if (AccountName.Length is < 3 or > 24
            || AccountName.Any(c => !(char.IsAsciiLetterLower(c) || c is >= '0' and <= '9')))
        {
            throw new ArgumentException("AccountName must contain 3-24 lowercase letters or digits.", nameof(AccountName));
        }

        if (ContainerName.Length is < 3 or > 63
            || !char.IsLetterOrDigit(ContainerName[0])
            || !char.IsLetterOrDigit(ContainerName[^1])
            || ContainerName.Contains("--", StringComparison.Ordinal)
            || ContainerName.Any(c => !(char.IsAsciiLetterLower(c) || c is >= '0' and <= '9' || c == '-')))
        {
            throw new ArgumentException("ContainerName is not a valid Azure Blob container name.", nameof(ContainerName));
        }

        if (requireBlob
            && (string.IsNullOrWhiteSpace(BlobName)
                || BlobName.Length > 1024
                || BlobName.Any(char.IsControl)))
        {
            throw new ArgumentException("BlobName must contain 1-1024 non-control characters.", nameof(BlobName));
        }
    }
}
