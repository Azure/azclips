// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Cli.Tests.Fixtures;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.ExtensionRuntime;
using Microsoft.Azure.Azclips.Extensions.Network.Models;
using Microsoft.Azure.Azclips.Extensions.Network.Processors;
using Microsoft.Azure.Azclips.Extensions.Resources.Models;
using Microsoft.Azure.Azclips.Extensions.Resources.Processors;

namespace Azclips.Tests.Fixtures.PublicIpBicepPilot;

public sealed record PublicIpPilotOptions(string DeploymentName)
{
    internal Func<string, Stream?> OpenResource { get; init; } = PublicIpBicepAssets.OpenResource;
}

public sealed class PublicIpBicepCreateProcessor : CreateOrUpdatePublicIPAddressesProcessor_Customization
{
    public override async Task ProcessAsync()
    {
        Publisher.Token.ThrowIfCancellationRequested();
        await BeforeProcessAsync();
        await Publisher.Publish(Events.ProcessorProcessAsyncStart);
        try
        {
            PublicIpPilotOptions options = AzclipsServiceProvider.GetRequiredService<PublicIpPilotOptions>();
            if (string.IsNullOrWhiteSpace(options.DeploymentName) || options.DeploymentName.Length > 64)
                throw new InvalidInputException("The Public IP pilot host must provide a bounded, owned deployment name.");
            if (string.IsNullOrWhiteSpace(SubscriptionId) || string.IsNullOrWhiteSpace(ResourceGroupName) ||
                string.IsNullOrWhiteSpace(PublicIpAddressName))
                throw new InvalidInputException("The Public IP pilot requires subscription, resource group and resource name.");

            var body = base.CreateRequestBody()
                ?? throw new InvalidOperationException("Public IP customization produced no request body.");
            JsonElement normalized = JsonSerializer.SerializeToElement(body, JsonOptions);
            IDictionary<string, DeploymentParameter> parameters = PublicIpBicepInputBinding.Bind(PublicIpAddressName, normalized);
            JsonElement template = PublicIpBicepAssets.Load(normalized, options.OpenResource);
            var get = new ExperimentalPublicIpGetProcessor
            {
                SubscriptionId = SubscriptionId,
                ResourceGroupName = ResourceGroupName,
                PublicIpAddressName = PublicIpAddressName,
            };
            var resourceOutput = new CapturedOutput<PublicIPAddress>();
            get.Init(ChildContext(resourceOutput));
            if (await get.ExistsAsync())
                throw new InvalidInputException("The Public IP pilot refuses to replace a detected existing target.");
            Publisher.Token.ThrowIfCancellationRequested();

            var deploymentOutput = new CapturedOutput<DeploymentExtended>();
            var deploy = new PilotDeploymentProcessor
            {
                SubscriptionId = SubscriptionId,
                ResourceGroupName = ResourceGroupName,
                Name = options.DeploymentName,
                Properties = new DeploymentProperties { Mode = "Incremental", Template = template, Parameters = parameters },
            };
            deploy.Init(ChildContext(deploymentOutput));
            await deploy.ProcessAsync();
            DeploymentExtended result = deploymentOutput.Value
                ?? throw new InvalidOperationException("The Public IP pilot deployment returned no result.");
            string? state = result.Properties?.ProvisioningState;
            if (string.Equals(state, "Canceled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(state, "Cancelled", StringComparison.OrdinalIgnoreCase))
                throw new OperationCanceledException("The Public IP pilot deployment was canceled.", Publisher.Token);
            if (!string.Equals(state, "Succeeded", StringComparison.OrdinalIgnoreCase) || result.Properties?.Error is not null)
                throw new InvalidOperationException($"The Public IP pilot deployment did not succeed (state: '{state ?? "missing"}').");
            Publisher.Token.ThrowIfCancellationRequested();

            await get.ProcessAsync();
            PublicIPAddress resource = resourceOutput.Value
                ?? throw new InvalidOperationException("The Public IP pilot final GET returned no resource.");
            if (!string.Equals(resource.Id, Uri.UnescapeDataString(new Uri(get.RequestUri).AbsolutePath), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(resource.Name, PublicIpAddressName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The Public IP pilot final GET returned an incomplete or different resource.");
            Publisher.Token.ThrowIfCancellationRequested();
            await WriteOutput(resource, Publisher.Token);
        }
        finally
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }

    private ProcessorContext ChildContext(IOutputHandler output) => new(HttpPipeline, Publisher, AuthHandler, output, ResolvedProfile);

    private sealed class PilotDeploymentProcessor : NewResourceGroupDeploymentProcessor
    {
        protected override void AfterInit() =>
            JsonOptions = new(JsonOptions) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    }

    private sealed class CapturedOutput<T> : IOutputHandler where T : class
    {
        internal T? Value { get; private set; }
        public OutputStreamDelegate WriteOutput { get; set; }

        internal CapturedOutput()
        {
            WriteOutput = (value, _) =>
            {
                if (value is not T result || Value is not null)
                    throw new InvalidOperationException($"The Public IP pilot expected exactly one {typeof(T).Name} result.");
                Value = result;
                return Task.CompletedTask;
            };
        }
    }

    private sealed class ExperimentalPublicIpGetProcessor : GetPublicIPAddressesProcessor
    {
        internal string RequestUri => CreateRequestUri();

        protected override string CreateRequestUri()
        {
            var uri = new UriBuilder(base.CreateRequestUri());
            var query = HttpUtility.ParseQueryString(uri.Query);
            query["api-version"] = "2026-03-01";
            uri.Query = query.ToString();
            return uri.Uri.AbsoluteUri;
        }

        internal async Task<bool> ExistsAsync()
        {
            Publisher.Token.ThrowIfCancellationRequested();
            using HttpRequestMessage request = await CreateRequestMessage();
            using HttpResponseMessage response = await HttpPipeline.SendAsync(request, Publisher);
            await Publisher.Publish(Events.TracingHttpResponseMessage, response);
            // Absence is expected during create preflight, not a failed command or resource output.
            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            await response.EnsureSuccessAsync(Publisher);
            return true;
        }
    }
}
