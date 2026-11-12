// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.Resources.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any resources processor" stays type-safe.
    /// All real processors should derive from <see cref="ResourcesProcessorBase{TResponse}"/>.
    /// </summary>
    public abstract class ResourcesProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        /// <summary>The resolved connection profile from the processor context; null when none is available.</summary>
        protected ProfileModel? ResolvedProfile;

        /// <summary>
        /// Bound <c>--profile</c>/<c>-Profile</c> parameter. The Engine resolves the named profile into
        /// <see cref="ResolvedProfile"/> before the processor runs, so this property exists only to accept
        /// the value during argument binding (per-processor workaround until a global-parameter framework exists).
        /// </summary>
        public string? Profile { get; set; }

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            // TODO: temporarily initialize arbitrary json options here, get from context in the future
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;
            ResolvedProfile = context.Profile;

            AfterInit();
            return this;
        }
        public virtual Task ProcessAsync() => Task.CompletedTask;

        /// <summary>HTTP method for the request. Every processor must override this (GET/PUT/DELETE/etc.).</summary>
        protected virtual HttpMethod Method => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(Method)}.");

        /// <summary>Builds the request URI. Override this seam alone for most read/delete operations.</summary>
        protected virtual string CreateRequestUri() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestUri)}.");

        /// <summary>
        /// Builds the request body, serialized with <see cref="JsonOptions"/> using its runtime type.
        /// Returns null (no content) by default; override for create/update operations.
        /// </summary>
        protected virtual object? CreateRequestBody() => null;

        /// <summary>
        /// Adds request headers beyond what the pipeline supplies. No-op by default; override to mutate
        /// <paramref name="request"/> for header-carrying operations.
        /// </summary>
        protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }

        /// <summary>
        /// Assembles the request from <see cref="Method"/>, <see cref="CreateRequestUri"/>, <see cref="CreateRequestBody"/>
        /// and <see cref="CreateRequestHeaders"/>, and publishes the before/after create events. Override the smaller
        /// seams above so this event wiring stays centralized; override this method only when a request needs fully custom construction.
        /// </summary>
        protected virtual async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);
            HttpRequestMessage request = new HttpRequestMessage(Method, CreateRequestUri());
            if (CreateRequestBody() is { } body)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
            }
            CreateRequestHeaders(request);
            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, ResolvedProfile)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : ResourcesProcessorBase, new()
        {
            T child = new T
            {
                HttpPipeline = HttpPipeline,
                Publisher = Publisher,
                JsonOptions = JsonOptions,
            };
            child.WriteOutput = WriteOutput;
            child.ResolvedProfile = ResolvedProfile;
            configure?.Invoke(child);
            return child;
        }

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            return await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
        }

        /*
            For LRO API has
                "x-ms-long-running-operation-options": {
                "final-state-via": "location/azure-async-operation"
                }
            "finaluri" is response.headers["Location"]
            "operationLocationUri" is response.headers["Operation-Location"]
            otherwise both null
        */
        protected virtual Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetLocationUri => _ => null;

        /// <summary>
        /// Long-running-operation transport: issues the request, then polls the ARM async-operation /
        /// location / operation-location URIs until the resource reaches a terminal provisioningState
        /// (Succeeded/Canceled) or the operation fails. Mirrors the proven Compute poller. Atomic LRO
        /// processors override <see cref="SendAsync"/> to delegate here.
        /// </summary>
        internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            return await HttpRuntime.SendAsyncLRO(CreateRequestMessage, Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri);
        }
    }

    /// <summary>
    /// Generic resources processor base. ProcessAsync owns lifecycle events; ExecuteAsync
    /// handles a single response by default. Pageable processors override execution and page reading.
    /// </summary>
    public abstract partial class ResourcesProcessorBase<TRequest, TResponse> : ResourcesProcessorBase
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Typed request-body seam. Returns null (no body) by default; write processors override to return
        /// their <typeparamref name="TRequest"/> model. Covariantly overrides the non-generic
        /// <see cref="ResourcesProcessorBase.CreateRequestBody"/> that the request template consumes.
        /// </summary>
        protected override TRequest? CreateRequestBody() => default;

        // Yabo: C# does not allow partial with return type to be unimplemented
        // So partial Task is forbidden, have to make it synchronous.
        partial void OverrideBeforeProcessBase();
        protected virtual Task BeforeProcessAsync()
        {
            OverrideBeforeProcessBase();
            return Task.CompletedTask;
        }

        protected virtual Task<bool> BeforeWriteOutputAsyncAndReturn(TResponse? body) => Task.FromResult(false);

        protected virtual Task<TResponse?> DeserializeResponseAsync(string rawBody) =>
            Task.FromResult(JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions));

        protected async Task<TResponse?> ReadResponseAsync()
        {
            Publisher.Token.ThrowIfCancellationRequested();
            using HttpResponseMessage response = await SendAsync();
            return await ReadResponseAsync(response);
        }

        private async Task<TResponse?> ReadResponseAsync(HttpResponseMessage response)
        {
            string? rawBody = null;
            TResponse? body = default;
            if (response.Content is null) await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
            else if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync())) await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
            else
            {
                try
                {
                    body = await DeserializeResponseAsync(rawBody);
                }
                catch (JsonException ex)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}", ex);
                }
            }
            return body;
        }

        /// <summary>Reads pages on demand using the execution publisher's cancellation token.</summary>
        protected virtual async IAsyncEnumerable<TResponse> ReadPagesAsync()
        {
            if (await ReadResponseAsync() is { } body)
            {
                yield return body;
            }
        }

        /// <summary>Collects all page items before one write. Override ExecuteAsync for other consumption policies.</summary>
        protected async Task ExecutePagesAsync<TItem>(Func<TResponse, IEnumerable<TItem>?> pageItems)
        {
            List<TItem> items = await ReadPagesAsync().CollectItemsAsync(pageItems);
            await WriteOutput(items);
        }

        protected virtual async Task ExecuteAsync()
        {
            using HttpResponseMessage response = await SendAsync();
            TResponse? body = await ReadResponseAsync(response);
            if (!await BeforeWriteOutputAsyncAndReturn(body) && body is not null)
            {
                await WriteOutput(body);
            }
        }

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                await ExecuteAsync();
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}