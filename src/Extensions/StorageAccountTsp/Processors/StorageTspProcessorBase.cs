// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
//
// StorageTsp processor base. Structural mirror of Compute's ComputeProcessorBase — each
// extension owns its own base so its LRO polling machinery stays isolated and can
// evolve independently. Runtime behaviour is identical to the Compute variant.
//
// Generated from provider Microsoft.Storage (api-version 2026-04-01).
// Source: TypeSpec (specification/storage/Storage.Management@56e69a6)

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.ExtensionRuntime.Json;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.StorageAccountTsp.Processors
{
    /// <summary>
    /// Non-generic base used as a marker so that <see cref="CreateExtendedProcessor{T}"/> and
    /// any code that needs to reference "any StorageTsp processor" stays type-safe.
    /// All real processors should derive from <see cref="StorageTspProcessorBase{TResponse}"/>.
    /// </summary>
    public abstract class StorageTspProcessorBase : IProcessor
    {
        public IRuntimeHttpPipeline HttpPipeline = null!;
        public IEventPublisher Publisher = null!;
        public JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;

        /// <summary>The resolved connection profile from the processor context; null when none is available.</summary>
        protected ProfileModel? ResolvedProfile;

        /// <summary>
        /// Bound <c>--profile</c>/<c>-Profile</c> parameter. The Engine resolves the named profile into
        /// <see cref="ResolvedProfile"/> before the processor runs.
        /// </summary>
        public string? Profile { get; set; }

        protected virtual void BeforeInit() { }
        protected virtual void AfterInit() { }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            BeforeInit();

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
        /// runtime context as this processor.
        /// </summary>
        protected T CreateExtendedProcessor<T>(Action<T>? configure = null) where T : StorageTspProcessorBase, new()
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

        protected virtual Func<HttpResponseMessage, string?> GetFinalUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetOperationLocationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetAsyncOperationUri => _ => null;
        protected virtual Func<HttpResponseMessage, string?> GetLocationUri => _ => null;

        /// <summary>
        /// When true, LRO operations return the initial 201/202 response immediately
        /// instead of polling to a terminal state. Bound from the shared "NoWait"
        /// parameter injected by <c>CmdletEngineRunner</c> for cmdlets that expose
        /// <c>-NoWait</c> and by CLI argument parsing for <c>--no-wait</c>. Non-LRO
        /// processors have the property but ignore it.
        /// </summary>
        public bool NoWait { get; set; }

        internal async virtual Task<HttpResponseMessage> SendAsync()
        {
            IRuntimeHttpPipeline _httpPipeline = HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
            IEventPublisher _publisher = Publisher ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
            using HttpRequestMessage _request = await CreateRequestMessage();
            HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _publisher);
            await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
            try
            {
                await _response.EnsureSuccessAsync(_publisher);
            }
            catch
            {
                _response.Dispose();
                throw;
            }
            return _response;
        }

        internal async virtual Task<HttpResponseMessage> SendAsyncLRO()
        {
            HttpRequestMessage _request = await CreateRequestMessage();
            try
            {
                IRuntimeHttpPipeline _httpPipeline = HttpPipeline ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
                IEventPublisher _publisher = Publisher ?? throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
                string _originalUri = _request.RequestUri!.AbsoluteUri;
                HttpResponseMessage _response = await _httpPipeline.SendAsync(_request, _publisher);
                await _publisher.Publish(Events.TracingHttpResponseMessage, _response);

                // NoWait: return the initial 201/202 response immediately without polling.
                // Callers get the operation-tracking headers (Location / Azure-AsyncOperation)
                // on the response and can poll manually if they want.
                if (NoWait)
                {
                    await _publisher.Publish(Events.PollingFinished, "NoWait requested; skipping LRO polling.");
                    return _response;
                }

                var _finalUri = string.IsNullOrEmpty(GetFinalUri(_response)) ? _originalUri : GetFinalUri(_response);
                var _asyncOperationUri = GetAsyncOperationUri(_response);
                var _locationUri = GetLocationUri(_response);
                var _operationLocationUri = GetOperationLocationUri(_response);

                while (_request.Method == HttpMethod.Put && _response.StatusCode == HttpStatusCode.OK || _response.StatusCode == HttpStatusCode.Created || _response.StatusCode == HttpStatusCode.Accepted)
                {
                    int delay = (int)(_response.Headers.RetryAfter?.Delta?.TotalSeconds ?? _httpPipeline.PollingInterval);
                    await _publisher.Publish(Events.DelayPolling, $"Delaying {delay} seconds before polling.");
                    Task delayTask = Task.Delay(delay * 1000, _publisher.Token);

                    if (!string.IsNullOrEmpty(GetAsyncOperationUri(_response)))
                    {
                        _asyncOperationUri = GetAsyncOperationUri(_response);
                    }
                    if (!string.IsNullOrEmpty(GetLocationUri(_response)))
                    {
                        _locationUri = GetLocationUri(_response);
                    }
                    if (!string.IsNullOrEmpty(GetOperationLocationUri(_response)))
                    {
                        _operationLocationUri = GetOperationLocationUri(_response);
                    }
                    var _uri = string.IsNullOrEmpty(_asyncOperationUri) ? string.IsNullOrEmpty(_locationUri) ? string.IsNullOrEmpty(_operationLocationUri) ? _originalUri : _operationLocationUri : _locationUri : _asyncOperationUri;

                    _response?.Dispose();

                    await delayTask;

                    _request = _request.CloneAndDispose(new Uri(_uri), HttpMethod.Get);
                    await _publisher.Publish(Events.BeforePollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                    _response = await _httpPipeline.SendAsync(_request, _publisher);
                    await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
                    await _publisher.Publish(Events.AfterPollingCall);

                    if (_response.StatusCode == HttpStatusCode.OK)
                    {
                        var error = false;
                        try
                        {
                            if (JsonNode.Parse(await _response.Content.ReadAsStringAsync()) is JsonObject json)
                            {
                                var state = json.Property("properties")?.PropertyT<JsonString>("provisioningState") ?? json.PropertyT<JsonString>("status");
                                if (state is null)
                                {
                                    break;
                                }

                                switch (state?.ToString()?.ToLower())
                                {
                                    case "failed":
                                        error = true;
                                        break;
                                    case "succeeded":
                                    case "canceled":
                                        break;

                                    default:
                                        _response.StatusCode = HttpStatusCode.Created;
                                        continue;
                                }
                            }
                        }
                        catch
                        {
                            // swallow polling peek errors; final result path handles the outcome
                        }
                        if (error)
                        {
                            throw new Exception($"Long running operation failed with response: {_response}");
                        }
                    }

                    if (_response.StatusCode == HttpStatusCode.Created || _response.StatusCode == HttpStatusCode.Accepted)
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(_finalUri))
                    {
                        _response?.Dispose();
                        _request = _request.CloneAndDispose(new Uri(_finalUri), HttpMethod.Get);
                        await _publisher.Publish(Events.BeforeFinalPollingCall, $"Polling status: {Environment.NewLine}_finalUri: {Environment.NewLine}{_finalUri}{Environment.NewLine}_asyncOperationUri: {Environment.NewLine}{_asyncOperationUri}{Environment.NewLine}_locationUri: {Environment.NewLine}{_locationUri}{Environment.NewLine}_operationLocationUri: {Environment.NewLine}{_operationLocationUri}");
                        _response = await _httpPipeline.SendAsync(_request, _publisher);
                        await _publisher.Publish(Events.TracingHttpResponseMessage, _response);
                        await _publisher.Publish(Events.AfterFinalPollingCall);
                        break;
                    }
                }
                switch (_response.StatusCode)
                {
                    case HttpStatusCode.OK:
                    case HttpStatusCode.NoContent:
                        {
                            await _publisher.Publish(Events.PollingFinished);
                            break;
                        }
                    default:
                        {
                            await _publisher.Publish(Events.PollingFailed);
                            try
                            {
                                await _response.EnsureSuccessAsync(_publisher);
                            }
                            catch
                            {
                                _response.Dispose();
                                throw;
                            }
                            break;
                        }
                }
                return _response;
            }
            finally
            {
                _request?.Dispose();
            }
        }
    }

    /// <summary>
    /// Generic StorageTsp processor base. The default <see cref="ProcessAsync"/> pipeline mirrors
    /// Compute's: publish start, send, deserialize into <typeparamref name="TResponse"/>,
    /// write output, publish end. LRO processors override <see cref="SendAsync"/> to delegate
    /// to <see cref="StorageTspProcessorBase.SendAsyncLRO"/>.
    /// </summary>
    public class StorageTspProcessorBase<TResponse> : StorageTspProcessorBase where TResponse : class
    {
        public string SubscriptionId { get; set; } = string.Empty;

        protected void ResolveSubscriptionId() =>
            SubscriptionId = !string.IsNullOrEmpty(SubscriptionId)
                ? SubscriptionId
                : ResolvedProfile?.SubscriptionId
                  ?? throw new InvalidOperationException("SubscriptionId must be provided either via property or an active profile.");

        internal Task<HttpRequestMessage> BuildRequestMessageAsync()
        {
            ResolveSubscriptionId();
            return CreateRequestMessage();
        }

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
        /// Orchestrators can opt out of typed response projection when their
        /// asynchronous response is a deployment or operation resource rather
        /// than <typeparamref name="TResponse"/>.
        /// </summary>
        protected virtual bool UseRawResponseOutput => false;

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

            if (UseRawResponseOutput)
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    await WriteOutput((object)doc.RootElement.Clone());
                }
                finally
                {
                    await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
                }
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
            catch (JsonException)
            {
                // Strong-typed deserialization failed (typically because ARM returned
                // fields our tsp-generated model class doesn't know about — service-injected
                // extensions, tags added by policy, etc). Fall back to emitting the raw JSON
                // as a JsonElement so the caller still sees the full ARM response. The
                // event feed gets a warning so downstream tools can flag drift.
                await Publisher.Publish(Events.ResponseInvalidJson, $"response has fields not in typed model for {response.RequestMessage?.RequestUri}; falling back to raw JSON");
                try
                {
                    using var doc = JsonDocument.Parse(rawBody);
                    await WriteOutput((object)doc.RootElement.Clone());
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException($"Failed to deserialize response body into {typeof(TResponse).Name}. Raw body: {rawBody}");
                }
            }
            finally
            {
                await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
            }
        }

        /// <summary>Resource URI for Operation (Microsoft.Storage/operations/{name}).</summary>
        protected string OperationUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/operations/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Operation at resource-group scope.</summary>
        protected string OperationCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/operations?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Operation at subscription scope.</summary>
        protected string OperationCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/operations?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for BlobContainer (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string BlobContainerUri(string resourceGroupName, string containerName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{containerName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for BlobContainer at resource-group scope.</summary>
        protected string BlobContainerCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for BlobContainer at subscription scope.</summary>
        protected string BlobContainerCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for BlobService (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string BlobServiceUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for BlobService at resource-group scope.</summary>
        protected string BlobServiceCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for BlobService at subscription scope.</summary>
        protected string BlobServiceCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for StorageAccount (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string StorageAccountUri(string resourceGroupName, string accountName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{accountName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageAccount at resource-group scope.</summary>
        protected string StorageAccountCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageAccount at subscription scope.</summary>
        protected string StorageAccountCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for FileShare (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string FileShareUri(string resourceGroupName, string shareName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{shareName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for FileShare at resource-group scope.</summary>
        protected string FileShareCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for FileShare at subscription scope.</summary>
        protected string FileShareCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for FileService (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string FileServiceUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for FileService at resource-group scope.</summary>
        protected string FileServiceCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for FileService at subscription scope.</summary>
        protected string FileServiceCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for QueueService (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string QueueServiceUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for QueueService at resource-group scope.</summary>
        protected string QueueServiceCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for QueueService at subscription scope.</summary>
        protected string QueueServiceCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for DeletedAccount (Microsoft.Storage/locations/{name}).</summary>
        protected string DeletedAccountUri(string resourceGroupName, string deletedAccountName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/locations/{deletedAccountName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for DeletedAccount at resource-group scope.</summary>
        protected string DeletedAccountCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/locations?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for DeletedAccount at subscription scope.</summary>
        protected string DeletedAccountCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/locations?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for ManagementPolicy (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string ManagementPolicyUri(string resourceGroupName, string managementPolicyName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{managementPolicyName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for ManagementPolicy at resource-group scope.</summary>
        protected string ManagementPolicyCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for ManagementPolicy at subscription scope.</summary>
        protected string ManagementPolicyCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for BlobInventoryPolicy (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string BlobInventoryPolicyUri(string resourceGroupName, string blobInventoryPolicyName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{blobInventoryPolicyName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for BlobInventoryPolicy at resource-group scope.</summary>
        protected string BlobInventoryPolicyCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for BlobInventoryPolicy at subscription scope.</summary>
        protected string BlobInventoryPolicyCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for PrivateEndpointConnection (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string PrivateEndpointConnectionUri(string resourceGroupName, string privateEndpointConnectionName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{privateEndpointConnectionName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for PrivateEndpointConnection at resource-group scope.</summary>
        protected string PrivateEndpointConnectionCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for PrivateEndpointConnection at subscription scope.</summary>
        protected string PrivateEndpointConnectionCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for EncryptionScope (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string EncryptionScopeUri(string resourceGroupName, string encryptionScopeName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{encryptionScopeName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for EncryptionScope at resource-group scope.</summary>
        protected string EncryptionScopeCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for EncryptionScope at subscription scope.</summary>
        protected string EncryptionScopeCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for TableService (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string TableServiceUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for TableService at resource-group scope.</summary>
        protected string TableServiceCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for TableService at subscription scope.</summary>
        protected string TableServiceCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for NetworkSecurityPerimeterConfiguration (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string NetworkSecurityPerimeterConfigurationUri(string resourceGroupName, string networkSecurityPerimeterConfigurationName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{networkSecurityPerimeterConfigurationName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for NetworkSecurityPerimeterConfiguration at resource-group scope.</summary>
        protected string NetworkSecurityPerimeterConfigurationCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for NetworkSecurityPerimeterConfiguration at subscription scope.</summary>
        protected string NetworkSecurityPerimeterConfigurationCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for StorageTaskAssignment (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string StorageTaskAssignmentUri(string resourceGroupName, string storageTaskAssignmentName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageTaskAssignmentName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageTaskAssignment at resource-group scope.</summary>
        protected string StorageTaskAssignmentCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageTaskAssignment at subscription scope.</summary>
        protected string StorageTaskAssignmentCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for Connector (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string ConnectorUri(string resourceGroupName, string connectorName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{connectorName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Connector at resource-group scope.</summary>
        protected string ConnectorCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Connector at subscription scope.</summary>
        protected string ConnectorCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for DataShare (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string DataShareUri(string resourceGroupName, string dataShareName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{dataShareName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for DataShare at resource-group scope.</summary>
        protected string DataShareCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for DataShare at subscription scope.</summary>
        protected string DataShareCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for AdvancedPlatformMetric (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string AdvancedPlatformMetricUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for AdvancedPlatformMetric at resource-group scope.</summary>
        protected string AdvancedPlatformMetricCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for AdvancedPlatformMetric at subscription scope.</summary>
        protected string AdvancedPlatformMetricCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for PrivateLinkResource (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string PrivateLinkResourceUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for PrivateLinkResource at resource-group scope.</summary>
        protected string PrivateLinkResourceCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for PrivateLinkResource at subscription scope.</summary>
        protected string PrivateLinkResourceCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for StorageTaskAssignmentsInstancesReport (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string StorageTaskAssignmentsInstancesReportUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageTaskAssignmentsInstancesReport at resource-group scope.</summary>
        protected string StorageTaskAssignmentsInstancesReportCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageTaskAssignmentsInstancesReport at subscription scope.</summary>
        protected string StorageTaskAssignmentsInstancesReportCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for Queue (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string QueueUri(string resourceGroupName, string queueName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{queueName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Queue at resource-group scope.</summary>
        protected string QueueCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Queue at subscription scope.</summary>
        protected string QueueCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for ObjectReplicationPolicy (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string ObjectReplicationPolicyUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for ObjectReplicationPolicy at resource-group scope.</summary>
        protected string ObjectReplicationPolicyCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for ObjectReplicationPolicy at subscription scope.</summary>
        protected string ObjectReplicationPolicyCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for LocalUser (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string LocalUserUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for LocalUser at resource-group scope.</summary>
        protected string LocalUserCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for LocalUser at subscription scope.</summary>
        protected string LocalUserCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for Table (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string TableUri(string resourceGroupName, string tableName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{tableName}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Table at resource-group scope.</summary>
        protected string TableCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Table at subscription scope.</summary>
        protected string TableCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for StorageTaskAssignmentInstancesReport (Microsoft.Storage/storageAccounts/{name}).</summary>
        protected string StorageTaskAssignmentInstancesReportUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageTaskAssignmentInstancesReport at resource-group scope.</summary>
        protected string StorageTaskAssignmentInstancesReportCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for StorageTaskAssignmentInstancesReport at subscription scope.</summary>
        protected string StorageTaskAssignmentInstancesReportCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/storageAccounts?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for Sku (Microsoft.Storage/skus/{name}).</summary>
        protected string SkuUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/skus/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Sku at resource-group scope.</summary>
        protected string SkuCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/skus?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Sku at subscription scope.</summary>
        protected string SkuCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/skus?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Resource URI for Usage (Microsoft.Storage/locations/{name}).</summary>
        protected string UsageUri(string resourceGroupName, string name)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/locations/{name}?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Usage at resource-group scope.</summary>
        protected string UsageCollectionUriByResourceGroup(string resourceGroupName)
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/locations?api-version={StorageTspConstants.ApiVersion}";

        /// <summary>Collection URI for Usage at subscription scope.</summary>
        protected string UsageCollectionUriBySubscription()
            => $"https://management.azure.com/subscriptions/{SubscriptionId}/providers/Microsoft.Storage/locations?api-version={StorageTspConstants.ApiVersion}";

        // JsonContent factory kept in the base to guarantee the same serializer options are used
        // for every request body across processors.
        protected JsonContent JsonBody<TBody>(TBody body) => JsonContent.Create(body, options: JsonOptions);

        // Build a nested JSON object from a flat set of (dotted-path, value) pairs.
        // Used by generated processors that received their body via the tsp emitter's
        // Phase B3 flattening: each cmdlet parameter has a JsonPath like
        // "properties.virtualMachineProfile.osProfile.adminUsername". Nulls are
        // skipped so optional parameters that the caller didn't set don't appear
        // in the request payload.
        //
        // The optional `bodyJsonPatch` argument is deep-merged into the built body
        // AFTER the flat leaves are applied. It's an escape hatch for the cases
        // where the ARM schema has an array-of-model that we can't cleanly flatten
        // (e.g. VMSS networkProfile.networkInterfaceConfigurations). Users pass
        // raw JSON via `-BodyJsonPatch` / `--body-json-patch`.
        protected JsonContent BuildNestedBody(
            IEnumerable<(string JsonPath, object? Value)> leaves,
            string? bodyJsonPatch = null)
        {
            var root = new System.Text.Json.Nodes.JsonObject();
            foreach (var (path, value) in leaves)
            {
                if (value is null || string.IsNullOrEmpty(path)) continue;
                var segments = path.Split('.');
                System.Text.Json.Nodes.JsonObject cursor = root;
                for (int i = 0; i < segments.Length - 1; i++)
                {
                    var seg = segments[i];
                    if (cursor[seg] is System.Text.Json.Nodes.JsonObject next)
                    {
                        cursor = next;
                    }
                    else
                    {
                        var fresh = new System.Text.Json.Nodes.JsonObject();
                        cursor[seg] = fresh;
                        cursor = fresh;
                    }
                }
                var leaf = segments[^1];
                cursor[leaf] = System.Text.Json.JsonSerializer.SerializeToNode(value, JsonOptions);
            }

            if (!string.IsNullOrWhiteSpace(bodyJsonPatch))
            {
                var patch = System.Text.Json.Nodes.JsonNode.Parse(bodyJsonPatch);
                if (patch is System.Text.Json.Nodes.JsonObject po)
                {
                    DeepMerge(root, po);
                }
            }

            return JsonContent.Create(root, options: JsonOptions);
        }

        // Recursive deep-merge of `source` into `target`. Objects merge property
        // by property; arrays and scalars in `source` REPLACE their counterpart
        // in `target`. This lets the caller override or extend any subtree of the
        // generated body with a partial JSON snippet.
        private static void DeepMerge(System.Text.Json.Nodes.JsonObject target, System.Text.Json.Nodes.JsonObject source)
        {
            foreach (var kv in source.ToArray())
            {
                var key = kv.Key;
                var val = kv.Value?.DeepClone();
                if (val is System.Text.Json.Nodes.JsonObject srcObj
                    && target[key] is System.Text.Json.Nodes.JsonObject tgtObj)
                {
                    DeepMerge(tgtObj, srcObj);
                }
                else
                {
                    target[key] = val;
                }
            }
        }
    }

    internal static class StorageTspConstants
    {
        // Pinned to the API version declared in the source spec
        // (TypeSpec: specification/storage/Storage.Management@56e69a6).
        public const string ApiVersion = "2026-04-01";
    }
}
