// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.Sql.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any sql processor" stays type-safe.
    /// All real processors should derive from <see cref="SqlProcessorBase{TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// TODO(Yabo): no rollback / compensation story yet. If an orchestrator's first child
    /// succeeds and a later child fails, earlier resources are orphaned. Document as a non-goal
    /// for now; revisit with an OnFailureAsync hook or a saga-style step list when needed.
    /// </remarks>
    public abstract class SqlProcessorBase : IProcessor
    {
        public IRuntimeHttpPipeline HttpPipeline = null!;
        public IEventPublisher Publisher = null!;
        public JsonSerializerOptions JsonOptions = null!;
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

            AfterInit();
            return this;
        }
        public virtual Task ProcessAsync() => Task.CompletedTask;
        protected virtual Task<HttpRequestMessage> CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, ResolvedProfile)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : SqlProcessorBase, new()
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
    /// Generic sql processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishes start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishes end.
    /// <para>
    /// <see cref="SendAsync"/> is the internal "give me the final response" transport primitive.
    /// Atomic LRO processors override it as a one-liner that delegates to <see cref="SqlProcessorBase.SendAsyncLRO"/>.
    /// Atomic non-LRO processors don't override at all (the inherited <see cref="SendAsync"/>
    /// resolves <see cref="SubscriptionId"/> then dispatches a single HTTP request).
    /// Orchestrators override <see cref="SendAsync"/> to compose children.
    /// </para>
    /// <para>
    /// <see cref="SubscriptionId"/> is resolved lazily inside <see cref="SendAsync"/> /
    /// <see cref="SendAsyncLRO"/>, so internal composition paths that call these helpers directly still get a
    /// populated value via <see cref="ResolvedProfile"/> fallback. Orchestrators don't need to copy
    /// <see cref="SubscriptionId"/> into children since each child resolves from the shared
    /// <see cref="ResolvedProfile"/>.
    /// </para>
    /// </summary>
    public class SqlProcessorBase<TResponse> : SqlProcessorBase where TResponse : class
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Resolves <see cref="SubscriptionId"/> from the property if set, otherwise from <see cref="ResolvedProfile"/>.
        /// Throws if neither has a value. Idempotent. Called automatically before any HTTP transport;
        /// custom <see cref="SendAsync"/>/<see cref="SendAsyncLRO"/> overrides that don't call <c>base</c>
        /// are responsible for invoking this themselves if they build URIs from <see cref="SubscriptionId"/>.
        /// </summary>
        protected void ResolveSubscriptionId() =>
            SubscriptionId = !string.IsNullOrEmpty(SubscriptionId)
                ? SubscriptionId
                : ResolvedProfile?.SubscriptionId
                  ?? throw new InvalidOperationException("SubscriptionId must be provided either via property or an active profile.");

        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return base.SendAsync();
        }

        internal override Task<HttpResponseMessage> SendAsyncLRO()
        {
            ResolveSubscriptionId();
            return base.SendAsyncLRO();
        }

        /// <summary>
        /// Projects the deserialized response into the value written to output. Default writes the
        /// response as-is; list processors override this to unwrap the ARM envelope and write its
        /// <c>value</c> collection (a flat array), which PowerShell enumerates and the CLI serializes.
        /// </summary>
        protected virtual object SelectOutput(TResponse body) => body;

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            using HttpResponseMessage response = await SendAsync();

            if (response.Content is null)
            {
                await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
                return;
            }

            string rawBody = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(rawBody))
            {
                await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
                return;
            }

            try
            {
                TResponse? body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
                if (body is not null)
                {
                    await WriteOutput(SelectOutput(body));
                }
            }
            catch (JsonException)
            {
                await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}");
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}
