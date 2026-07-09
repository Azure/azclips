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
    ManagedIdentityAuthenticator ManagedIdentityAuthenticator) : IAuthService
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

        // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/clear-token-cache#desktop-command-line-and-mobile-applications
        var app = PublicClientApplicationBuilder.Create(ClientId)
            .Build();

        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var accounts = (await app.GetAccountsAsync()).ToList();
        var matching = accounts
            .Where(a => string.Equals(a.Username, userPrincipalName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Logger.Debug($"[{nameof(MsalAuthService)}] Found {matching.Count} matching account(s) for '{userPrincipalName}' of {accounts.Count} in the token cache.");

        foreach (var account in matching)
        {
            Logger.Debug($"[{nameof(MsalAuthService)}] Removing account: {account.Username}");
            await app.RemoveAsync(account);
        }
    }

    private async Task<AuthResult> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenInteractiveAsync)}");
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
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
    private IPublicClientApplication BuildApp(string? tenantId) => PublicClientApplicationBuilder.Create(ClientId)
                .WithRedirectUri("http://localhost")
                // todo: what should be the client id here?
                .WithTenantId(tenantId ?? "72f988bf-86f1-41af-91ab-2d7cd011db47") // todo: fallback to tenant id in the context
                .Build();

    private async Task RegisterTokenCacheAsync(IPublicClientApplication app, bool plaintextUnsafe = false)
    {
        var cacheDirectory = HomePaths.GetDefaultLocalAppDataRoot();
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
        var app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var accounts = await app.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        AuthenticationResult result;
        try
        {
            // If no account found, try with AcquireTokenSilent without account (uses login hint from cache)
            if (account == null)
            {
                result = await app.AcquireTokenSilent(scopes ?? [ArmDefaultScope], (IAccount?)null)
                    .WithForceRefresh(false)
                    .ExecuteAsync(cancellationToken);
            }
            else
            {
                result = await app.AcquireTokenSilent(scopes ?? [ArmDefaultScope], account).ExecuteAsync(cancellationToken);
            }
        }
        catch (MsalUiRequiredException) when (promptOnError)
        {
            result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
        }

        LogTokenMetadataDebug(result);
        return result.ToAuthResult();
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
