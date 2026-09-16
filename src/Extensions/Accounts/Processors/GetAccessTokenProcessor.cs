// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security;
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
                ?? throw AzclipsErrorException.UsageError(
                    "No signed-in context is available. Run 'login' (or set an active profile) before retrieving a token.");

            string? explicitTenant = string.IsNullOrWhiteSpace(Tenant) ? null : Tenant.Trim();

            ValidateTenantCompatibility(profile, explicitTenant);

            string[] scopes = TokenScopeResolver.Resolve(Resource, ResourceType, Scope);

            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            string targetTenant = explicitTenant ?? profile.TenantId;
            AuthResult authResult;
            try
            {
                authResult = await authService.AcquireTokenSilentlyAsync(
                    scopes,
                    targetTenant,
                    promptOnError: false,
                    cancellationToken: Token,
                    profile: profile);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Acquisition is silent (promptOnError: false), so translate a raw MSAL failure into
                // actionable, surface-aware sign-in guidance.
                var errorClientType = AzclipsServiceProvider.GetRequiredService<ClientType>();
                string signInCommand = errorClientType == ClientType.PowerShell ? "Connect-AzAccount" : "az login";
                string tenantFlag = errorClientType == ClientType.PowerShell ? "-TenantId" : "--tenant-id";
                throw new InvalidOperationException(
                    $"Could not acquire an access token for tenant '{targetTenant}'. {ex.Message} "
                    + "You may not be signed in to that tenant, or it requires additional sign-in "
                    + "(for example, multifactor authentication or a conditional access policy). "
                    + $"Run '{signInCommand} {tenantFlag} {targetTenant}' to authenticate, then retry.",
                    ex);
            }

            // get-access-token operates in the active profile's context, so the reported subscription
            // always comes from that profile. Unlike classic `az` (which caches every subscription at
            // login and can resolve an arbitrary --subscription to its owning tenant offline), AzCLIPS
            // keeps only the single active profile, so there is no --subscription input to honor here;
            // switch subscription with login/profile set, which resolves the owning tenant correctly.
            string? subscription = string.IsNullOrEmpty(profile.SubscriptionId) ? null : profile.SubscriptionId;

            string? userId = !string.IsNullOrEmpty(profile.UserPrincipalName)
                ? profile.UserPrincipalName
                : authResult.UserPrincipalName;

            await _writeOutput(RenderOutput(authResult, subscription, userId));

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
            if (!tenantBoundToAccount)
            {
                return;
            }

            // A managed identity's tenant is fixed by the account. If the account's tenant is
            // unknown (managed identity profiles can carry an empty tenant), an explicit tenant
            // cannot be honored - MSAL would ignore it and return a token for the account's own
            // tenant - so reject it rather than silently serve a different tenant than requested.
            if (string.IsNullOrEmpty(profile.TenantId))
            {
                throw AzclipsErrorException.UsageError(
                    $"Tenant '{explicitTenant}' cannot be honored for the current managed identity account, whose tenant is fixed by the account and cannot be overridden. Retry without specifying a tenant.");
            }

            if (!string.Equals(explicitTenant, profile.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw AzclipsErrorException.UsageError(
                    $"Tenant '{explicitTenant}' is not valid for the current account. A managed identity account can only acquire tokens for its own tenant ('{profile.TenantId}').");
            }
        }

        /// <summary>
        /// Renders the output shape for the current front end:
        /// <list type="bullet">
        /// <item>CLI: an <see cref="AccessTokenResult"/> whose token is redacted unless <c>--plaintext</c> was passed.</item>
        /// <item>PowerShell without <c>-AsPlainText</c>: a <see cref="SecureAccessTokenResult"/> whose token is a <see cref="SecureString"/>.</item>
        /// <item>PowerShell with <c>-AsPlainText</c>: an <see cref="AccessTokenResult"/> with the raw token.</item>
        /// </list>
        /// </summary>
        private object RenderOutput(AuthResult authResult, string? subscription, string? userId)
        {
            long expiresOnUnixTime = authResult.ExpiresOn.ToUnixTimeSeconds();
            var clientType = AzclipsServiceProvider.GetRequiredService<ClientType>();

            if (clientType == ClientType.PowerShell && !Plaintext)
            {
                return new SecureAccessTokenResult
                {
                    Token = ToSecureString(authResult.AccessToken),
                    ExpiresOn = authResult.ExpiresOn,
                    ExpiresOnUnixTime = expiresOnUnixTime,
                    TenantId = authResult.TenantId,
                    SubscriptionId = subscription,
                    UserId = userId,
                };
            }

            bool redact = clientType == ClientType.CLI && !Plaintext;
            return new AccessTokenResult
            {
                Token = redact ? AccessTokenResult.RedactedPlaceholder : authResult.AccessToken,
                ExpiresOn = authResult.ExpiresOn,
                ExpiresOnUnixTime = expiresOnUnixTime,
                TenantId = authResult.TenantId,
                SubscriptionId = subscription,
                UserId = userId,
            };
        }

        private static SecureString ToSecureString(string value)
        {
            var secure = new SecureString();
            foreach (char c in value)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }
    }
}
