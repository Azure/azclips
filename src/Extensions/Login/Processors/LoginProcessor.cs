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

        [ArgumentInfo(
        Required = false,
        Description = @"Create or overwrite a named profile with the login result (mutually exclusive with --profile).",
        SerializedName = @"OutProfile")]
        public string? OutProfile { get; set; }

        // Managed identity

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
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            string? reAuthName = string.IsNullOrWhiteSpace(Profile) ? null : Profile.Trim();
            string? outName = string.IsNullOrWhiteSpace(OutProfile) ? null : OutProfile.Trim();

            ValidateArguments();

            string targetName;
            string tenantId;
            string subscriptionId;
            string cloudName;


            if (reAuthName is not null)
            {
                // Re-authenticate an existing profile: source connection state from the named profile.
                Profile existing = await profileService.GetProfileAsync(reAuthName)
                    ?? throw new ArgumentException($"Profile '{reAuthName}' does not exist.");
                targetName = existing.Name;
                tenantId = existing.TenantId;
                subscriptionId = existing.SubscriptionId;
                cloudName = string.IsNullOrEmpty(existing.CloudName) ? DefaultCloudName : existing.CloudName;
            }
            else
            {
                targetName = outName ?? ProfileNameValidator.ReservedTempName;
                tenantId = TenantId;
                subscriptionId = SubscriptionId;
                cloudName = DefaultCloudName;
            }


            var mode = Identity ? AuthMethod.ManagedIdentity
                     : UseDeviceCode ? AuthMethod.DeviceCode
                     : AuthMethod.Browser;

            LoginRequest loginRequest = new()
            {
                TenantId = tenantId,
                SubscriptionId = subscriptionId,
                Mode = mode,
                ManagedIdentityClientId = string.IsNullOrEmpty(ClientId) ? null : ClientId,
                ManagedIdentityObjectId = string.IsNullOrEmpty(ObjectId) ? null : ObjectId,
                ManagedIdentityResourceId = string.IsNullOrEmpty(ResourceId) ? null : ResourceId,
            };

            AuthResult authResult = await authService.LoginAsync(loginRequest);

            // If no subscription was provided, pick the first enabled one we can see.
            string effectiveSubscriptionId = subscriptionId;
            if (string.IsNullOrEmpty(effectiveSubscriptionId))
            {
                effectiveSubscriptionId = await ResolveDefaultSubscriptionAsync(authResult.AccessToken);
            }

            Profile resultProfile = new()
            {
                Name = targetName,
                TenantId = string.IsNullOrEmpty(tenantId) ? authResult.TenantId : tenantId,
                SubscriptionId = effectiveSubscriptionId,
                CloudName = cloudName,
                AuthMethod = mode,
                UserPrincipalName = authResult.UserPrincipalName,
                ClientId = string.IsNullOrEmpty(ClientId) ? null : ClientId,
                ManagedIdentityId = mode == AuthMethod.ManagedIdentity
                    ? ManagedIdentityIdCodec.Format(loginRequest)
                    : null,
            };

            await profileService.SaveAndActivateProfileAsync(resultProfile);
            await WriteOutput(resultProfile);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        /// <summary>Validates login argument combinations.</summary>
        private void ValidateArguments()
        {
            string? reAuthName = string.IsNullOrWhiteSpace(Profile) ? null : Profile.Trim();
            string? outName = string.IsNullOrWhiteSpace(OutProfile) ? null : OutProfile.Trim();

            // Enforce mutual exclusivity and the reserved-name rule before any authentication (fail fast).
            if (reAuthName is not null && outName is not null)
            {
                throw new ArgumentException("'--profile' and '--out-profile' cannot be used together. '--profile' re-authenticates an existing profile; '--out-profile' creates a new one.");
            }
            if (outName is not null)
            {
                ProfileNameValidator.ValidateForCreation(outName);
            }

            // A subscription id is required for a fresh interactive/device-code login. It is not
            // needed for managed identity (token carries it) or profile re-auth (sourced from the profile).
            if (!Identity && reAuthName is null && string.IsNullOrWhiteSpace(SubscriptionId))
            {
                throw new ArgumentException("A subscription id is required. Provide it with --subscription-id.");
            }

            // --tenant doesn't apply to --identity.
            if (Identity && !string.IsNullOrEmpty(TenantId))
            {
                throw new InvalidOperationException(
                    "usage error: '--identity' is not applicable with '--tenant'. " +
                    "Managed identity tokens already contain tenant information.");
            }

            // user-assigned MI must use exactly one selector.
            var identityIdCount = new[] { ClientId, ObjectId, ResourceId }.Count(id => !string.IsNullOrEmpty(id));
            if (identityIdCount > 1)
            {
                throw new InvalidOperationException(
                    "usage error: Provide only one of '--client-id', '--object-id', '--resource-id'.");
            }

            // --identity and --use-device-code are mutually exclusive.
            if (Identity && UseDeviceCode)
            {
                throw new InvalidOperationException(
                    "usage error: '--identity' cannot be combined with '--use-device-code'.");
            }

            // MI selectors require --identity.
            if (!Identity && identityIdCount > 0)
            {
                throw new InvalidOperationException(
                    "usage error: '--client-id', '--object-id' and '--resource-id' require '--identity'.");
            }


        }

        /// <summary>Finds the default subscription id, or empty if discovery fails.</summary>
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
