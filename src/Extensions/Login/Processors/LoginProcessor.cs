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

            // Resolve the tenant that actually owns the requested subscription. Without an explicit
            // --tenant, interactive/device-code login targets the identity's home tenant, which may not
            // own the subscription (a subscription can be homed in a different tenant). Persisting the
            // home tenant produces a broken profile whose refreshed token cannot access the subscription,
            // so correct it to the subscription's owning tenant here.
            string resolvedTenantId = tenantId;
            bool tenantResolvedFromSubscription = false;
            if (ShouldResolveTenantFromSubscription(reAuthName, authMode, tenantId, subscriptionId))
            {
                resolvedTenantId = await ResolveSubscriptionTenantAsync(authResult, subscriptionId);
                tenantResolvedFromSubscription = !string.IsNullOrEmpty(resolvedTenantId);
            }

            // If no subscription was provided, pick the first enabled one we can see.
            string effectiveSubscriptionId = subscriptionId;
            if (string.IsNullOrEmpty(effectiveSubscriptionId))
            {
                effectiveSubscriptionId = await ResolveDefaultSubscriptionAsync(authResult.AccessToken);
            }

            Profile resultProfile = new()
            {
                Name = targetName,
                TenantId = string.IsNullOrEmpty(resolvedTenantId) ? authResult.TenantId : resolvedTenantId,
                SubscriptionId = effectiveSubscriptionId,
                CloudName = cloudName,
                AuthMethod = authMode,
                UserPrincipalName = authResult.UserPrincipalName,
                ClientId = resultClientId,
                ManagedIdentityId = authMode == AuthMethod.ManagedIdentity
                    ? (resultManagedIdentityId ?? ManagedIdentityIdCodec.Format(loginRequest))
                    : null,
            };

            // When the subscription is homed in a different tenant than the one we signed into, verify we can
            // obtain a token for that tenant without prompting BEFORE persisting anything. If interactive
            // sign-in is required (for example, MFA or a conditional access policy) or the tenant cannot be
            // probed, fail the login without saving rather than persisting a profile whose subsequent commands
            // could not access the subscription.
            if (tenantResolvedFromSubscription)
            {
                await EnsureCrossTenantAccessibleOrThrowAsync(resultProfile, effectiveSubscriptionId);
            }

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

        /// <summary>
        /// Verifies the resolved owning tenant yields a token silently before the profile is persisted. For a
        /// cross-tenant subscription this is a hard requirement: if interactive sign-in is required (MFA or a
        /// conditional access policy) or the tenant cannot be probed, this throws so the login fails WITHOUT
        /// saving, rather than persisting a profile whose subsequent commands cannot access the subscription.
        /// </summary>
        private async Task EnsureCrossTenantAccessibleOrThrowAsync(Profile profile, string subscriptionId)
        {
            var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            bool canAcquire;
            try
            {
                var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
                canAcquire = await authService.CanAcquireTokenSilentlyAsync(profile.TenantId, profile);
            }
            catch (Exception ex)
            {
                // The probe itself failed, so we cannot confirm access to the cross tenant. Fail closed: do
                // not save a profile we could not verify.
                logger.Debug($"Tenant access probe for '{profile.TenantId}' did not complete: {ex.Message}");
                throw new InvalidOperationException(
                    $"Signed in, but could not verify access to tenant '{profile.TenantId}' that owns subscription "
                    + $"'{subscriptionId}': {ex.Message} The profile was not saved. "
                    + $"Run 'az login --tenant {profile.TenantId}' to authenticate to that tenant, then retry.",
                    ex);
            }

            if (!canAcquire)
            {
                throw new InvalidOperationException(
                    $"Signed in, but subscription '{subscriptionId}' is homed in tenant '{profile.TenantId}', which "
                    + "requires additional sign-in (for example, multifactor authentication or a conditional access policy). "
                    + "The profile was not saved. "
                    + $"Run 'az login --tenant {profile.TenantId}' to finish authenticating to that tenant, then retry.");
            }
        }

        /// <summary>
        /// Determines whether the owning tenant should be resolved from the subscription. This applies only to
        /// a fresh interactive/device-code login (not a re-auth or managed identity) where the caller supplied a
        /// subscription but no explicit <c>--tenant</c>, so the signed-in home tenant may not own the subscription.
        /// </summary>
        private static bool ShouldResolveTenantFromSubscription(
            string? reAuthName,
            AuthMethod authMode,
            string tenantId,
            string subscriptionId)
        {
            return reAuthName is null
                && authMode != AuthMethod.ManagedIdentity
                && string.IsNullOrEmpty(tenantId)
                && !string.IsNullOrEmpty(subscriptionId);
        }

        /// <summary>
        /// Resolves the tenant that owns <paramref name="subscriptionId"/>, returning an empty string
        /// when the signed-in tenant already owns it so the caller falls back to the token's tenant.
        /// Throws when the owning tenant genuinely cannot be determined (the ARM lookup failed), so the
        /// login fails before saving rather than persisting an unverified, possibly-wrong tenant.
        /// </summary>
        private async Task<string> ResolveSubscriptionTenantAsync(AuthResult authResult, string subscriptionId)
        {
            var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            try
            {
                var subscriptionService = AzclipsServiceProvider.GetRequiredService<ISubscriptionService>();
                var owningTenant = await subscriptionService.ResolveSubscriptionTenantAsync(authResult.AccessToken, subscriptionId);

                if (!string.IsNullOrEmpty(owningTenant)
                    && !string.Equals(owningTenant, authResult.TenantId, StringComparison.OrdinalIgnoreCase))
                {
                    logger.Verbose(
                        $"Subscription '{subscriptionId}' is homed in tenant '{owningTenant}', not the signed-in tenant "
                        + $"'{authResult.TenantId}'. Using the subscription's tenant for the profile.");
                    return owningTenant;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                // The owning tenant could not be determined. Fail the login here (before the profile is
                // saved) rather than falling back to the signed-in tenant: persisting an unverified tenant
                // would produce a broken profile whose subsequent commands fail against the subscription.
                logger.Debug(
                    $"Could not resolve the owning tenant for subscription '{subscriptionId}': {ex.Message}");
                throw new InvalidOperationException(
                    $"Could not determine the tenant that owns subscription '{subscriptionId}': {ex.Message} "
                    + "The profile was not saved to avoid persisting an unverified tenant. "
                    + "Retry, or specify the tenant explicitly with '--tenant <tenantId>'.",
                    ex);
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
