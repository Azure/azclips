// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;
using Microsoft.Azure.Azclips.Extensions.Accounts.Models;
using Microsoft.Azure.Azclips.Extensions.Accounts.ParameterSets;
using System.Diagnostics;

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
        Description = @"Secret of a service principal.",
        SerializedName = @"ClientSecret")]
        public string ClientSecret { get; set; } = ""; // todo: rename. Also we don't want to reuse the username/password pattern of classic CLI (we want to deprecate ropc anyway)

        [ArgumentInfo(
        Required = false,
        Description = @"Federated token issued by a trusted external identity provider.",
        SerializedName = @"FederatedToken")]
        public string? FederatedToken { get; set; } = null;

        private IProfileService ProfileService => _lazyProfileService.Value;
        private readonly Lazy<IProfileService> _lazyProfileService = new(() => AzclipsServiceProvider.GetRequiredService<IProfileService>());
        private ILoggingService Logger => _logger.Value;
        private readonly Lazy<ILoggingService> _logger = new(() => AzclipsServiceProvider.GetRequiredService<ILoggingService>());
        private IAuthService AuthService => _lazyAuthService.Value;
        private readonly Lazy<IAuthService> _lazyAuthService = new(() => AzclipsServiceProvider.GetRequiredService<IAuthService>());

        private static class ParamSet
        {
            public const string ManagedIdentity = "ManagedIdentity";
            public const string DeviceCode = "DeviceCode";
            public const string FederatedIdentity = "FederatedIdentity";
            public const string ProfileReauth = "ProfileReauth";
            public const string Interactive = "Browser";
            public const string SPWithSecret = "SPWithSecret";
        }

        private CommandMetadata? _cliCommandMetadataCache;
        private CommandMetadata? _psCommandMetadataCache;

        protected override void DefineParameterSets()
        {
            Resolver.DefineSet(ParamSet.ManagedIdentity)
                .WithMandatory(c => ((LoginProcessor)c).Identity)
                // The subscription is optional; the tenant is not applicable (the MI token carries it).
                .WithOptional(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).ClientId)
                .WithOptional(c => ((LoginProcessor)c).ObjectId)
                .WithOptional(c => ((LoginProcessor)c).ResourceId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSet.DeviceCode)
                .WithMandatory(c => ((LoginProcessor)c).UseDeviceCode)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).TenantId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSet.FederatedIdentity)
                .WithMandatory(c => ((LoginProcessor)c).ServicePrincipal)
                .WithMandatory(c => ((LoginProcessor)c).FederatedToken)
                .WithMandatory(c => ((LoginProcessor)c).ClientId)
                .WithMandatory(c => ((LoginProcessor)c).TenantId)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSet.ProfileReauth)
                .WithMandatory(c => ((LoginProcessor)c).Profile)
                .WithOptional(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).FederatedToken);

            Resolver.DefineSet(ParamSet.Interactive)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).TenantId)
                .WithOptional(c => ((LoginProcessor)c).OutProfile);

            Resolver.DefineSet(ParamSet.SPWithSecret)
                .WithMandatory(c => ((LoginProcessor)c).ClientId)
                .WithMandatory(c => ((LoginProcessor)c).ClientSecret)
                .WithMandatory(c => ((LoginProcessor)c).TenantId)
                .WithMandatory(c => ((LoginProcessor)c).SubscriptionId)
                .WithOptional(c => ((LoginProcessor)c).ServicePrincipal) // really necessary?
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
            ValidateParameterSet(parameterSet);

            var loginRequest = await CreateLoginRequestAsync(parameterSet);

            await Publisher.Publish(Events.AuthBeforeTokenAcquisition);
            var authResult = await AuthService.LoginAsync(loginRequest);

            TenantAndSubResolution resolution = await ResolveTenantAndSubAsync(loginRequest, authResult);

            Profile resultProfile = await SaveProfileAsync(loginRequest, authResult, resolution);

            await WriteOutput(ProfileResult.From(resultProfile, isActive: true));
            await Publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        private async Task<Profile> SaveProfileAsync(LoginRequest request, AuthResult authResult, TenantAndSubResolution resolution)
        {
            var existingProfile = Profile is null ? null : await ProfileService.GetProfileAsync(Profile);
            Profile resultProfile = new Profile
            {
                Name = existingProfile?.Name ?? OutProfile ?? ProfileNameValidator.ReservedDefaultName,
                TenantId = resolution.TenantID,
                SubscriptionId = resolution.SubscriptionID,
                CloudName = string.IsNullOrEmpty(existingProfile?.CloudName) ? DefaultCloudName : existingProfile.CloudName,
                AuthMethod = request.Credential.ToAuthMethod(),
                UserPrincipalName = authResult.UserPrincipalName,
                ClientId = request.Credential switch
                {
                    AuthCredential.ClientSecret clientSecret => clientSecret.ClientId,
                    AuthCredential.FederatedIdentity federated => federated.ClientId,
                    _ => null,
                },
                ManagedIdentityId = request.Credential is AuthCredential.ManagedIdentity
                    ? ManagedIdentityIdCodec.Format(request)
                    : null,
            }.WithRuntimeMetadata(authResult);

            await Publisher.Publish(Events.AuthAfterTokenAcquisition, new EventData { Payload = resultProfile });

            await ProfileService.SaveAndActivateProfileAsync(resultProfile);

            return resultProfile;
        }

        /// <summary>
        /// Resolve tenant and subscription according to user input.
        /// </summary>
        /// <param name="loginRequest"></param>
        /// <param name="authResult"></param>
        /// <returns>Tuple of resolved tenant ID (not null) and subscription ID (may be null)</returns>
        private async Task<TenantAndSubResolution> ResolveTenantAndSubAsync(LoginRequest loginRequest, AuthResult authResult)
        {
            // authResult is the source of truth because it's validated by MSAL,
            // whereas the loginRequest is just user input and may not be 100% accurate
            // tenant + subscription => exact match or error
            // tenant only => no subscription
            // subscription only => resolve subscription's owning tenant
            // nothing => home tenant + no subscription

            var subscriptionService = AzclipsServiceProvider.GetRequiredService<ISubscriptionService>();

            if (loginRequest.TenantId is not null && loginRequest.SubscriptionId is not null)
            {
                // request has tenant ID and subscription ID => exact match or error
                SubscriptionInfo subscription;
                try
                {
                    subscription = await subscriptionService.GetSubscriptionByIdAsync(authResult.AccessToken, loginRequest.SubscriptionId);
                }
                catch (HttpRequestException ex)
                {
                    throw AzclipsErrorException.InvalidInput($"Failed to access subscription '{loginRequest.SubscriptionId}': {ex.Message}. Make sure the subscription ID is correct and you have the necessary permissions.");
                }
                Debug.Assert(
                    string.Equals(loginRequest.SubscriptionId, subscription.SubscriptionId, StringComparison.OrdinalIgnoreCase),
                    $"Subscription ID in login request ({loginRequest.SubscriptionId}) should match the subscription ID in subscription info ({subscription.SubscriptionId}).");
                return new TenantAndSubResolution(authResult.TenantId, subscription.SubscriptionId);
            }

            if (loginRequest.TenantId is not null && loginRequest.SubscriptionId is null)
            {
                // tenant only => throw
                // alternatively return specified tenant + no subscription when we design no-sub ux
                throw AzclipsErrorException.InvalidInput("Please provide a subscription ID.");
            }

            if (loginRequest.TenantId is null && loginRequest.SubscriptionId is not null)
            {
                string? tid;
                // subscription only => resolve subscription's owning tenant
                if (loginRequest.Credential is AuthCredential.ManagedIdentity)
                {
                    // authenticator already resolves tenant ID, validate subscription
                    Debug.Assert(Guid.TryParse(authResult.TenantId, out _));
                    tid = authResult.TenantId;
                }
                else
                {
                    // resolves real tenant ID using a deliberate 401/403 GET subscription call
                    try
                    {
                        var owningTenant = await ResolveSubscriptionTenantAsync(authResult, loginRequest.SubscriptionId);
                        tid = string.IsNullOrEmpty(owningTenant) ? authResult.TenantId : owningTenant;
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw AzclipsErrorException.InvalidInput(
                            $"Failed to resolve subscription tenant: {ex.Message}",
                            innerException: ex);
                    }
                    // try acquiring a token from the resolved tenant
                    // might fail due to MFA requirement
                    try
                    {
                        if (!string.Equals(tid, authResult.TenantId, StringComparison.OrdinalIgnoreCase))
                        {
                            authResult = await AuthService.AcquireTokenSilentlyAsync(
                                loginRequest.Scopes, tid,
                                profile: new Profile
                                {
                                    Name = Profile ?? OutProfile ?? ProfileNameValidator.ReservedDefaultName,
                                    TenantId = tid,
                                    SubscriptionId = loginRequest.SubscriptionId,
                                    AuthMethod = loginRequest.Credential.ToAuthMethod(),
                                    UserPrincipalName = authResult.UserPrincipalName,
                                });
                        }
                    }
                    catch (Exception ex) // todo: MsalUiRequiredException expected, but we should not leak types from dependencies out of api service
                    {
                        throw AzclipsErrorException.InvalidInput(
                            $"""The subscription "{loginRequest.SubscriptionId}" belongs to tenant "{tid}", but authentication failed due to: "{ex.Message}". Please retry with '{FormatParameterName(nameof(TenantId), AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType)}'.""",
                            innerException: ex);
                    }
                }
                // validate the subscription exists
                SubscriptionInfo subscription;
                try
                {
                    subscription = await subscriptionService.GetSubscriptionByIdAsync(authResult.AccessToken, loginRequest.SubscriptionId);
                }
                catch (HttpRequestException ex)
                {
                    throw AzclipsErrorException.InvalidInput($"Failed to validate subscription '{loginRequest.SubscriptionId}': {ex.Message}", innerException: ex);
                } // todo: this is duplicated
                return new TenantAndSubResolution(tid, subscription.SubscriptionId);
            }

            if (loginRequest.TenantId is null && loginRequest.SubscriptionId is null)
            {
                // neither tenant nor subscription provided => throw
                // alternatively return home tenant + no subscription when the no-sub ux is designed
                throw AzclipsErrorException.InvalidInput("Neither tenant ID nor subscription ID was provided.");
            }

            throw AzclipsErrorException.InvalidInput("Dead code, should not reach here.");
        }

        private record TenantAndSubResolution(string TenantID, string SubscriptionID);

        private async Task<LoginRequest> CreateLoginRequestAsync(string parameterSet)
        {
            switch (parameterSet)
            {
                case ParamSet.Interactive:
                    return AuthRequestFactory.CreateInteractive(tenantId: TenantId, subscriptionId: SubscriptionId);
                case ParamSet.DeviceCode:
                    return AuthRequestFactory.CreateDeviceCode(tenantId: TenantId, subscriptionId: SubscriptionId);
                case ParamSet.SPWithSecret:
                    Debug.Assert(!string.IsNullOrEmpty(ClientId), "ClientId must not be null or empty");
                    Debug.Assert(!string.IsNullOrEmpty(ClientSecret), "ClientSecret must not be null or empty");
                    return AuthRequestFactory.CreateClientSecret(
                        clientId: ClientId,
                        clientSecret: ClientSecret,
                        tenantId: TenantId,
                        subscriptionId: SubscriptionId);
                // case ParamSet.SPWithCert:
                case ParamSet.ManagedIdentity:
                    return AuthRequestFactory.CreateManagedIdentity(
                        clientId: ClientId,
                        objectId: ObjectId,
                        resourceId: ResourceId,
                        tenantId: TenantId,
                        subscriptionId: SubscriptionId);
                case ParamSet.FederatedIdentity:
                    Debug.Assert(!string.IsNullOrEmpty(ClientId), "ClientId must not be null or empty");
                    Debug.Assert(!string.IsNullOrEmpty(FederatedToken), "FederatedToken must not be null or empty");
                    return AuthRequestFactory.CreateFederatedIdentity(
                        clientId: ClientId,
                        federatedToken: FederatedToken,
                        tenantId: TenantId,
                         subscriptionId: SubscriptionId);
                case ParamSet.ProfileReauth:
                    Debug.Assert(!string.IsNullOrEmpty(Profile), "Profile must not be null or empty");
                    var ProfileData = await ProfileService.GetProfileAsync(Profile);
                    if (null == ProfileData)
                        throw AzclipsErrorException.ProfileNotFound($"Profile '{Profile}' does not exist.");
                    if (ProfileData.AuthMethod == AuthMethod.Federated)
                    {
                        if (string.IsNullOrEmpty(FederatedToken))
                            throw AzclipsErrorException.InvalidInput(
                                $"Profile '{Profile}' requires a newly issued token in '{FormatParameterName(nameof(FederatedToken), AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType)}'.");
                        if (string.IsNullOrWhiteSpace(ProfileData.ClientId))
                            throw AzclipsErrorException.InvalidInput($"Profile '{Profile}' is missing its service principal client ID.");
                        return AuthRequestFactory.CreateFederatedIdentity(
                            ProfileData.ClientId, FederatedToken, ProfileData.TenantId, ProfileData.SubscriptionId);
                    }
                    return await AuthRequestFactory.Create(ProfileData, AzclipsServiceProvider.GetRequiredService<IServicePrincipalSecretStore>());
                default:
                    throw AzclipsErrorException.InvalidInput(
                        $"usage error: Unsupported parameter set '{parameterSet}'.");
            }
        }

        /// <summary>Validates and normalizes individual values before parameter-set resolution.</summary>
        private void ValidateParameterValues()
        {
            // Parameter set agnostic validation. TODO: leverage attributes for common validation where possible.
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
            ValidateNotWhitespace(ClientSecret, nameof(ClientSecret));

            if (OutProfile is not null)
            {
                ProfileNameValidator.ValidateForCreation(OutProfile);
            }
            SubscriptionIdValidator.Validate(SubscriptionId, clientParamNameFormatter(nameof(SubscriptionId)), allowEmpty: true);

            ValidateGuid(ClientId, nameof(ClientId), clientParamNameFormatter);
            ValidateGuid(ObjectId, nameof(ObjectId), clientParamNameFormatter);
        }

        private void ValidateParameterSet(string parameterSet)
        {
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            if (parameterSet == ParamSet.ManagedIdentity)
            {
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
        /// Resolves the tenant that owns <paramref name="subscriptionId"/>, returning an empty string
        /// when the signed-in tenant already owns it so the caller falls back to the token's tenant.
        /// Throws when the owning tenant genuinely cannot be determined (the ARM lookup failed), so the
        /// login fails before saving rather than persisting an unverified, possibly-wrong tenant.
        /// </summary>
        private async Task<string> ResolveSubscriptionTenantAsync(AuthResult authResult, string? subscriptionId)
        {
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientMetadata>().ClientType;
            string clientParamNameFormatter(string propertyName) => FormatParameterName(propertyName, clientType);

            try
            {
                var subscriptionService = AzclipsServiceProvider.GetRequiredService<ISubscriptionService>();
                var owningTenant = await subscriptionService.ResolveSubscriptionTenantAsync(authResult.AccessToken, subscriptionId);

                if (!string.IsNullOrEmpty(owningTenant)
                    && !string.Equals(owningTenant, authResult.TenantId, StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Verbose(
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
                Logger.Debug(
                    $"Could not resolve the owning tenant for subscription '{subscriptionId}': {ex.Message}");
                throw new InvalidOperationException(
                    $"Could not determine the tenant that owns subscription '{subscriptionId}': {ex.Message} "
                    + "The profile was not saved to avoid persisting an unverified tenant. "
                    + $"Retry, or specify the tenant explicitly with '{clientParamNameFormatter(nameof(TenantId))} <tenantId>'.",
                    ex);
            }
        }
    }


}
