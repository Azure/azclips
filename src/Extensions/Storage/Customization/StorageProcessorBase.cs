// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.Storage.Customization;

public abstract class StorageProcessorBase : IProcessor
{
    private ProcessorContext processorContext = null!;
    protected IEventPublisher Publisher => processorContext.Publisher;
    protected IRuntimeHttpPipeline HttpPipeline => processorContext.HttpPipeline;
    protected Core.Abstractions.Profile? ResolvedProfile => processorContext.Profile;
    internal OutputStreamDelegate? OutputOverride { get; set; }
    internal bool SuppressProgress { get; set; }
    protected OutputStreamDelegate WriteOutput => OutputOverride ?? processorContext.OutputHandler.WriteOutput;
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string? Profile { get; set; }
    public StorageContext? Context { get; set; }
    internal RequestAttemptState? AttemptState { get; set; }
    internal bool DeferFailureDiagnostics { get; set; }
    public CancellationToken Token => Publisher.Token;
    public Action Cancel => Publisher.Cancel;
    protected abstract StorageService Service { get; }
    protected abstract HttpMethod Method { get; }
    protected virtual string? ResourceUrl => null;
    protected virtual void BeforeInit() { }
    protected virtual void AfterInit() { }

    public IProcessor Init(ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        BeforeInit();
        processorContext = context;
        AfterInit();
        return this;
    }

