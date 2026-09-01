// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;

namespace Microsoft.Azure.Azclips.Extensions.MsGraph.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any graph processor" stays type-safe.
    /// All real processors should derive from <see cref="GraphProcessorBase{TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// Microsoft Graph requires a different token audience than ARM. ARM-bound processors let the
    /// engine acquire a token for the default ARM scope; Graph processors instead install an
    /// <see cref="IAuthHandler.AcquireTokenAsyncOverride"/> during <see cref="Init"/> that requests
    /// the <see cref="GraphConstants.GraphScopes"/> audience. The override wraps the engine default
    /// delegate, so token cache, tenant, and interactive fallback behavior are all preserved, only
    /// the scope changes. This keeps the entire Graph audience concern inside the extension; no
    /// Core change is required.
    /// </remarks>
    public abstract class GraphProcessorBase : IProcessor
    {
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected Profile Profile = null!;

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

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
            Profile = context.AuthHandler.Profile
                ?? throw new InvalidOperationException(
                    "Microsoft Graph commands require an active profile. Sign in first.");

            ApplyGraphScope(context.AuthHandler);

            AfterInit();
            return this;
        }

        /// <summary>
        /// Redirects token acquisition for this invocation to the Microsoft Graph audience by
        /// installing an override on the shared <see cref="IAuthHandler"/>. The override delegates
        /// to the engine default delegate but forces <see cref="GraphConstants.GraphScopes"/>,
        /// ignoring any scopes the pipeline would otherwise pass (the pipeline currently passes
        /// none, defaulting to ARM).
        /// </summary>
        private static void ApplyGraphScope(IAuthHandler authHandler)
        {
            AcquireTokenAsyncDelegate acquireWithDefaultScope = authHandler.AcquireTokenAsyncDefault;

            Task<AuthResult> AcquireWithGraphScope(string[]? _, string? tenantId, bool promptOnError, CancellationToken cancellationToken, Profile? profile) =>
                acquireWithDefaultScope(GraphConstants.GraphScopes, tenantId, promptOnError, cancellationToken, profile);

            authHandler.AcquireTokenAsyncOverride = AcquireWithGraphScope;
            authHandler.ApplyOverride = true;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;

        protected virtual Task<HttpRequestMessage> CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

        /// <summary>
        /// Creates an extended processor of type <typeparamref name="T"/> initialized with the same
        /// runtime context (HttpPipeline, Publisher, JsonOptions, WriteOutput, Profile)
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
            configure?.Invoke(child);
            return child;
        }

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            EnsureRuntimeContext();
            using HttpRequestMessage _request = await CreateRequestMessage();
            HttpResponseMessage _response = await HttpPipeline.SendAsync(_request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, _response);
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
    /// Generic graph processor base. The default <see cref="ProcessAsync"/> pipeline:
    /// 1) Publishes start, 2) calls <see cref="SendAsync"/>,
    /// 3) deserializes into <typeparamref name="TResponse"/>, 4) writes output, 5) Publishes end.
    /// <para>
    /// <see cref="SendAsync"/> is the internal "give me the final response" transport primitive.
    /// Atomic processors don't override it (the inherited <see cref="SendAsync"/> dispatches a
    /// single HTTP request). Graph reads are simple GETs with no long-running-operation polling.
    /// </para>
    /// </summary>
    public abstract class GraphProcessorBase<TResponse> : GraphProcessorBase where TResponse : class
    {
        /// <summary>
        /// Projects the deserialized response into the value written to output. Default writes the
        /// response as-is; list processors override this to unwrap the OData envelope and write its
        /// <c>value</c> collection so PowerShell enumerates it and the CLI serializes a JSON array.
        /// </summary>
        protected virtual object SelectOutput(TResponse body) => body;

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
                        await WriteOutput(SelectOutput(body));
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
