// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.ManagedServiceIdentity.Processors
{
    public abstract partial class IdentityProcessorBase : IProcessor
    {
        protected const string IdentityApiVersion = "2025-05-31-preview";
        protected const string ManagementEndpoint = "https://management.azure.com";
                
        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected IAuthHandler AuthHandler = null!;
        protected ProfileModel? ResolvedProfile = null!;

        public string? Profile { get; set; }
        public string SubscriptionId { get; set; } = string.Empty;
        public bool AcquirePolicyToken { get; set; }
        public string? ChangeReference { get; set; }

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
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
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

        protected virtual HttpMethod Method => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(Method)}.");
        protected virtual string CreateRequestUri() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestUri)}.");
        protected virtual object? CreateRequestBody() => null;
        protected virtual void CreateRequestHeaders(HttpRequestMessage request) { }

        protected virtual async Task<HttpRequestMessage> CreateRequestMessage()
        {
            await Publisher.Publish(Events.ProcessorBeforeHttpRequestCreate);

            HttpRequestMessage request = new(Method, CreateRequestUri());
            if (CreateRequestBody() is { } body)
            {
                request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
            }
            CreateRequestHeaders(request);

            await Publisher.Publish(Events.ProcessorAfterHttpRequestCreate);
            return request;
        }

        internal virtual async Task<HttpResponseMessage> SendAsync()
        {
            return await HttpRuntime.SendAsync(CreateRequestMessage, Publisher, HttpPipeline);
        }

        protected async Task<T?> ReadResponseBodyAsync<T>(HttpResponseMessage response) where T : class
        {
            string? rawBody = null;
            if (response.Content is null)
            {
                await Publisher.Publish(Events.ResponseNoContent, $"response has no content for {response.RequestMessage?.RequestUri}");
                return default;
            }

            if (string.IsNullOrWhiteSpace(rawBody = await response.Content.ReadAsStringAsync()))
            {
                await Publisher.Publish(Events.ResponseEmptyBody, $"response has empty body for {response.RequestMessage?.RequestUri}");
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<T>(rawBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                await Publisher.Publish(Events.ResponseInvalidJson, $"response has invalid JSON for {response.RequestMessage?.RequestUri}");
                throw new InvalidOperationException($"Failed to deserialize response body into {typeof(T).Name}. Raw body: {rawBody}", ex);
            }
        }

        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : IdentityProcessorBase, new()
        {
            T child = new T
            {
                HttpPipeline = HttpPipeline,
                Publisher = Publisher,
                JsonOptions = JsonOptions,
                AuthHandler = AuthHandler,
                WriteOutput = WriteOutput,
                ResolvedProfile = ResolvedProfile
            };
            configure?.Invoke(child);
            return child;
        }
    }

    public abstract partial class IdentityProcessorBase<TRequest, TResponse> : IdentityProcessorBase
        where TRequest : class
        where TResponse : class
    {
        protected override TRequest? CreateRequestBody() => default;

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
                TResponse? body = await ReadResponseBodyAsync<TResponse>(response);

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