    public abstract Task ProcessAsync();
    protected async Task ProcessWorkflowAsync(Func<Task> execute)
    {
        try
        {
            Token.ThrowIfCancellationRequested();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            await execute();
        }
        catch (HttpRequestException exception) when (!DeferFailureDiagnostics && exception.Data["FailureDiagnosticsDeferred"] is true)
        {
            string message = exception.Message;
            if (exception.Data["ServiceErrorCode"] is string code && code.Length is > 0 and <= 128 &&
                code.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                message += $" Storage error code: {code}.";
            if (exception.Data["CopySourceErrorCode"] is string sourceCode && sourceCode.Length is > 0 and <= 128 &&
                sourceCode.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                message += $" Copy source error code: {sourceCode}.";
            if (exception.Data["CopySourceStatusCode"] is int status && status is >= 100 and <= 599)
                message += $" Copy source HTTP status: {status}.";
            if (exception.Data["ServiceRequestId"] is string value && Guid.TryParse(value, out var requestId))
                message += $" Request ID: {requestId}.";
            await Publisher.Publish(Events.ResourceOperationFailed, message);
            throw;
        }
        catch (InvalidDataException exception) when (!DeferFailureDiagnostics)
        {
            await Publisher.Publish(Events.ResourceOperationFailed, exception.Message);
            throw;
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    protected abstract string CreateRequestUri();
    protected virtual object? CreateRequestBody() => null;
    protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }

    protected T CreateExtendedProcessor<T>(Action<T>? configure = null, Core.Abstractions.Profile? profile = null)
        where T : StorageProcessorBase, new()
    {
        T child = new() { Profile = Profile, Context = Context, SuppressProgress = SuppressProgress };
        child.Init(profile is null ? processorContext : processorContext with { Profile = profile });
        configure?.Invoke(child);
        return child;
    }

    protected virtual async Task<HttpRequestMessage> CreateRequestMessage()
    {
        Token.ThrowIfCancellationRequested();
        var profile = Context?.Profile ?? ResolvedProfile;
        StorageContext.ValidateProfile(profile);
        if (Profile is not null && !string.Equals(Profile, profile!.Name, StringComparison.Ordinal))
            throw new ArgumentException("The selected profile conflicts with the Storage context profile.");

        Uri? resource = null;
        if (Service != StorageService.Management)
        {
            if (!Uri.TryCreate(ResourceUrl, UriKind.Absolute, out resource))
                throw new ArgumentException("Specify a valid Storage resource URL.");
            string account = StorageContext.ValidatePublicUri(resource, Service);
            if (Context is not null && account != Context.StorageAccountName)
                throw new ArgumentException("The request must target the selected Storage context's account.");
            if (Service == StorageService.Files && Context?.EnableFileBackupRequestIntent != true)
                throw new ArgumentException("Azure Files OAuth requires explicit EnableFileBackupRequestIntent.");
        }

        await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
        string generatedRequest = CreateRequestUri();
        var uri = resource is null ? new Uri(generatedRequest, UriKind.Absolute) : StorageContext.MergeResourceQuery(resource, generatedRequest);
        if (Service == StorageService.Management &&
            (uri.Scheme != Uri.UriSchemeHttps || uri.IdnHost != "management.azure.com" || uri.Port != 443 ||
             uri.UserInfo.Length != 0 || uri.Fragment.Length != 0))
            throw new ArgumentException("ARM requests must target the Azure public-cloud management endpoint.");
        if (Service != StorageService.Management)
            StorageContext.ValidatePublicUri(uri, Service);

        var request = new HttpRequestMessage(Method, uri);
        try
        {
            request.MarkSensitive();
            request.Options.Set(RequestAuthentication.OptionsKey, new RequestAuthentication(profile!,
                [Service == StorageService.Management ? "https://management.azure.com/.default" : "https://storage.azure.com/.default"]));
            if (AttemptState is not null)
                request.Options.Set(RequestAttemptState.OptionsKey, AttemptState);
            object? body = CreateRequestBody();
            request.Content = Service == StorageService.Management
                ? body is null ? null : JsonContent.Create(body, body.GetType(), options: JsonOptions)
                : StorageWireFormat.CreateContent(body);
            CreateRequestHeaders(request);
            if (Service == StorageService.Files)
            {
                if (request.Headers.TryGetValues("x-ms-file-request-intent", out var intents) &&
                    !intents.SequenceEqual(["backup"], StringComparer.Ordinal))
                    throw new ArgumentException("Azure Files OAuth supports backup intent only.");
                request.Headers.Remove("x-ms-file-request-intent");
                request.Headers.Add("x-ms-file-request-intent", "backup");
            }
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    protected static void SetContentHeader(HttpRequestMessage request, string name, string value)
    {
        request.Content ??= new ByteArrayContent([]);
        if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
            (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length) ||
             length != request.Content.Headers.ContentLength))
            throw new ArgumentException("The declared request length must equal the binary body length.");
        request.Content.Headers.Remove(name);
        if (!request.Content.Headers.TryAddWithoutValidation(name, value))
            throw new ArgumentException("The Storage request contains an invalid content header.");
    }

    protected static void AddMetadataHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null) return;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$") ||
                !keys.Add(key) || value is null || value.Any(character => character < 0x20 || character > 0x7e))
                throw new ArgumentException("Storage metadata requires identifier keys and printable ASCII values.");
            request.Headers.Add("x-ms-meta-" + key, value);
        }
    }

    internal virtual Task<HttpResponseMessage> SendAsync() =>
        HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline, reportFailure: !DeferFailureDiagnostics);

    protected async Task ProcessResponseAsync<TResponse>(Func<Task> beforeProcess, Func<TResponse?, Task<bool>> beforeOutput)
        where TResponse : class
    {
        try
        {
            await beforeProcess();
            Token.ThrowIfCancellationRequested();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            using var response = await SendAsync();
            object? result;
            if (Service == StorageService.Management)
            {
                try
                {
                    result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, Token)
                        ?? throw new InvalidDataException("Storage returned an empty management response.");
                }
                catch (JsonException)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, "Storage returned invalid JSON. Sensitive response details are omitted.");
                    throw new InvalidDataException("Storage returned invalid JSON. Sensitive response details are omitted.");
                }
            }
            else if (typeof(TResponse) == typeof(object))
                result = Method == HttpMethod.Delete ? null : StorageResourceProperties.FromResponse(response);
            else
                result = await StorageWireFormat.ReadResponseAsync<TResponse>(response, Token);

            if (!await beforeOutput((TResponse?)result) && result is not null)
                await WriteOutput(result, Token);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
