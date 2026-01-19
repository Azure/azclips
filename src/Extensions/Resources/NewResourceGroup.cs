using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.Abstractions.Runtime.Handler;
using Core.Engine;
using Microsoft.Azure.Cirrus.Extensions.Resources.Client;
using Microsoft.Azure.Cirrus.Extensions.Resources.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.Cirrus.Extensions.Resources.Commands
{
    public class NewResourceGroupCommand : IProcessor, IEventListener
    {
        private ICommandContext _commandContext = null!;
        private readonly JsonSerializerOptions _jsonOptions = null!;
        private ICommandOutputHandler _outputHandler = null!;

        [ArgumentInfoAttribute(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; }

        [ArgumentInfo(
        Required = true,
        Description = @"The name of the resource group.",
        SerializedName = @"Name")]
        public string Name { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"The resource id of the resource group.",
        SerializedName = @"ResourceId")]
        public string ResourceId { get; set; } = "";

        [ArgumentInfo(
        Required = true,
        Description = @"Location for resource group.",
        SerializedName = @"Location")]
        public string Location { get; set; } = "";

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor SetCommandContext(ICommandContext context)
        {
            _commandContext = context;
            return this;
        }

        public async Task ProcessAsync()
        {
            if (_commandContext == null)
            {
                throw new InvalidOperationException("Command context is not set. Please set the command context before executing the command.");
            }

            _outputHandler = _commandContext.CommandOutputHandler;

            // construct URL
            var url = $"https://management.azure.com/subscriptions/{SubscriptionId}/resourcegroups/{Name}?api-version=2025-04-01";
            //TODO: set_jsonOptions from command context
            using var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(new ResourceGroup("eastus", Name), options: _jsonOptions)
            };
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            HttpResponseMessage response = await _commandContext.HttpPipeline.SendAsync(request, this);
            ResourceGroup rg = (await response.Content.ReadFromJsonAsync<ResourceGroup>(_jsonOptions))!;
            await _outputHandler.WriteOutput(rg);
        }

        public object Process()
        {
            var serviceProvider = Bootstrapper.ServiceProviderAsync.GetAwaiter().GetResult();
            var activitySource = serviceProvider.GetService<ITelemetryService>()?.ActivitySource;
            using var _ = activitySource?.StartActivity($"{nameof(NewResourceGroupCommand)}.{nameof(Process)}");

            var authService = serviceProvider.GetRequiredService<IAuthService>();
            var authContextService = serviceProvider.GetRequiredService<IAuthContextService>();
            var authContext = authContextService.GetCurrentContextAsync().GetAwaiter().GetResult();

            if (authContext == null)
            {
                throw new InvalidOperationException("Not authenticated. Please run 'azv2 login' first.");
            }

            var authResult = authService
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: authContext.TenantId, promptOnError: false).GetAwaiter().GetResult();
            return new ResourcesClient { ActivitySource = activitySource }.CreateOrUpdateResourceGroupAsync(SubscriptionId ?? authContext.SubscriptionId, Name, new ResourceGroup(Location, Name), authResult.AccessToken).GetAwaiter().GetResult();
        }

        public string Process(bool Serialize)
        {
            // TODO: models to provide model.ToJson() method for serialization.
            ResourceGroup result = (ResourceGroup)Process();
            if (Serialize)
            {
                return System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                throw new NotImplementedException("Non-serialized output is not implemented.");
            }
        }

        public Task Signal(string id, CancellationToken token, Func<object> createMessage) => throw new NotImplementedException();
    }
}