// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

public class MsalAuthService(
    ILoggingService Logger,
    ITelemetryService TelemetryService,
    IProfileService ProfileService,
    ManagedIdentityAuthenticator ManagedIdentityAuthenticator,
    MsalClientOptions Options) : IAuthService
{
    private const string ArmDefaultScope = AuthConstants.ArmDefaultScope;
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2"; // todo: 1P or 3P?

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LoginAsync)}");
        return request.AuthMode switch
        {
            AuthMethod.ManagedIdentity => await ManagedIdentityAuthenticator.AuthenticateAsync(request, cancellationToken),
            AuthMethod.DeviceCode => await AcquireTokenWithDeviceCodeAsync(request.Scopes, request.TenantId, cancellationToken),
            _ => await AcquireTokenInteractiveAsync(request.Scopes, request.TenantId, cancellationToken),
        };
    }

    public async Task LogoutAsync(string? userPrincipalName, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LogoutAsync)}");

        if (string.IsNullOrWhiteSpace(userPrincipalName))
        {
            Logger.Verbose($"[{nameof(MsalAuthService)}] No user principal name provided; nothing to remove from the token cache.");
            return;
        }

        Logger.Verbose($"[{nameof(MsalAuthService)}] Removing account '{userPrincipalName}' from the MSAL token cache.");

        try
        {
            // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/clear-token-cache#desktop-command-line-and-mobile-applications
            var app = BuildApp(tenantId: null);

            await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

            var accounts = (await app.GetAccountsAsync()).ToList();
            var matching = accounts
                .Where(a => string.Equals(a.Username, userPrincipalName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            Logger.Debug($"[{nameof(MsalAuthService)}] Found {matching.Count} matching account(s) for '{userPrincipalName}' of {accounts.Count} in the token cache.");

            foreach (var account in matching)
            {
                Logger.Debug($"[{nameof(MsalAuthService)}] Removing account: {account.Username}");
                try
                {
                    await app.RemoveAsync(account);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Removing one account is best-effort: a failure here must not abort removal of the
                    // remaining matching accounts, nor block the caller from signing out locally.
                    // Keep the account identifier and full exception text at Debug only (Warn is
                    // console-routed by default) to avoid surfacing the account UPN or other sensitive
                    // details from the exception message as PII on a non-fatal cleanup error.
                    Logger.Debug($"[{nameof(MsalAuthService)}] Failed to remove account '{account.Username}' from the MSAL token cache: {ex.GetType().Name}: {ex.Message}");
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
            // Keep the account identifier and full exception text at Debug only (Warn is console-routed
            // by default) to avoid surfacing the account UPN or other sensitive details from the
            // exception message as PII on a non-fatal cleanup error.
            Logger.Debug($"[{nameof(MsalAuthService)}] Token-cache cleanup failed during logout for account '{userPrincipalName}': {ex.GetType().Name}: {ex.Message}");
            Logger.Warn($"[{nameof(MsalAuthService)}] Non-fatal: token-cache cleanup failed during logout ({ex.GetType().Name}); local sign-out continues. Enable debug logging for details.");
        }
    }

    private async Task<AuthResult> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenInteractiveAsync)}");
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope])
            .WithPrompt(Prompt.SelectAccount)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync(cancellationToken);
        LogTokenMetadataDebug(result);
        return result.ToAuthResult();
    }

    private async Task<AuthResult> AcquireTokenWithDeviceCodeAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenWithDeviceCodeAsync)}");
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var result = await app.AcquireTokenWithDeviceCode(scopes ?? [ArmDefaultScope], deviceCodeResult =>
        {
            //TODO: This output always needs to be shown to the user,
            //but we should consider a better way to do this output + stlying than just writing to the console here.
            Console.WriteLine(deviceCodeResult.Message);
            Console.WriteLine("Press Ctrl+C to cancel.");
            return Task.CompletedTask;
        }).ExecuteAsync(cancellationToken);

        LogTokenMetadataDebug(result);
        return result.ToAuthResult();
    }

    // todo: reuse app instance but allow reinitialization when tenantId changes
    private IPublicClientApplication BuildApp(string? tenantId)
    {
        var builder = PublicClientApplicationBuilder.Create(ClientId)
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

    private async Task RegisterTokenCacheAsync(IPublicClientApplication app, bool plaintextUnsafe = false)
    {
        var cacheDirectory = Options.CacheDirectory;
        var filename = "MSAL.cache";
        if (plaintextUnsafe)
        {
            filename += ".plaintext";
        }
        Logger.Debug($"[{nameof(MsalAuthService)}] Using MSAL token cache file: {Path.Combine(cacheDirectory, filename)}");

        var builder = new StorageCreationPropertiesBuilder(filename, cacheDirectory);
        builder = plaintextUnsafe ?
            builder.WithUnprotectedFile() :
            builder.WithLinuxKeyring(
                "Microsoft.Azclips",
                "default", // todo: post MVP: configurable?
                "Tokens and credentials for Azclips",
                new System.Collections.Generic.KeyValuePair<string, string>("application", "Microsoft.Azclips"),
                new System.Collections.Generic.KeyValuePair<string, string>("service", "default"))
            .WithMacKeyChain(
                "Microsoft Azclips",
                "default"); // todo: post MVP: does it make sense to split the cache by account?

        var storageProperties = builder.Build();

        // This hooks up the cross-platform cache into MSAL
        var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
        cacheHelper.RegisterCache(app.UserTokenCache);
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
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenSilentlyAsync)}");

        // profile will be set from the engine, fallback for other direct caller like TUI/Tooling
        // TODO: Consider making the profile mandatory
        profile ??= await ProfileService.GetActiveProfileAsync(cancellationToken);
        if (profile?.AuthMethod == AuthMethod.ManagedIdentity)
        {
            return await ManagedIdentityAuthenticator.AcquireTokenAsync(profile, scopes, cancellationToken);
        }

        // otherwise use the usual MSAL flow
        var effectiveTenantId = string.IsNullOrWhiteSpace(tenantId) ? profile?.TenantId : tenantId;
        var app = BuildApp(effectiveTenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

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

        LogTokenMetadataDebug(result);
        return result.ToAuthResult();
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
}
