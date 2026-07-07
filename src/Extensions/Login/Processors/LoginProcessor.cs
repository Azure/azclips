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
            AuthMethod authMode;
            string? miClientId;
            string? miObjectId;
            string? miResourceId;
            string? resultClientId;
            string? resultManagedIdentityId;

            if (reAuthName is not null)
            {
                // Re-authenticate an existing profile: reproduce the profile's stored connection
                // state and authentication method rather than deriving auth mode from CLI flags.
                Profile existing = await profileService.GetProfileAsync(reAuthName)
                    ?? throw new ArgumentException($"Profile '{reAuthName}' does not exist.");
                targetName = existing.Name;
                tenantId = existing.TenantId;
                subscriptionId = existing.SubscriptionId;
                cloudName = string.IsNullOrEmpty(existing.CloudName) ? DefaultCloudName : existing.CloudName;
                authMode = existing.AuthMethod;
                resultClientId = existing.ClientId;
                resultManagedIdentityId = existing.ManagedIdentityId;

                // Rebuild the managed-identity selector from the stored key so token refresh
                // targets the same identity instead of silently falling back to system-assigned.
                var (idType, idValue) = authMode == AuthMethod.ManagedIdentity
                    ? ManagedIdentityIdCodec.Parse(existing.ManagedIdentityId)
                    : (ManagedIdentityIdType.SystemAssigned, (string?)null);
                miClientId = idType == ManagedIdentityIdType.ClientId ? idValue : null;
                miObjectId = idType == ManagedIdentityIdType.ObjectId ? idValue : null;
                miResourceId = idType == ManagedIdentityIdType.ResourceId ? idValue : null;
            }
            else
            {
                targetName = outName ?? ProfileNameValidator.ReservedTempName;
                tenantId = TenantId;
                subscriptionId = SubscriptionId;
                cloudName = DefaultCloudName;
                authMode = Identity ? AuthMethod.ManagedIdentity
                     : UseDeviceCode ? AuthMethod.DeviceCode
                     : AuthMethod.Browser;
                miClientId = string.IsNullOrEmpty(ClientId) ? null : ClientId;
                miObjectId = string.IsNullOrEmpty(ObjectId) ? null : ObjectId;
                miResourceId = string.IsNullOrEmpty(ResourceId) ? null : ResourceId;
                resultClientId = string.IsNullOrEmpty(ClientId) ? null : ClientId;
                resultManagedIdentityId = null;
            }

            LoginRequest loginRequest = new()
            {
                TenantId = tenantId,
                SubscriptionId = subscriptionId,
                AuthMode = authMode,
                ManagedIdentityClientId = miClientId,
                ManagedIdentityObjectId = miObjectId,
                ManagedIdentityResourceId = miResourceId,
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
                AuthMethod = authMode,
                UserPrincipalName = authResult.UserPrincipalName,
                ClientId = resultClientId,
                ManagedIdentityId = authMode == AuthMethod.ManagedIdentity
                    ? (resultManagedIdentityId ?? ManagedIdentityIdCodec.Format(loginRequest))
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

            // Re-authentication reuses the profile's stored authentication method, so auth-mode
            // flags would be silently ignored. Reject them to keep re-auth deterministic.
            if (reAuthName is not null &&
                (Identity || UseDeviceCode
                 || !string.IsNullOrEmpty(ClientId)
                 || !string.IsNullOrEmpty(ObjectId)
                 || !string.IsNullOrEmpty(ResourceId)))
            {
                throw new ArgumentException(
                    "Authentication flags ('--identity', '--use-device-code', '--client-id', '--object-id', '--resource-id') cannot be combined with '--profile'. "
                    + "Re-authentication reuses the profile's stored authentication method.");
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
