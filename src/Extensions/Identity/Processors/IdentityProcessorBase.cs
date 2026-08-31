// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using ProfileModel = Core.Abstractions.Profile;

namespace Microsoft.Azure.Azclips.Extensions.Identity.Processors
{
    public abstract class IdentityProcessorBase : IProcessor
    {
        protected const string IdentityApiVersion = "2025-05-31-preview";
        protected const string ResourceGroupsApiVersion = "2025-04-01";
        private const string IdentityTypeSegment = "userAssignedIdentities";

        protected IRuntimeHttpPipeline HttpPipeline = null!;
        protected IEventPublisher Publisher = null!;
        protected JsonSerializerOptions JsonOptions = null!;
        protected OutputStreamDelegate WriteOutput = null!;
        protected ProfileModel? ResolvedProfile;

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

            AfterInit();
            return this;
        }

        public virtual Task ProcessAsync() => Task.CompletedTask;
        protected virtual Task<HttpRequestMessage> CreateRequestMessage() => throw new NotImplementedException($"Processor {GetType().Name} does not implement {nameof(CreateRequestMessage)}");

        protected void ResolveSubscriptionId()
        {
            if (!string.IsNullOrWhiteSpace(SubscriptionId))
                return;

            if (ResolvedProfile is null)
                throw new InvalidOperationException($"Processor {GetType().Name} has no profile, so SubscriptionId cannot be resolved. Sign in with 'login' or pass --subscription / --profile.");

            if (string.IsNullOrWhiteSpace(ResolvedProfile.SubscriptionId))
                throw new InvalidOperationException("SubscriptionId must be provided either via property or an active profile.");

            SubscriptionId = ResolvedProfile.SubscriptionId;
        }

        internal virtual async Task<HttpResponseMessage> SendAsync()
        {
            EnsureRuntimeContext();
            using HttpRequestMessage request = await CreateRequestMessage();
            HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);

