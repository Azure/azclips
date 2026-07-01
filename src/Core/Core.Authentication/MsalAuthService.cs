// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

public class MsalAuthService(ILoggingService Logger, ITelemetryService TelemetryService) : IAuthService // todo: testability, consider injecting MSAL API wrappers
{
    private const string ArmDefaultScope = "https://management.core.windows.net//.default";
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2"; // todo: 1P or 3P?

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LoginAsync)}");
        if (request.UseDeviceCodeFlow)
        {
            return await AcquireTokenWithDeviceCodeAsync(request.Scopes, request.TenantId, cancellationToken);
        }
        else
        {
            return await AcquireTokenInteractiveAsync(request.Scopes, request.TenantId, cancellationToken);
        }
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
            Logger.Log(deviceCodeResult.Message);
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
        var cacheDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
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
                "Microsoft.Project.Cirrus",
                "default", // todo: post MVP: configurable?
                "Tokens and credentials for Microsoft Project Cirrus",
                new System.Collections.Generic.KeyValuePair<string, string>("application", "Microsoft.Project.Cirrus"),
                new System.Collections.Generic.KeyValuePair<string, string>("service", "default"))
            .WithMacKeyChain(
                "Microsoft Project Cirrus",
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
    public async Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenSilentlyAsync)}");

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
