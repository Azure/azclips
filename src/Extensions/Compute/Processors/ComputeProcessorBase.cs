// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;
using Core.Abstractions.Runtime.Handler;

namespace Microsoft.Azure.Azclips.Extensions.Compute.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any compute processor" stays type-safe.
    /// All real processors should derive from <see cref="ComputeProcessorBase{TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// TODO(Yabo): no rollback / compensation story yet. If an orchestrator's first child
    /// succeeds and a later child fails, earlier resources are orphaned. Document as a non-goal
    /// for now; revisit with an OnFailureAsync hook or a saga-style step list when needed.
    /// </remarks>
    public abstract partial class ComputeProcessorBase : IProcessor
    {
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

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, ResolvedProfile, AuthHandler)
        /// as this processor. Use the optional <paramref name="configure"/> action to set the
        /// extended processor's own properties.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : ComputeProcessorBase, new()
        {
            T child = new T
            {
                HttpPipeline = HttpPipeline,
                Publisher = Publisher,
                JsonOptions = JsonOptions,
                AuthHandler = AuthHandler,
            };
            child.WriteOutput = WriteOutput;
            child.ResolvedProfile = ResolvedProfile;
            configure?.Invoke(child);
            return child;
        }

        protected virtual string ResourceIdProvider => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(ResourceIdProvider)}.");

        protected virtual string ResourceIdType => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(ResourceIdType)}.");

        protected static bool HasMultipleResourceIds(string[]? ids) => ids is { Length: > 1 };

        protected virtual void SetResourceIdArguments(string subscriptionId, string resourceGroup, string name) { }

        protected void ApplyResourceId(string[]? ids)
        {
            if (ids is null || ids.Length == 0)
            {
                return;
            }

            var (subscriptionId, resourceGroup, name) = ParseResourceId(ids[0]);
            SetResourceIdArguments(subscriptionId, resourceGroup, name);
        }

        protected static void ValidateResourceIdArguments(string? resourceGroup, string? name, string[]? ids)
        {
            if (ids is { Length: > 0 })
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(resourceGroup))
            {
                throw new InvalidOperationException("--resource-group is required unless --ids is provided.");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("--name is required unless --ids is provided.");
            }
        }

        protected (string SubscriptionId, string ResourceGroup, string Name) ParseResourceId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidOperationException("Resource id cannot be empty.");
            }

            string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 8
                || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
                || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
                || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
                || !segments[5].Equals(ResourceIdProvider, StringComparison.OrdinalIgnoreCase)
                || !segments[6].Equals(ResourceIdType, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/{ResourceIdProvider}/{ResourceIdType}/{{name}}.");
            }

            return (segments[1], segments[3], segments[7]);
        }

        // ── Parallel --ids fan-out ──

        protected virtual int ResourceIdFanOutLimit => 10;

        /// <summary>
        /// Builds a processor targeting one resource id. Each id gets its own instance so the target
        /// fields are never shared, which is what makes the fan-out safe to run concurrently.
        /// Processors that accept <c>--ids</c> override this via <see cref="CreateExtendedProcessor{T}"/>.
        /// </summary>
        protected virtual ComputeProcessorBase CreateResourceIdChild(string subscriptionId, string resourceGroup, string name)
            => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateResourceIdChild)}.");

        /// <summary>
        /// Runs one child per resource id, up to <see cref="ResourceIdFanOutLimit"/> at a time, and
        /// reports every failure together once they have all been attempted.
        /// </summary>
        protected async Task ProcessResourceIdsAsync(string[] ids)
        {
            // Parse every id before issuing any request: a malformed id is a typo, and rejecting the
            // whole command is better than acting on an arbitrary subset of the resources.
            var targets = new (string SubscriptionId, string ResourceGroup, string Name)[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                targets[i] = ParseResourceId(ids[i]);
            }

            // One slot per id, written only by its own iteration, so no synchronization is needed.
            var failures = new Exception?[ids.Length];

            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = ResourceIdFanOutLimit,
                CancellationToken = Publisher.Token,
            };

            // Failures are recorded rather than propagated: if one escaped, Parallel.ForEachAsync
            // would cancel the ids that have not started yet. Recording them keeps the run going so
            // every id is attempted and the caller gets a complete picture.
            await Parallel.ForEachAsync(Enumerable.Range(0, ids.Length), options, async (index, _) =>
            {
                try
                {
                    var (subscriptionId, resourceGroup, name) = targets[index];
                    using HttpResponseMessage response = await CreateResourceIdChild(subscriptionId, resourceGroup, name).SendAsync();
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is not a per-resource failure; let it unwind the whole fan-out.
                    throw;
                }
                catch (Exception ex)
                {
                    failures[index] = ex;
                }
            });

            ThrowIfAnyResourceIdFailed(ids, failures);
        }

        private static void ThrowIfAnyResourceIdFailed(string[] ids, Exception?[] failures)
        {
            int[] failed = [.. Enumerable.Range(0, ids.Length).Where(index => failures[index] is not null)];
            if (failed.Length == 0)
            {
                return;
            }

            // Every failure already reached the error stream through Events.ResourceOperationFailed,
            // so the summary names the resources instead of repeating the ARM payloads. An
            // AggregateException would render as "One or more errors occurred." because hosts show
            // only Message, so it is carried as the inner exception for telemetry instead.
            string names = string.Join(", ", failed.Select(index => DescribeResourceId(ids[index])));
            throw new InvalidOperationException(
                $"{failed.Length} of {ids.Length} resources failed: {names}.",
                new AggregateException(failed.Select(index => failures[index]!)));
        }

        /// <summary>Trailing segment of a resource id, for error messages. Never throws.</summary>
        private static string DescribeResourceId(string id)
        {
            string[] segments = id?.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];
            return segments.Length > 0 ? segments[^1] : "(empty id)";
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
    /// Generic compute processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishs start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishs end.
    /// </summary>
    public partial class ComputeProcessorBase<TRequest, TResponse> : ComputeProcessorBase
        where TRequest : class
        where TResponse : class
    {
        /// <summary>
        /// Typed request-body seam. Returns null (no body) by default; write processors override to return
        /// their <typeparamref name="TRequest"/> model. Covariantly overrides the non-generic
        /// <see cref="ComputeProcessorBase.CreateRequestBody"/> that the request template consumes.
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
