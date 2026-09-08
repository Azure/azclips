// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Abstractions.Runtime;
using Core.DependencyInjection;

namespace Microsoft.Azure.Azclips.Extensions.Accounts.Processors
{
    /// <summary>
    /// Retrieves an access token for a resource, well-known resource type, or explicit scope list
    /// from the current signed-in context (<c>az account get-access-token</c> / <c>Get-AzAccessToken</c>).
    /// </summary>
    /// <remarks>
    /// Output is secure by default: the raw token is only rendered when the caller opts into plaintext
    /// (CLI <c>--plaintext</c> flag; PowerShell <c>-AsPlainText</c> switch).
    /// </remarks>
    public class GetAccessTokenProcessor : IProcessor
    {
        private IEventPublisher _publisher = null!;
        private OutputStreamDelegate _writeOutput = null!;
        private Profile? _profile;

        [ArgumentInfo(
        Required = false,
        Description = @"Resource URL (v1.0) to acquire the token for, e.g. https://vault.azure.net. Mutually exclusive with --resource-type and --scope.",
        SerializedName = @"Resource")]
        public string? Resource { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Well-known resource type shortcut (e.g. arm, ms-graph, key-vault). Mutually exclusive with --resource and --scope.",
        SerializedName = @"ResourceTypeName")]
        public string? ResourceType { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"One or more Microsoft Entra v2.0 scopes. Mutually exclusive with --resource and --resource-type.",
        SerializedName = @"Scope")]
        public string[]? Scope { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Tenant ID to acquire the token for. Defaults to the current profile's tenant.",
        SerializedName = @"TenantId")]
        public string? Tenant { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Subscription ID associated with the request. Defaults to the current profile's subscription.",
        SerializedName = @"SubscriptionId")]
        public string? Subscription { get; set; }

        [ArgumentInfo(
        Required = false,
        Description = @"Emit the raw access token as plain text. By default the token is redacted in output.",
        SerializedName = @"Plaintext")]
        public bool Plaintext { get; set; }

        public CancellationToken Token => CancellationToken.None;

        public Action Cancel => throw new NotImplementedException();

        public IProcessor Init(ProcessorContext context)
        {
            _publisher = context.Publisher;
            _writeOutput = context.OutputHandler.WriteOutput;
            _profile = context.Profile;
            return this;
        }

        public async Task ProcessAsync()
        {
            await _publisher.Publish(Events.ProcessorProcessAsyncStart);

            Profile profile = _profile
                ?? throw new InvalidOperationException(
                    "No signed-in context is available. Run 'login' (or set an active profile) before retrieving a token.");

            string? explicitTenant = string.IsNullOrWhiteSpace(Tenant) ? null : Tenant.Trim();

            ValidateTenantCompatibility(profile, explicitTenant);

            string[] scopes = TokenScopeResolver.Resolve(Resource, ResourceType, Scope);

            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            AuthResult authResult = await authService.AcquireTokenSilentlyAsync(
                scopes,
                explicitTenant ?? profile.TenantId,
                promptOnError: false,
                cancellationToken: Token,
                profile: profile);

            string? subscription = !string.IsNullOrWhiteSpace(Subscription)
                ? Subscription.Trim()
                : profile.SubscriptionId;

            var result = new AccessTokenResult
            {
                Token = authResult.AccessToken,
                ExpiresOn = authResult.ExpiresOn,
                TenantId = authResult.TenantId,
                SubscriptionId = string.IsNullOrEmpty(subscription) ? null : subscription,
            };

            await _writeOutput(RenderOutput(result));

            await _publisher.Publish(Events.ProcessorProcessAsyncEnd);
        }

        /// <summary>
        /// Rejects an explicit tenant that is incompatible with a managed identity account. For
        /// managed identity the tenant is fixed by the account, so an explicit tenant that differs
        /// from the account's tenant cannot be honored.
        /// </summary>
        /// <remarks>
        /// A Cloud Shell session currently authenticates via managed identity and is therefore already
        /// covered by this check. If a dedicated Cloud Shell authentication method is added in the
        /// future, extend this validation to cover it.
        /// </remarks>
        private static void ValidateTenantCompatibility(Profile profile, string? explicitTenant)
        {
            if (explicitTenant is null)
            {
                return;
            }

            bool tenantBoundToAccount = profile.AuthMethod == AuthMethod.ManagedIdentity;
            if (tenantBoundToAccount &&
                !string.IsNullOrEmpty(profile.TenantId) &&
                !string.Equals(explicitTenant, profile.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Tenant '{explicitTenant}' is not valid for the current account. A managed identity account can only acquire tokens for its own tenant ('{profile.TenantId}').");
            }
        }

        /// <summary>
        /// Renders the result, redacting the token unless plaintext output was requested via the
        /// <c>--plaintext</c> flag (CLI) or the <c>-AsPlainText</c> switch (PowerShell). When redacted,
        /// the placeholder names the reveal option for the current front end.
        /// </summary>
        private AccessTokenResult RenderOutput(AccessTokenResult result)
        {
            if (Plaintext)
            {
                return result;
            }

            var clientType = AzclipsServiceProvider.GetRequiredService<ClientType>();
            string placeholder = clientType == ClientType.PowerShell
                ? AccessTokenResult.RedactedPlaceholderPowerShell
                : AccessTokenResult.RedactedPlaceholderCli;

            return result with { Token = placeholder };
        }
    }
}
