// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    public class LoginProcessor : LoginProcessorBase
    {
        [ArgumentInfo(
        Required = false,
        Description = @"Azure Subscription Id.",
        SerializedName = @"SubscriptionId")]
        public string? SubscriptionId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Tenant ID to login to.",
        SerializedName = @"TenantId")]
        public string? TenantId { get; set; } = "";

        [ArgumentInfo(
        Required = false,
        Description = @"Use device code flow for authentication.",
        SerializedName = @"UseDeviceCode")]
        public bool? UseDeviceCode { get; set; } = null;

        [ArgumentInfo(
        Required = false,
        Description = @"Create or overwrite a named profile with the login result (mutually exclusive with --profile).",
        SerializedName = @"OutProfile")]
        public string? OutProfile { get; set; } = null;

        // Managed identity

        [ParameterGroup("Managed Identity Arguments")]
        [ArgumentInfo(
        Required = false,
        Description = @"Login using managed identity in the current environment.",
        SerializedName = @"Identity")]
        public bool? Identity { get; set; } = null;

        [ParameterGroup("Managed Identity Arguments")]
        [ArgumentInfo(
        Required = false,
        Description = @"Client ID of the user-assigned managed identity.",
        SerializedName = @"ClientId")]
        public string? ClientId { get; set; } = null;

        [ParameterGroup("Managed Identity Arguments")]
        [ArgumentInfo(
        Required = false,
        Description = @"Object ID of the user-assigned managed identity.",
        SerializedName = @"ObjectId")]
        public string? ObjectId { get; set; } = null;

        [ParameterGroup("Managed Identity Arguments")]
        [ArgumentInfo(
        Required = false,
        Description = @"Resource ID of the user-assigned managed identity.",
        SerializedName = @"ResourceId")]
        public string? ResourceId { get; set; } = null;

        // Service principal (federated / OIDC)

        [ArgumentInfo(
        Required = false,
        Description = @"Login as a service principal using a federated (OIDC) token.",
        SerializedName = @"ServicePrincipal")]
        public bool? ServicePrincipal { get; set; } = null;

        [ArgumentInfo(
        Required = false,
        Description = @"Federated token issued by a trusted external identity provider.",
        SerializedName = @"FederatedToken")]
        public string? FederatedToken { get; set; } = null;

        private const string ParamSetManagedIdentity = "ManagedIdentity";
        private const string ParamSetDeviceCode = "DeviceCode";
        private const string ParamSetFederatedIdentity = "FederatedIdentity";
        private const string ParamSetProfileReauth = "ProfileReauth";
        private const string ParamSetBrowser = "Browser";

        private CommandMetadata? _cliCommandMetadataCache;
        private CommandMetadata? _psCommandMetadataCache;

        protected override void DefineParameterSets() {
            Resolver.DefineSet(ParamSetManagedIdentity)
                .WithMandatory(c => ((LoginProcessor)c).Identity)
                // The subscription is optional; the tenant is not applicable (the MI token carries it).
                .WithOptional(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).ClientId)
                .WithOptional(c => ((LoginProcessor)c).ObjectId)
                .WithOptional(c => ((LoginProcessor)c).ResourceId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSetDeviceCode)
                .WithMandatory(c => ((LoginProcessor)c).UseDeviceCode)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).TenantId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSetFederatedIdentity)
                .WithMandatory(c => ((LoginProcessor)c).ServicePrincipal)
                .WithMandatory(c => ((LoginProcessor)c).FederatedToken)
                .WithMandatory(c => ((LoginProcessor)c).ClientId)
                .WithMandatory(c => ((LoginProcessor)c).TenantId)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSetProfileReauth)
                .WithMandatory(c => ((LoginProcessor)c).Profile)
                .WithOptional(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).FederatedToken);

            Resolver.DefineSet(ParamSetBrowser)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).TenantId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);
        }

        /// <summary>
        /// Resolves the active parameter set, reporting an unresolvable combination as a usage error
        /// that names parameters as the user typed them on the invoking surface.
        /// </summary>
        private string ResolveParameterSet()
        {
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            try
            {
                return Resolver.Resolve(this, name => FormatParameterName(name, clientType)).Name;
            }
            catch (ParameterSetResolutionException ex)
            {
                throw AzclipsErrorException.InvalidInput(ex.Message, innerException: ex);
            }
        }

        public override async Task ProcessAsync()
        {
            await Publisher.Publish(Events.ProcessorProcessAsyncStart);

            ValidateParameterValues();
            string parameterSet = ResolveParameterSet();
            ValidateArguments(parameterSet);

            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            var profileService = AzclipsServiceProvider.GetRequiredService<IProfileService>();

            string? reAuthName = Profile;
            string? outName = OutProfile;


            string targetName;
            string? tenantId;
            string? subscriptionId;
            string cloudName;
            AuthMethod authMode;
            string? miClientId;
            string? miObjectId;
            string? miResourceId;
            string? resultClientId;
            string? resultManagedIdentityId;
            string? federatedToken;

            if (reAuthName is not null)
            {
                // Re-authenticate an existing profile: reproduce the profile's stored connection
                // state and authentication method rather than deriving auth mode from CLI clientParamNameFormatters.
                Profile existing = await profileService.GetProfileAsync(reAuthName)
                    ?? throw AzclipsErrorException.ProfileNotFound($"Profile '{reAuthName}' does not exist.");
                targetName = existing.Name;

                // The profile contributes the connection state, the caller the credential. The stored
                // assertion backs silent refresh between logins, not the login itself.
                bool servicePrincipalReAuth = existing.AuthMethod == AuthMethod.Federated;
                if (servicePrincipalReAuth && string.IsNullOrWhiteSpace(FederatedToken))
                {
                    throw AzclipsErrorException.InvalidInput(
                        $"Profile '{existing.Name}' signed in as a service principal, so re-authentication requires a " +
                        $"newly issued token in '{FormatParameterName(nameof(FederatedToken), AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType)}'.");
                }

                tenantId = existing.TenantId;
                subscriptionId = existing.SubscriptionId;
                cloudName = string.IsNullOrEmpty(existing.CloudName) ? DefaultCloudName : existing.CloudName;
                authMode = existing.AuthMethod;
                resultClientId = existing.ClientId;
                resultManagedIdentityId = existing.ManagedIdentityId;

                // Rotated on every re-auth; other auth methods must not carry one.
                federatedToken = servicePrincipalReAuth ? FederatedToken?.Trim() : null;

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
                targetName = outName ?? ProfileNameValidator.ReservedDefaultName;
                tenantId = TenantId;
                subscriptionId = SubscriptionId;
                cloudName = DefaultCloudName;
                authMode = Identity == true ? AuthMethod.ManagedIdentity
                     : ServicePrincipalMethod()
                       ?? (UseDeviceCode == true ? AuthMethod.DeviceCode : AuthMethod.Browser);
                // The cache partition is named after this, so {braced}, upper case and hyphenless
                // spellings of one application must not each get their own file.
                string? clientId = string.IsNullOrEmpty(ClientId) ? null : Guid.Parse(ClientId).ToString("D");

                // --client-id is shared with the service principal flow.
                bool managedIdentity = authMode == AuthMethod.ManagedIdentity;
                miClientId = managedIdentity ? clientId : null;
                miObjectId = managedIdentity && !string.IsNullOrEmpty(ObjectId) ? ObjectId : null;
                miResourceId = managedIdentity && !string.IsNullOrEmpty(ResourceId) ? ResourceId : null;
                resultClientId = clientId;
                resultManagedIdentityId = null;
                federatedToken = string.IsNullOrEmpty(FederatedToken) ? null : FederatedToken;
            }

            LoginRequest loginRequest = new()
            {
                TenantId = tenantId,
                SubscriptionId = subscriptionId,
                AuthMode = authMode,
                ManagedIdentityClientId = miClientId,
                ManagedIdentityObjectId = miObjectId,
                ManagedIdentityResourceId = miResourceId,
                ServicePrincipalClientId = authMode == AuthMethod.Federated ? resultClientId : null,
                FederatedToken = federatedToken,
            };

            await Publisher.Publish(Events.AuthBeforeTokenAcquisition);
            AuthResult authResult = await authService.LoginAsync(loginRequest);

            // Resolve the tenant that actually owns the requested subscription. Without an explicit
            // --tenant, interactive/device-code login targets the identity's home tenant, which may not
            // own the subscription (a subscription can be homed in a different tenant). Persisting the
            // home tenant produces a broken profile whose refreshed token cannot access the subscription,
            // so correct it to the subscription's owning tenant here.
            string? resolvedTenantId = tenantId;
            bool tenantResolvedFromSubscription = false;
            if (ShouldResolveTenantFromSubscription(reAuthName, authMode, tenantId, subscriptionId))
            {
                resolvedTenantId = await ResolveSubscriptionTenantAsync(authResult, subscriptionId);
                tenantResolvedFromSubscription = !string.IsNullOrEmpty(resolvedTenantId);
            }

            // If no subscription was provided, pick the first enabled one we can see.
            string? effectiveSubscriptionId = subscriptionId;
            if (string.IsNullOrEmpty(effectiveSubscriptionId))
            {
                effectiveSubscriptionId = await ResolveDefaultSubscriptionAsync(authResult.AccessToken);
            }

            Profile resultProfile = new Profile
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
            }.WithRuntimeMetadata(authResult);

            await Publisher.Publish(Events.AuthAfterTokenAcquisition, new EventData { Payload = resultProfile });

            // When the subscription is homed in a different tenant than the one we signed into, verify we can
            // obtain a token for that tenant without prompting BEFORE persisting anything. If interactive
            // sign-in is required (for example, MFA or a conditional access policy) or the tenant cannot be
            // probed, fail the login without saving rather than persisting a profile whose subsequent commands
            // could not access the subscription.
            if (tenantResolvedFromSubscription)
            {
                await EnsureCrossTenantAccessible(resultProfile, effectiveSubscriptionId);
            }

            await profileService.SaveAndActivateProfileAsync(resultProfile);

            await WriteOutput(ProfileResult.From(resultProfile, isActive: true));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        /// <summary>Validates and normalizes individual values before parameter-set resolution.</summary>
        private void ValidateParameterValues()
        {
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            string clientParamNameFormatter(string propertyName) => FormatParameterName(propertyName, clientType);

            void ValidateNotWhitespace(string? value, string propertyName)
            {
                if (!string.IsNullOrEmpty(value) && string.IsNullOrWhiteSpace(value))
                {
                    throw AzclipsErrorException.InvalidInput(
                        $"usage error: '{clientParamNameFormatter(propertyName)}' must not be whitespace.");
                }
            }

            Profile = string.IsNullOrWhiteSpace(Profile) ? null : Profile.Trim();
            OutProfile = string.IsNullOrWhiteSpace(OutProfile) ? null : OutProfile.Trim();

            ValidateNotWhitespace(SubscriptionId, nameof(SubscriptionId));
            ValidateNotWhitespace(TenantId, nameof(TenantId));
            ValidateNotWhitespace(ClientId, nameof(ClientId));
            ValidateNotWhitespace(ObjectId, nameof(ObjectId));
            ValidateNotWhitespace(ResourceId, nameof(ResourceId));
            ValidateNotWhitespace(FederatedToken, nameof(FederatedToken));

            if (OutProfile is not null)
            {
                ProfileNameValidator.ValidateForCreation(OutProfile);
            }
            SubscriptionIdValidator.Validate(SubscriptionId, clientParamNameFormatter(nameof(SubscriptionId)), allowEmpty: true);

            ValidateGuid(ClientId, nameof(ClientId), clientParamNameFormatter);
            ValidateGuid(ObjectId, nameof(ObjectId), clientParamNameFormatter);
        }

        /// <summary>Validates login argument combinations within the resolved parameter set.</summary>
        private void ValidateArguments(string parameterSet)
        {
            if (parameterSet == ParamSetManagedIdentity)
            {
                var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
                ValidateManagedIdentityArguments(
                    ClientId, ObjectId, ResourceId, name => FormatParameterName(name, clientType));
            }
        }

        private static void ValidateManagedIdentityArguments(
            string? clientId, string? objectId, string? resourceId, Func<string, string> clientParamNameFormatter)
        {
            // At most one selector for a user-assigned MI; none means system-assigned.
            var identityIdCount = new[] { clientId, objectId, resourceId }.Count(id => !string.IsNullOrEmpty(id));
            if (identityIdCount > 1)
            {
                throw AzclipsErrorException.InvalidInput(
                    $"usage error: Provide only one of '{clientParamNameFormatter(nameof(ClientId))}', '{clientParamNameFormatter(nameof(ObjectId))}', '{clientParamNameFormatter(nameof(ResourceId))}'.");
            }
        }

        /// <summary>
        /// Turns what would be a remote token failure — or, for the client id, a cache file named from
        /// arbitrary input — into a usage error.
        /// </summary>
        private static void ValidateGuid(string? value, string propertyName, Func<string, string> clientParamNameFormatter)
        {
            if (!string.IsNullOrEmpty(value) && !Guid.TryParse(value, out _))
            {
                throw AzclipsErrorException.InvalidInput(
                    $"usage error: '{clientParamNameFormatter(propertyName)}' must be a GUID. '{value}' is not.");
            }
        }

        /// <summary>
        /// Which credential a service principal is signing in with, or null when this is not one.
        /// The credential decides, not <see cref="ServicePrincipal"/>: a secret or a certificate needs
        /// its own authenticator, so it gets its own <see cref="AuthMethod"/>.
        /// </summary>
        private AuthMethod? ServicePrincipalMethod() =>
            string.IsNullOrEmpty(FederatedToken) ? null : AuthMethod.Federated;

        /// <summary>
        /// Formats a parameter as the clientParamNameFormatter the user actually typed on the invoking surface:
        /// <c>--kebab-case</c> on the CLI, <c>-PascalCase</c> in PowerShell. The user-facing form is
        /// sourced from <see cref="ExtensionIndex"/> so message text stays in sync with the metadata
        /// that drives argument parsing — a rename to a <c>ParameterMetadata.InvocationName</c> updates
        /// both. Because this processor is shared across both surfaces, hardcoding one form would
        /// mis-instruct users on the other.
        /// </summary>
        /// <param name="propertyName">The name of the processor property that maps to a parameter (e.g., nameof(SubscriptionId)).</param>
        /// <param name="clientType">The invocation surface, resolved from DI by the caller.</param>
        private string FormatParameterName(string propertyName, ClientType clientType)
        {
            const string CliPrefix = "--";
            const string PsPrefix = "-";

            switch (clientType)
            {
                case ClientType.CLI:
                    _cliCommandMetadataCache ??= new ExtensionIndex()
                        .GetAllCommands(ClientType.CLI)
                        .First(c => c.ProcessorType == GetType());
                    return CliPrefix + _cliCommandMetadataCache.Parameters.Values
                        .First(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal))
                        .InvocationName;

                case ClientType.PowerShell:
                    _psCommandMetadataCache ??= new ExtensionIndex()
                        .GetAllCommands(ClientType.PowerShell)
                        .First(c => c.ProcessorType == GetType());
                    return PsPrefix + _psCommandMetadataCache.Parameters.Values
                        .First(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal))
                        .InvocationName;

                default:
                    throw new NotSupportedException(
                        $"Unsupported client type '{clientType}' for parameter name formatting.");
            }
        }

        /// <summary>
        /// Verifies the resolved owning tenant yields a token silently before the profile is persisted. For a
        /// cross-tenant subscription this is a hard requirement: if interactive sign-in is required (MFA or a
        /// conditional access policy) or the tenant cannot be probed, this throws so the login fails WITHOUT
        /// saving, rather than persisting a profile whose subsequent commands cannot access the subscription.
        /// </summary>
        private async Task EnsureCrossTenantAccessible(Profile profile, string subscriptionId)
        {
            var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();

            // Render sign-in guidance in the caller's surface: PowerShell users need 'Connect-AzAccount
            // -TenantId', CLI users need 'az login --tenant-id'. Hardcoding one form mis-instructs the other.
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            string clientParamNameFormatter(string propertyName) => FormatParameterName(propertyName, clientType);
            string signInCommand = clientType == ClientType.PowerShell ? "Connect-AzAccount" : "az login";

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
                    + $"Run '{signInCommand} {clientParamNameFormatter(nameof(TenantId))} {profile.TenantId}' to authenticate to that tenant, then retry.",
                    ex);
            }

            if (!canAcquire)
            {
                throw AzclipsErrorException.NotSignedIn(
                    $"Signed in, but subscription '{subscriptionId}' is homed in tenant '{profile.TenantId}', which "
                    + "requires additional sign-in (for example, multifactor authentication or a conditional access policy). "
                    + "The profile was not saved. "
                    + $"Run '{signInCommand} {clientParamNameFormatter(nameof(TenantId))} {profile.TenantId}' to finish authenticating to that tenant, then retry.");
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
            string? tenantId,
            string? subscriptionId)
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
        private async Task<string> ResolveSubscriptionTenantAsync(AuthResult authResult, string? subscriptionId)
        {
            var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            string clientParamNameFormatter(string propertyName) => FormatParameterName(propertyName, clientType);

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
                    + $"Retry, or specify the tenant explicitly with '{clientParamNameFormatter(nameof(TenantId))} <tenantId>'.",
                    ex);
            }
        }

        /// <summary>Finds the default subscription id, or empty if discovery fails.</summary>
        private async Task<string> ResolveDefaultSubscriptionAsync(string accessToken)
        {
            var logger = AzclipsServiceProvider.GetRequiredService<ILoggingService>();
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            string clientParamNameFormatter(string propertyName) => FormatParameterName(propertyName, clientType);

            try
            {
                var subscriptionService = AzclipsServiceProvider.GetRequiredService<ISubscriptionService>();
                var subscriptions = await subscriptionService.ListSubscriptionsAsync(accessToken);
                var defaultSubscription = subscriptions.FirstOrDefault(s => s.IsEnabled)
                    ?? subscriptions.FirstOrDefault();

                if (defaultSubscription is null)
                {
                    logger.Warn("No subscriptions found for the logged-in identity. "
                        + $"Continuing with tenant-level access; pass '{clientParamNameFormatter(nameof(SubscriptionId))}' to target a subscription.");
                    return string.Empty;
                }

                return defaultSubscription.SubscriptionId;
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to auto-discover subscriptions: {ex.Message}. "
                    + $"Continuing without a default subscription; pass '{clientParamNameFormatter(nameof(SubscriptionId))}' to set one.");
                return string.Empty;
            }
        }
    }
}
