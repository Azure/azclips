// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

public class MsalAuthService(
    ILoggingService Logger,
    ITelemetryService TelemetryService,
    IAuthContextService AuthContextService,
    MsalTokenCacheProvider TokenCacheProvider,
    ManagedIdentityAuthenticator ManagedIdentityAuthenticator) : IAuthService
{
    private const string ArmDefaultScope = AuthConstants.ArmDefaultScope;
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2"; // todo: 1P or 3P?

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LoginAsync)}");
        return request.Mode switch
        {
            LoginMode.ManagedIdentity => await ManagedIdentityAuthenticator.AuthenticateAsync(request, cancellationToken),
            LoginMode.DeviceCode => await AcquireTokenWithDeviceCodeAsync(request.Scopes, request.TenantId, cancellationToken),
            _ => await AcquireTokenInteractiveAsync(request.Scopes, request.TenantId, cancellationToken),
        };
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(LogoutAsync)}");
        Logger.Verbose($"[{nameof(MsalAuthService)}] Clearing MSAL token cache.");

        // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/clear-token-cache#desktop-command-line-and-mobile-applications
        var app = PublicClientApplicationBuilder.Create(ClientId)
            .Build();

        await TokenCacheProvider.RegisterCacheAsync(app);

        var accounts = (await app.GetAccountsAsync()).ToList();
        Logger.Debug($"[{nameof(MsalAuthService)}] Found {accounts.Count} accounts in the token cache.");

        while (accounts.Any())
        {
            Logger.Debug($"[{nameof(MsalAuthService)}] Removing account: {accounts.First().Username}");
            await app.RemoveAsync(accounts.First());
            accounts = (await app.GetAccountsAsync()).ToList();
        }

        await Task.CompletedTask;
    }

    private async Task<AuthResult> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenInteractiveAsync)}");
        IPublicClientApplication app = BuildApp(tenantId);
        await TokenCacheProvider.RegisterCacheAsync(app);

        var result = await TokenRetryHelper.ExecuteWithRetryAsync(
            () => app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken),
            Logger);
        LogTokenMetadataDebug(result);
        return result.ToAuthResult();
    }

    private async Task<AuthResult> AcquireTokenWithDeviceCodeAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenWithDeviceCodeAsync)}");
        IPublicClientApplication app = BuildApp(tenantId);
        await TokenCacheProvider.RegisterCacheAsync(app);

        var result = await TokenRetryHelper.ExecuteWithRetryAsync(
            () => app.AcquireTokenWithDeviceCode(scopes ?? [ArmDefaultScope], deviceCodeResult =>
            {
                Logger.Log(deviceCodeResult.Message);
                return Task.CompletedTask;
            }).ExecuteAsync(cancellationToken),
            Logger);

        LogTokenMetadataDebug(result);
        return result.ToAuthResult();
    }

    // todo: reuse app instance but allow reinitialization when tenantId changes
    private IPublicClientApplication BuildApp(string? tenantId) => PublicClientApplicationBuilder.Create(ClientId)
                .WithRedirectUri("http://localhost")
                // todo: what should be the client id here?
                .WithTenantId(tenantId ?? "72f988bf-86f1-41af-91ab-2d7cd011db47") // todo: fallback to tenant id in the context
                .Build();

    /// <summary>
    /// Acquires a token silently if available, otherwise optionally prompts the user interactively.
    /// Routes to the appropriate authenticator based on the persisted login mode.
    /// </summary>
    public async Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenSilentlyAsync)}");

        // Check persisted auth context to determine login mode
        var context = await AuthContextService.GetCurrentContextAsync(cancellationToken);
        if (context?.Mode == LoginMode.ManagedIdentity)
        {
            return await ManagedIdentityAuthenticator.AcquireTokenAsync(context, scopes, cancellationToken);
        }

        // Default: MSAL interactive/silent flow
        var app = BuildApp(tenantId ?? context?.TenantId);
        await TokenCacheProvider.RegisterCacheAsync(app);

        var accounts = await app.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        AuthenticationResult result;
        try
        {
            result = await TokenRetryHelper.ExecuteWithRetryAsync(
                (Func<Task<AuthenticationResult>>)(async () =>
                {
                    if (account == null)
                    {
                        return await app.AcquireTokenSilent(scopes ?? [ArmDefaultScope], (IAccount?)null)
                            .WithForceRefresh(false)
                            .ExecuteAsync(cancellationToken);
                    }
                    else
                    {
                        return await app.AcquireTokenSilent(scopes ?? [ArmDefaultScope], account).ExecuteAsync(cancellationToken);
                    }
                }),
                Logger);
        }
        catch (MsalUiRequiredException) when (promptOnError)
        {
            result = await TokenRetryHelper.ExecuteWithRetryAsync(
                () => app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken),
                Logger);
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
