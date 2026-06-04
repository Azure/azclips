using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Cirrus.ExtensionRuntime;

namespace Microsoft.Azure.Cirrus.Extensions.Rest.Processors
{
    /// <summary>
    /// Base processor for the universal <c>rest</c> passthrough command. Captures the runtime
    /// context (HttpPipeline, Publisher, JsonOptions, WriteOutput, AuthContext) and exposes a single
    /// untyped transport primitive, <see cref="SendRequestAsync"/>.
    /// <para>
    /// Unlike the typed Resources base, this base does NOT deserialize the response into a fixed
    /// model. The transport primitive here returns non-2xx responses as-is rather than throwing, so
    /// the full response payload (status, headers, body) can be surfaced to output first, matching
    /// <c>az rest</c>. The concrete <see cref="RestProcessor.ProcessAsync"/> writes that payload to
    /// output and then, for a status &gt;= 400, publishes a failure event and throws an
    /// <see cref="System.Net.Http.HttpRequestException"/> so the command produces a non-zero,
    /// terminating outcome after the error body has already been made visible.
    /// </para>
    /// <para>
    /// <see cref="AuthContext"/> is captured because the command performs <c>{subscriptionId}</c>
    /// token substitution from the current auth context.
    /// </para>
    /// </summary>
    public abstract class RestProcessorBase : IProcessor
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

        /// <summary>
        /// Sends <paramref name="request"/> through the authenticated pipeline (the auth handler is
        /// already prepended by the engine, so this command only targets the ARM audience) and
        /// returns the raw response. The caller owns the returned <see cref="HttpResponseMessage"/>
        /// and must dispose it. Non-2xx responses are returned as-is (not thrown) so error payloads
        /// can be surfaced.
        /// </summary>
        internal async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request)
        {
            EnsureRuntimeContext();
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            return response;
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
}
