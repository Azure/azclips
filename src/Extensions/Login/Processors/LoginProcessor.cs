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

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);
            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

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

                if (string.IsNullOrWhiteSpace(subscriptionId))
                {
                    throw new ArgumentException("A subscription id is required. Provide it with --subscription-id.");
                }
            }

            LoginRequest loginRequest = new()
            {
                TenantId = tenantId,
                SubscriptionId = subscriptionId,
                UseDeviceCodeFlow = UseDeviceCode
            };

            AuthResult authResult = await authService.LoginAsync(loginRequest);

            Profile resultProfile = new()
            {
                Name = targetName,
                TenantId = string.IsNullOrEmpty(tenantId) ? authResult.TenantId : tenantId,
                SubscriptionId = subscriptionId,
                CloudName = cloudName,
                AuthMethod = UseDeviceCode ? AuthMethod.DeviceCode : AuthMethod.Browser,
                UserPrincipalName = authResult.UserPrincipalName,
            };

            await profileService.SaveAndActivateProfileAsync(resultProfile);
            await WriteOutput(resultProfile);
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }
    }
}
