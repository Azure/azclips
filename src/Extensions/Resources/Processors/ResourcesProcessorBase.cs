using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Processors
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
        protected AuthContext AuthContext = null!;
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
            AuthContext = context.AuthHandler.AuthContext;

            AfterInit();
            return this;
        }
        public virtual Task ProcessAsync() => Task.CompletedTask;
        protected virtual Task<HttpRequestMessage> CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, AuthContext)
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
            child.AuthContext = AuthContext;
            configure?.Invoke(child);
            return child;
        }

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            // Validate runtime context before CreateRequestMessage(), since derived implementations
            // may publish events (using Publisher) while building the request.
            EnsureRuntimeContext();
            using HttpRequestMessage _request = await CreateRequestMessage();
            HttpResponseMessage _response = await HttpPipeline.SendAsync(_request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, _response);
            // Surface non-2xx ARM responses (e.g. 404 on a missing resource group) as a thrown
            // HttpRequestException after publishing ResourceOperationFailed, instead of deserializing
            // an error body into a default response. EnsureSuccessAsync only reads the body on failure,
            // so the success-path content stream stays intact for the deserialize in ProcessAsync.
            try
            {
                await _response.EnsureSuccessAsync(Publisher);
            }
            catch
            {
                _response.Dispose();
                throw;
            }
            return _response;
        }

        /// <summary>
        /// Verifies the processor was initialized with the runtime context required to issue requests
        /// and publish events. Throws a deterministic <see cref="InvalidOperationException"/> (rather
        /// than letting a later access throw <see cref="NullReferenceException"/>) when it was not.
        /// </summary>
        private protected void EnsureRuntimeContext()
        {
            if (HttpPipeline is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
            }
            if (Publisher is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
            }
            if (WriteOutput is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(WriteOutput)}.");
            }
            if (JsonOptions is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(JsonOptions)}.");
            }
        }
    }

    /// <summary>
    /// Generic resources processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishes start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishes end.
    /// <para>
    /// <see cref="SendAsync"/> is the internal "give me the final response" transport primitive.
    /// Atomic processors don't override it (the inherited <see cref="SendAsync"/>
    /// resolves <see cref="SubscriptionId"/> then dispatches a single HTTP request).
    /// </para>
    /// </summary>
    public class ResourcesProcessorBase<TResponse> : ResourcesProcessorBase where TResponse : class
    {
        public string SubscriptionId { get; set; } = string.Empty;

        /// <summary>
        /// Resolves <see cref="SubscriptionId"/> from the property if set, otherwise from <see cref="AuthContext"/>.
        /// Throws if neither has a value. Idempotent. Called automatically before any HTTP transport.
        /// </summary>
        /// TODO: update processor context to accept tenantId and subscriptionId instead of auth context
        protected void ResolveSubscriptionId()
        {
            if (!string.IsNullOrWhiteSpace(SubscriptionId))
            {
                return;
            }

            if (AuthContext is null)
            {
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(AuthContext)}, so SubscriptionId cannot be resolved. Provide it via the property instead.");
            }

            if (string.IsNullOrWhiteSpace(AuthContext.SubscriptionId))
            {
                throw new InvalidOperationException("SubscriptionId must be provided either via property or auth context.");
            }

            SubscriptionId = AuthContext.SubscriptionId;
        }

        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return base.SendAsync();
        }

        public override async Task ProcessAsync()
        {
            EnsureRuntimeContext();
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            try
            {
                using HttpResponseMessage response = await SendAsync();

                if (response.Content is null)
                {
                    await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                    return;
                }

                string rawBody = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawBody))
                {
                    await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                    return;
                }

                try
                {
                    TResponse? body = JsonSerializer.Deserialize<TResponse>(rawBody, JsonOptions);
                    if (body is not null)
                    {
                        await WriteOutput(body);
                    }
                }
                catch (JsonException ex)
                {
                    await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                    throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}", ex);
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }
    }
}