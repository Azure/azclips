// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

internal class MsalAuthService(
    ILoggingService Logger,
    ITelemetryService TelemetryService,
    IProfileService ProfileService,
    ClientMetadata ClientMetadata,
    ManagedIdentityAuthenticator ManagedIdentityAuthenticator,
    FederatedTokenAuthenticator FederatedTokenAuthenticator,
    ServicePrincipalSecretAuthenticator ServicePrincipalSecretAuthenticator,
    IServicePrincipalSecretStore ServicePrincipalSecretStore,
    MsalClientOptions Options) : IAuthService
{
    private const string ArmDefaultScope = AuthConstants.ArmDefaultScope;

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LoginAsync)}");
        var result = request.Credential switch
        {
            AuthCredential.ManagedIdentity => await AuthenticateWithManagedIdentityAsync(request, cancellationToken),
            AuthCredential.FederatedIdentity => await AuthenticateWithFederatedTokenAsync(request, cancellationToken),
            AuthCredential.DeviceCode => await AcquireTokenWithDeviceCodeAsync(request.Scopes, request.TenantId, cancellationToken),
            AuthCredential.ClientSecret => await ServicePrincipalSecretAuthenticator.AuthenticateAsync(request, cancellationToken),
            _ => await AcquireTokenInteractiveAsync(request.Scopes, request.TenantId, cancellationToken),
        };

        WarnIfEncryptionUnavailable();
        return result;
    }

    private async Task<AuthResult> AuthenticateWithManagedIdentityAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = TelemetryService.StartActivity(
            $"{nameof(MsalAuthService)}.{nameof(AuthenticateWithManagedIdentityAsync)}");
        try
        {
            var result = await ManagedIdentityAuthenticator.AuthenticateAsync(request, cancellationToken);
            SetAuthenticationSuccessTags(activity);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetAuthenticationFailureTags(activity, ex);
            throw;
        }
    }

    private async Task<AuthResult> AuthenticateWithFederatedTokenAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = TelemetryService.StartActivity(
            $"{nameof(MsalAuthService)}.{nameof(AuthenticateWithFederatedTokenAsync)}");
        try
        {
            var result = await FederatedTokenAuthenticator.AuthenticateAsync(request, cancellationToken);
            SetAuthenticationSuccessTags(activity);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetAuthenticationFailureTags(activity, ex);
            throw;
        }
    }

    /// <summary>Set when the OS credential store was unavailable and credentials went to a plaintext file.</summary>
    private void WarnIfEncryptionUnavailable()
    {
        if (!MsalTokenCacheFactory.EncryptionFallback)
        {
            return;
        }

        Logger.Warn(
            "Encryption is unavailable on this machine, so the token cache and service principal secrets " +
            "are stored in plaintext.");
    }

    public async Task LogoutAsync(string profileName, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LogoutAsync)}");

        var profile = await ProfileService.GetProfileAsync(profileName, cancellationToken);

        if (profile is null)
        {
            // Best-effort: the caller still has to unset the active-profile pointer, so a missing
            // record must not abort the sign-out.
            Logger.Warn($"[{nameof(MsalAuthService)}] Profile '{profileName}' was not found; no cached credentials could be identified to remove.");
            return;
        }

        // Service principal tokens live in the app cache, keyed by application rather than account, so
        // account removal cannot evict them.
        if (profile.AuthMethod is AuthMethod.Federated or AuthMethod.ServicePrincipalSecret)
        {
            await RemoveAppCacheAsync(profile, cancellationToken);
            return;
        }

        // Managed identity tokens are issued by the platform endpoint on demand and never cached here.
        if (profile.AuthMethod == AuthMethod.ManagedIdentity)
        {
            return;
        }

        await RemoveUserAccountAsync(profile, cancellationToken);
    }

    /// <summary>
    /// Removes one account from the user token cache. The cache is shared across profiles, so this
    /// matches on the profile's user principal name rather than clearing it.
    /// </summary>
    private async Task RemoveUserAccountAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        var userPrincipalName = profile.UserPrincipalName;

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            Logger.Verbose($"[{nameof(MsalAuthService)}] No user principal name provided; nothing to remove from the token cache.");
            return;
        }

        Logger.Verbose($"[{nameof(MsalAuthService)}] Removing the account of profile '{profile.Name}' from the MSAL token cache.");

        try
        {
            // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/clear-token-cache#desktop-command-line-and-mobile-applications
            var app = BuildApp(tenantId: null);

            await RegisterTokenCacheAsync(app);

            var accounts = (await app.GetAccountsAsync()).ToList();
            var matching = accounts
                .Where(a => string.Equals(a.Username, userPrincipalName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Logger.Debug($"[{nameof(MsalAuthService)}] Found {matching.Count} matching account(s) for profile '{profile.Name}' of {accounts.Count} in the token cache.");

            foreach (var account in matching)
            {
                Logger.Debug($"[{nameof(MsalAuthService)}] Removing a matched account of profile '{profile.Name}'.");
                try
                {
                    await app.RemoveAsync(account);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Removing one account is best-effort: a failure here must not abort removal of the
                    // remaining matching accounts, nor block the caller from signing out locally.
                    // Log only the exception type and HResult, never ex.Message: every ILoggingService
                    // level here is also recorded as an ActivityEvent tag, and event tags bypass the
                    // telemetry property allowlist (DefaultTelemetryPropertyFilterProcessor only filters
                    // top-level Activity tags), so the unbounded exception text could otherwise leak
                    // paths/PII into exported trace files.
                    Logger.Debug($"[{nameof(MsalAuthService)}] Failed to remove a matched account of profile '{profile.Name}' from the MSAL token cache: {ex.GetType().Name} (HResult 0x{ex.HResult:X8})");
                    Logger.Warn($"[{nameof(MsalAuthService)}] Non-fatal: token-cache cleanup failed during logout ({ex.GetType().Name}); local sign-out continues. Enable debug logging for details.");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Token-cache cleanup as a whole is best-effort: any step here (building the MSAL client,
            // registering the cache, or enumerating accounts) can fail from environmental issues (e.g. a
            // conflicting Microsoft.Identity.Client/broker assembly already loaded by another module in the
            // same host process, or a locked/corrupt cache file) and must not prevent the user from signing
            // out locally. Cancellation is allowed to propagate so a cancelled logout is not silently swallowed.
            // Log only the exception type and HResult, never ex.Message: every ILoggingService level here is
            // also recorded as an ActivityEvent tag, and event tags bypass the telemetry property allowlist,
            // so the unbounded exception text could otherwise leak paths/PII into exported trace files.
            Logger.Debug($"[{nameof(MsalAuthService)}] Token-cache cleanup failed during logout for profile '{profile.Name}': {ex.GetType().Name} (HResult 0x{ex.HResult:X8})");
            Logger.Warn($"[{nameof(MsalAuthService)}] Non-fatal: token-cache cleanup failed during logout ({ex.GetType().Name}); local sign-out continues. Enable debug logging for details.");
        }
    }

    /// <summary>
    /// Evicts cached client-credential tokens on logout. MSAL .NET offers no per-application removal
    /// from an app cache (<c>RemoveAsync</c> takes an account, which an app cache has none of), so the
    /// whole cache goes and every service principal loses its cached tokens. They recover on their own
    /// — their credentials are still stored. Only this profile's credential is removed.
    /// </summary>
    private async Task RemoveAppCacheAsync(Profile profile, CancellationToken cancellationToken = default)
    {
        await MsalTokenCacheFactory.RemoveAppCacheAsync(
            prefix: MsalTokenCacheFactory.AppCachePrefix,
            cacheDirectory: Options.CacheDirectory,
            logger: Logger);

        // Otherwise the tokens are gone but the credential that mints them is not, and the next
        // command signs straight back in.
        await ServicePrincipalSecretStore.RemoveAsync(
            profile.ClientId!, profile.TenantId, Options.CacheDirectory, Logger);

        Logger.Debug($"[{nameof(MsalAuthService)}] Cleared cached service principal tokens for profile '{profile.Name}'.");
    }

    private async Task<AuthResult> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenInteractiveAsync)}");
        try
        {
            IPublicClientApplication app = BuildApp(tenantId);
            await RegisterTokenCacheAsync(app);

            var result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope])
                .WithPrompt(Prompt.SelectAccount)
                .WithUseEmbeddedWebView(false)
                .ExecuteAsync(cancellationToken);
            SetAuthenticationSuccessTags(
                activity,
                result.AuthenticationResultMetadata.DurationTotalInMs);
            LogTokenMetadataDebug(result);
            return result.ToAuthResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetAuthenticationFailureTags(activity, ex);
            throw;
        }
    }

    private async Task<AuthResult> AcquireTokenWithDeviceCodeAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenWithDeviceCodeAsync)}");
        try
        {
            IPublicClientApplication app = BuildApp(tenantId);
            await RegisterTokenCacheAsync(app);

            var result = await app.AcquireTokenWithDeviceCode(scopes ?? [ArmDefaultScope], deviceCodeResult =>
            {
                //TODO: This output always needs to be shown to the user,
                //but we should consider a better way to do this output + stlying than just writing to the console here.
                Console.WriteLine(deviceCodeResult.Message);
                Console.WriteLine("Press Ctrl+C to cancel.");
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken);

            SetAuthenticationSuccessTags(
                activity,
                result.AuthenticationResultMetadata.DurationTotalInMs);
            LogTokenMetadataDebug(result);
            return result.ToAuthResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetAuthenticationFailureTags(activity, ex);
            throw;
        }
    }

    private async Task<AuthResult> AcquireTokenWithClientSecret(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var activity = TelemetryService.StartActivity(
            $"{nameof(MsalAuthService)}.{nameof(AcquireTokenWithClientSecret)}");
        try
        {
            var result = await ServicePrincipalSecretAuthenticator.AuthenticateAsync(request, cancellationToken);
            SetAuthenticationSuccessTags(activity);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetAuthenticationFailureTags(activity, ex);
            throw;
        }
    }

    // todo: reuse app instance but allow reinitialization when tenantId changes
    private IPublicClientApplication BuildApp(string? tenantId)
    {
        var builder = PublicClientApplicationBuilder.Create(ClientMetadata.FirstPartyApplicationId)
            .WithRedirectUri("http://localhost");

        // No tenant supplied: use the multi-tenant "organizations" authority
        builder = string.IsNullOrWhiteSpace(tenantId)
            ? builder.WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
            : builder.WithTenantId(tenantId);

        if (Options.HttpClientFactory is not null)
        {
            builder = builder.WithHttpClientFactory(Options.HttpClientFactory);
        }

        return builder.Build();
    }

    private async Task RegisterTokenCacheAsync(IPublicClientApplication app)
    {
        var resolution = await MsalTokenCacheFactory.CreateAsync(
            cacheDirectory: Options.CacheDirectory, logger: Logger);

        if (resolution.Helper is null)
        {
            Logger.Debug($"[{nameof(MsalAuthService)}] Token cache is held in memory for this process only.");
            return;
        }

        Logger.Debug($"[{nameof(MsalAuthService)}] Using MSAL token cache file: " +
            $"{MsalTokenCacheFactory.GetCacheFilePath(resolution.PlaintextUnsafe, cacheDirectory: Options.CacheDirectory)}");

        resolution.Helper.RegisterCache(app.UserTokenCache);
    }

    /// <summary>
    /// Acquires a token silently if available, otherwise optionally prompts the user interactively.
    /// </summary>
    /// <param name="promptOnError">Whether to prompt the user interactively if silent acquisition fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The acquired access token.</returns>
    /// <exception cref="MsalUiRequiredException">Thrown if silent acquisition fails and prompting is disabled.</exception>
    public async Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default, Profile? profile = null)
    {
        using var activity = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenSilentlyAsync)}");

        try
        {
            // profile will be set from the engine, fallback for other direct caller like TUI/Tooling
            // TODO: Consider making the profile mandatory
            profile ??= await ProfileService.GetActiveProfileAsync(cancellationToken);
            if (profile?.AuthMethod == AuthMethod.ManagedIdentity)
            {
                var authResult = await ManagedIdentityAuthenticator.AcquireTokenAsync(profile, scopes, cancellationToken);
                SetAuthenticationSuccessTags(activity);
                return authResult;
            }

            if (profile?.AuthMethod == AuthMethod.Federated)
            {
                var authResult = await FederatedTokenAuthenticator.AcquireTokenAsync(profile, scopes, cancellationToken);
                SetAuthenticationSuccessTags(activity);
                return authResult;
            }

            if (profile?.AuthMethod == AuthMethod.ServicePrincipalSecret)
            {
                if (string.IsNullOrWhiteSpace(profile.ClientId))
                    throw new ArgumentException("A client ID is required for service principal login.");

                var tenant = string.IsNullOrWhiteSpace(tenantId) ? profile.TenantId : tenantId;
                if (string.IsNullOrWhiteSpace(tenant))
                    throw new ArgumentException("A tenant ID is required for service principal login.");

                var secret = await ServicePrincipalSecretStore.GetClientSecretAsync(profile.ClientId, Options.CacheDirectory, Logger);
                if (string.IsNullOrEmpty(secret))
                    throw new InvalidOperationException("Client secret not found in the secret store.");

                var request = new LoginRequest(tenant, scopes, new AuthCredential.ClientSecret(profile.ClientId, secret));
                var authResult = await ServicePrincipalSecretAuthenticator.AcquireTokenAsync(request, cancellationToken);
                SetAuthenticationSuccessTags(activity);
                return authResult;
            }

            // otherwise use the usual MSAL flow
            var effectiveTenantId = string.IsNullOrWhiteSpace(tenantId) ? profile?.TenantId : tenantId;
            var app = BuildApp(effectiveTenantId);
            await RegisterTokenCacheAsync(app);

            var accounts = await app.GetAccountsAsync();
            var account = MsalAccountSelector.SelectAccount(accounts, profile);

            AuthenticationResult result;
            try
            {
                if (account == null)
                {
                    throw new MsalUiRequiredException(
                        "no_matching_account",
                        "No cached account matches the active profile. Sign in again to refresh credentials.");
                }

                result = await app.AcquireTokenSilent(scopes ?? [ArmDefaultScope], account).ExecuteAsync(cancellationToken);
            }
            catch (MsalUiRequiredException) when (promptOnError)
            {
                result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope])
                    .WithPrompt(Prompt.SelectAccount)
                    .WithUseEmbeddedWebView(false)
                    .ExecuteAsync(cancellationToken);
            }

            SetAuthenticationSuccessTags(
                activity,
                result.AuthenticationResultMetadata.DurationTotalInMs);
            LogTokenMetadataDebug(result);
            return result.ToAuthResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetAuthenticationFailureTags(activity, ex);
            throw;
        }
    }

    public async Task<bool> CanAcquireTokenSilentlyAsync(string? tenantId, Profile profile, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(CanAcquireTokenSilentlyAsync)}");
        try
        {
            await AcquireTokenSilentlyAsync(scopes: null, tenantId: tenantId, promptOnError: false, cancellationToken: cancellationToken, profile: profile);
            return true;
        }
        catch (MsalUiRequiredException ex)
        {
            Logger.Debug($"[{nameof(MsalAuthService)}] Silent token acquisition for tenant '{tenantId}' requires interaction: {ex.Message}");
            return false;
        }
    }

    private void LogTokenMetadataDebug(AuthenticationResult result)
    {
        Logger.Debug($"[{nameof(MsalAuthService)}] Token acquired silently: "
        + $"Username={result.Account.Username} | "
        + $"DurationTotalInMs={result.AuthenticationResultMetadata.DurationTotalInMs} | "
        + $"TokenSource={result.AuthenticationResultMetadata.TokenSource} | "
        + $"CacheRefreshReason={result.AuthenticationResultMetadata.CacheRefreshReason}");
    }

    internal static void SetAuthenticationSuccessTags(
        Activity? activity,
        long? durationTotalMs = null)
    {
        activity?.SetTag(TelemetryTags.AuthSuccess, true);
        if (durationTotalMs.HasValue)
            activity?.SetTag(TelemetryTags.AuthDurationTotalMs, durationTotalMs.Value);
    }

    internal static void SetAuthenticationFailureTags(Activity? activity, Exception exception)
    {
        activity?.SetTag(TelemetryTags.AuthSuccess, false);
        activity?.SetTag(TelemetryTags.ExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        if (exception is MsalServiceException { StatusCode: > 0 } serviceException)
            activity?.SetTag(TelemetryTags.AuthStatusCode, serviceException.StatusCode);
    }
}
