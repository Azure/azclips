// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Azclips.Extensions.Network.Processors
{
    /// <summary>
    /// Non-generic base holding the runtime wiring shared by every network processor, so that code
    /// needing to reference "any network processor" stays type-safe.
    /// All real processors should derive from <see cref="NetworkProcessorBase{TRequest, TResponse}"/>.
    /// </summary>
    public abstract partial class NetworkProcessorBase : IProcessor
    {
        /// <summary>Microsoft.Network ARM API version used by every processor in this extension.</summary>
        protected const string ApiVersion = "2025-07-01";

        public IRuntimeHttpPipeline HttpPipeline = null!;
        public IEventPublisher Publisher = null!;
        public JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected IAuthHandler AuthHandler = null!;

        /// <summary>The resolved connection profile from the processor context; null when none is available.</summary>
        protected ProfileModel? ResolvedProfile = null!;

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

            // TODO(Yabo): temporarily initialize arbitrary json options here, get from context in the future
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;
            ResolvedProfile = context.Profile;
            AuthHandler = context.AuthHandler;

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
        /// Adds request headers beyond what the pipeline supplies (e.g. Graph's <c>ConsistencyLevel</c>).
        /// No-op by default; override to mutate <paramref name="request"/> for header-carrying operations.
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

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            return await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
        }

        internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            return await HttpRuntime.SendAsyncLRO(CreateRequestMessage, Publisher, HttpPipeline, GetFinalUri, GetOperationLocationUri, GetAsyncOperationUri, GetLocationUri);
        }
    }

    /// <summary>
    /// Generic network processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishs start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishs end.
    /// </summary>
    public partial class NetworkProcessorBase<TRequest, TResponse> : NetworkProcessorBase
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Typed request-body seam. Returns null (no body) by default; write processors override to return
        /// their <typeparamref name="TRequest"/> model. Covariantly overrides the non-generic
        /// <see cref="NetworkProcessorBase.CreateRequestBody"/> that the request template consumes.
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
        
        // Yabo: C# does not allow async task to have ref parameter
        // this override likely needs to perform write output, thus likely to be async
        // return Task<bool> instead
        protected virtual Task<bool> BeforeWriteOutputAsyncAndReturn(TResponse? body) => Task.FromResult(false);

        public override async Task ProcessAsync()
        {
            try
            {
                await BeforeProcessAsync();
                await Publisher.Publish(Events.ProcessorProcessAsyncStart);
                using HttpResponseMessage response = await SendAsync();
                string? rawBody = null;
                TResponse? body = default;

                if (response.Content is null) await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                else if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync())) await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                else
                {
                    try
                    {
                        body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
                    }
                    catch (JsonException)
                    {
                        await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}");
                    }
                }          
                if (!await BeforeWriteOutputAsyncAndReturn(body) && body is not null)
                {
                    await WriteOutput(body);
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}