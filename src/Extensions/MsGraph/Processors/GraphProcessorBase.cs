// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any graph processor" stays type-safe.
    /// All real processors should derive from <see cref="GraphProcessorBase{TRequest, TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// The Microsoft Graph token-audience override is installed via a partial-method injection
    /// point on the generic base — see <c>Customization/GraphProcessorBase.cs</c>. That keeps the
    /// hand-authored auth hook out of the codegen path.
    /// </remarks>
    public abstract class GraphProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected Profile Profile = null!;
        protected IAuthHandler AuthHandler = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

            // TODO: temporarily initialize arbitrary json options here, get from context in the future
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            HttpPipeline = context.HttpPipeline;
            Publisher = context.Publisher;
            WriteOutput = context.OutputHandler.WriteOutput;
            AuthHandler = context.AuthHandler;
            Profile = AuthHandler.Profile
                ?? throw new InvalidOperationException(
                    "Microsoft Graph commands require an active profile. Sign in first.");

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;

        /// <summary>HTTP method for the request. Every processor must override this (GET/POST/DELETE/etc.).</summary>
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

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, Profile, AuthHandler)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : GraphProcessorBase, new()
        {
            T child = new T
            {
                HttpPipeline = HttpPipeline,
                Publisher = Publisher,
                JsonOptions = JsonOptions,
            };
            child.WriteOutput = WriteOutput;
            child.Profile = Profile;
            child.AuthHandler = AuthHandler;
            configure?.Invoke(child);
            return child;
        }

        // Yabo: Pure data plane probably doesn't need LRO URIs, will defer when working on data plane base
        protected virtual Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetLocationUri => _ => null;

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            return await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
        }

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
    /// Generic graph processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishes start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishes end.
    /// <para>
    /// <see cref="SendAsync"/> is the internal "give me the final response" transport primitive.
    /// Atomic processors don't override it (the inherited <see cref="SendAsync"/> dispatches a
    /// single HTTP request). Graph reads are simple GETs with no long-running-operation polling.
    /// </para>
    /// </summary>
    public abstract partial class GraphProcessorBase<TRequest, TResponse> : GraphProcessorBase
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Typed request-body seam. Returns null (no body) by default; write processors override to return
        /// their <typeparamref name="TRequest"/> model. Covariantly overrides the non-generic
        /// <see cref="GraphProcessorBase.CreateRequestBody"/> that the request template consumes.
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
                    catch (JsonException ex)
                    {
                        await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                        throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}", ex);
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