            try
            {
                await response.EnsureSuccessAsync(Publisher);
            }
            catch
            {
                response.Dispose();
                throw;
            }
            return response;
        }

        protected string BuildIdentityUri(string resourceGroup, string resourceName)
        {
            return $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(resourceGroup)}/providers/Microsoft.ManagedIdentity/{IdentityTypeSegment}/{Uri.EscapeDataString(resourceName)}?api-version={IdentityApiVersion}";
        }

        protected string BuildIdentityListUri(string? resourceGroup, string? nextToken = null)
        {
            //TODO: This should be config based for cloud env supportability.
            //For now, we will hardcode the endpoint to Azure public cloud.
            if (!string.IsNullOrWhiteSpace(nextToken))
            {
                if (Uri.TryCreate(nextToken, UriKind.Absolute, out var absolute))
                {
                    return absolute.ToString();
                }

                var baseUri = BuildIdentityListUri(resourceGroup);
                return $"{baseUri}&$skiptoken={Uri.EscapeDataString(nextToken)}";
            }

            string scope = string.IsNullOrWhiteSpace(resourceGroup)
                ? $"subscriptions/{Uri.EscapeDataString(SubscriptionId)}"
                : $"subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(resourceGroup)}";
            return $"https://management.azure.com/{scope}/providers/Microsoft.ManagedIdentity/{IdentityTypeSegment}?api-version={IdentityApiVersion}";
        }

        protected string BuildResourceGroupUri(string resourceGroup)
        {
            //TODO: This should be config based for cloud env supportability.
            //For now, we will hardcode the public cloud endpoint.
            return $"https://management.azure.com/subscriptions/{Uri.EscapeDataString(SubscriptionId)}/resourceGroups/{Uri.EscapeDataString(resourceGroup)}?api-version={ResourceGroupsApiVersion}";
        }

        protected (string SubscriptionId, string ResourceGroup, string ResourceName) ParseIdentityResourceId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("Identity resource id cannot be empty.");

            string[] segments = id.Trim().Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 8
                || !segments[0].Equals("subscriptions", StringComparison.OrdinalIgnoreCase)
                || !segments[2].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase)
                || !segments[4].Equals("providers", StringComparison.OrdinalIgnoreCase)
                || !segments[5].Equals("Microsoft.ManagedIdentity", StringComparison.OrdinalIgnoreCase)
                || !segments[6].Equals(IdentityTypeSegment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Invalid managed identity resource id '{id}'. Expected /subscriptions/{{subscriptionId}}/resourceGroups/{{resourceGroupName}}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/{{resourceName}}.");
            }

            return (segments[1], segments[3], segments[7]);
        }

        protected static bool HasMultipleIdentityResourceIds(string[]? ids) => ids is { Length: > 1 };

        protected void ApplyIdentityResourceId(string[]? ids)
        {
            if (ids is null || ids.Length == 0)
                return;

            if (ids.Length > 1)
                throw new InvalidOperationException("Only one identity resource id is supported for this command.");

            var parsed = ParseIdentityResourceId(ids[0]);
            SubscriptionId = parsed.SubscriptionId;
            SetResourceIdArguments(parsed.ResourceGroup, parsed.ResourceName);
        }

        protected virtual void SetResourceIdArguments(string resourceGroup, string resourceName) { }

        protected static Dictionary<string, string>? ParseTags(string[]? tags)
        {
            if (tags is null || tags.Length == 0)
                return null;

            string single = string.Join(' ', tags).Trim();
            if (single.StartsWith('{'))
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(single);
            }

            var tagDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                var parts = tag.Split('=', 2);
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]))
                    throw new InvalidOperationException($"Invalid tag '{tag}'. Expected KEY=VALUE format.");
                tagDict[parts[0].Trim()] = parts[1];
            }
            return tagDict;
        }

        protected static JsonObject? ParseAssignmentRestrictions(string[]? restriction)
        {
            if (restriction is null || restriction.Length == 0)
                return null;

            string raw = string.Join(' ', restriction).Trim();
            if (raw.Length == 0)
                return null;

            if (raw.StartsWith("@", StringComparison.Ordinal))
            {
                raw = File.ReadAllText(raw[1..]);
            }

            if (raw.StartsWith('{'))
            {
                var node = JsonNode.Parse(raw) as JsonObject
                    ?? throw new InvalidOperationException("ResourceRestriction must be a JSON object.");
                return node;
            }

            var providers = new List<string>();
            foreach (string token in restriction)
            {
                if (string.IsNullOrWhiteSpace(token))
                    continue;
                string value = token.Trim();
                if (value.StartsWith("providers=", StringComparison.OrdinalIgnoreCase))
                {
                    value = value["providers=".Length..];
                }
                foreach (var provider in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    providers.Add(provider);
                }
            }

            return new JsonObject { ["providers"] = new JsonArray(providers.Select(p => JsonValue.Create(p)).ToArray<JsonNode?>()) };
        }

        protected void ValidateIdentityNameArguments(string? resourceGroup, string? resourceName, string[]? ids)
        {
            bool hasIds = ids is { Length: > 0 };
            if (!hasIds && string.IsNullOrWhiteSpace(resourceGroup))
                throw new InvalidOperationException("--resource-group is required unless --ids is provided.");
            if (!hasIds && string.IsNullOrWhiteSpace(resourceName))
                throw new InvalidOperationException("--name or --resource-name is required unless --ids is provided.");
        }

        protected void EnsureRuntimeContext()
        {
            if (HttpPipeline is null)
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(HttpPipeline)}.");
            if (Publisher is null)
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(Publisher)}.");
            if (WriteOutput is null)
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(WriteOutput)}.");
            if (JsonOptions is null)
                throw new InvalidOperationException($"Processor {GetType().Name} was not initialized with {nameof(JsonOptions)}.");
        }
    }

    public abstract class IdentityProcessorBase<TResponse> : IdentityProcessorBase where TResponse : class
    {
        internal override Task<HttpResponseMessage> SendAsync()
        {
            ResolveSubscriptionId();
            return base.SendAsync();
        }

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
