using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Login.Processors
{
    public class LoginProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = true,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string SubscriptionId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Tenant ID to login to.",
        SerializedName = @"TenantId")]
        public string TenantId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Use device code flow for authentication.",
        SerializedName = @"UseDeviceCode")]
        public bool UseDeviceCode { get; set; } = false;

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var telemetryService = CirrusServiceProvider.GetRequiredService<ITelemetryService>();
            var authService = CirrusServiceProvider.GetRequiredService<IAuthService>();
            var authContextService = CirrusServiceProvider.GetRequiredService<IAuthContextService>();

            LoginRequest loginRequest = new()
            {
                TenantId = TenantId,
                SubscriptionId = SubscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };

            var authResult = authService.LoginAsync(loginRequest).GetAwaiter().GetResult();
            AuthContext authContext = new AuthContext(TenantId: authResult.TenantId, SubscriptionId: SubscriptionId);
            authContextService.SaveContextAsync(authContext).GetAwaiter().GetResult();
            await WriteOutput(authContext);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
