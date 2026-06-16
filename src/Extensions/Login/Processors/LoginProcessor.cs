// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Login.Processors
{
    public class LoginProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Subscription ID.",
        SerializedName = @"Subscription")]
        public string Subscription { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Tenant name or ID. Required for service principal login.",
        SerializedName = @"Tenant")]
        public string Tenant { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Use device code flow for authentication.",
        SerializedName = @"UseDeviceCode")]
        public bool UseDeviceCode { get; set; } = false;

        // ── Managed Identity ──

        [ArgumentInfo(
        Required = false,
        Description = @"Login using managed identity in the current environment.",
        SerializedName = @"Identity")]
        public bool Identity { get; set; } = false;

        [ArgumentInfo(
        Required = false,
        Description = @"Client ID of the user-assigned managed identity.",
        SerializedName = @"ClientId")]
        public string ClientId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Object ID of the user-assigned managed identity.",
        SerializedName = @"ObjectId")]
        public string ObjectId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Resource ID of the user-assigned managed identity.",
        SerializedName = @"ResourceId")]
        public string ResourceId { get; set; } = "";

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var authContextService = AzclipsServiceProvider.GetRequiredService<IAuthContextService>();

            ValidateArguments();

            var mode = Identity ? LoginMode.ManagedIdentity
                     : UseDeviceCode ? LoginMode.DeviceCode
                     : LoginMode.Interactive;

            LoginRequest loginRequest = new()
            {
                TenantId = Tenant,
                SubscriptionId = Subscription,
                Mode = mode,
                ManagedIdentityClientId = string.IsNullOrEmpty(ClientId) ? null : ClientId,
                ManagedIdentityObjectId = string.IsNullOrEmpty(ObjectId) ? null : ObjectId,
                ManagedIdentityResourceId = string.IsNullOrEmpty(ResourceId) ? null : ResourceId,
            };

            var authResult = await authService.LoginAsync(loginRequest);

            // Subscription discovery: when the caller didn't specify a subscription, enumerate the
            // subscriptions the token can access and select a default (first Enabled, else first),
            // matching Azure CLI (`az login`) and Azure PowerShell (`Connect-AzAccount`). When an
            // explicit --subscription-id is supplied, honor it as-is (treated as the preference).
            string effectiveSubscriptionId = Subscription;
            if (string.IsNullOrEmpty(effectiveSubscriptionId))
            {
                effectiveSubscriptionId = await ResolveDefaultSubscriptionAsync(authResult.AccessToken);
            }

            AuthContext authContext = new AuthContext(
                TenantId: authResult.TenantId,
                SubscriptionId: effectiveSubscriptionId,
                Mode: mode)
            {
                ManagedIdentityId = mode == LoginMode.ManagedIdentity
                    ? AuthContext.FormatManagedIdentityId(loginRequest)
                    : null,
            };
            await authContextService.SaveContextAsync(authContext);
            await WriteOutput(authContext);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        /// <summary>
        /// Validates the combination of login arguments, throwing an <see cref="InvalidOperationException"/>
        /// with an Azure CLI-style "usage error" message when an unsupported combination is supplied.
        /// </summary>
        private void ValidateArguments()
        {
            // Match Azure CLI behavior - reject --tenant with --identity.
            if (Identity && !string.IsNullOrEmpty(Tenant))
            {
                throw new InvalidOperationException(
                    "usage error: '--identity' is not applicable with '--tenant'. " +
                    "Managed identity tokens already contain tenant information.");
            }

            // Match Azure CLI's ManagedIdentityAuth.parse_ids - a user-assigned managed identity
            // must be selected by exactly one of --client-id / --object-id / --resource-id.
            var identityIdCount = new[] { ClientId, ObjectId, ResourceId }.Count(id => !string.IsNullOrEmpty(id));
            if (identityIdCount > 1)
            {
                throw new InvalidOperationException(
                    "usage error: Provide only one of '--client-id', '--object-id', '--resource-id'.");
            }

            // '--identity' and '--use-device-code' select mutually exclusive flows.
            if (Identity && UseDeviceCode)
            {
                throw new InvalidOperationException(
                    "usage error: '--identity' cannot be combined with '--use-device-code'.");
            }

            // The managed-identity selectors only apply to '--identity'.
            if (!Identity && identityIdCount > 0)
            {
                throw new InvalidOperationException(
                    "usage error: '--client-id', '--object-id' and '--resource-id' require '--identity'.");
            }
        }

        /// <summary>
        /// Lists the subscriptions accessible to the supplied token and returns the default
        /// subscription id (first Enabled, otherwise the first returned). Returns an empty string
        /// when the identity has tenant-level access only (no subscriptions) or discovery fails —
        /// in which case the login still succeeds with an empty subscription context.
        /// </summary>
        private async Task<string> ResolveDefaultSubscriptionAsync(string accessToken)
        {
            var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            try
            {
                var subscriptionService = AzclipsServiceProvider.GetRequiredService<ISubscriptionService>();
                var subscriptions = await subscriptionService.ListSubscriptionsAsync(accessToken);
                var defaultSubscription = subscriptions.FirstOrDefault(s => s.IsEnabled)
                    ?? subscriptions.FirstOrDefault();

                if (defaultSubscription is null)
                {
                    logger.Warn("No subscriptions found for the logged-in identity. "
                        + "Continuing with tenant-level access; pass '--subscription-id' to target a subscription.");
                    return string.Empty;
                }

                return defaultSubscription.SubscriptionId;
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to auto-discover subscriptions: {ex.Message}. "
                    + "Continuing without a default subscription; pass '--subscription-id' to set one.");
                return string.Empty;
            }
        }
    }
}
