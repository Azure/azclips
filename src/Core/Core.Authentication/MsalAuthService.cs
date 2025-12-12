using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

public class MsalAuthService(ILoggerService Logger) : IAuthService // todo: testability, consider injecting MSAL API wrappers
{
    private const string ArmDefaultScope = "https://management.core.windows.net//.default";
    private AuthenticationInfo? _currentContext; // todo: proper context management (synchronization, multi-account, etc.)

    public async Task<AuthenticationInfo> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UseDeviceCodeFlow)
        {
            await AcquireTokenWithDeviceCodeAsync(request.Scopes, request.TenantId, cancellationToken);
        }
        else
        {
            await AcquireTokenInteractiveAsync(request.Scopes, request.TenantId, cancellationToken);
        }
        _currentContext = new AuthenticationInfo
        {
            SubscriptionId = request.SubscriptionId ?? "00000000-0000-0000-0000-000000000000",
            TenantId = request.TenantId ?? "11111111-1111-1111-1111-111111111111",
            IsAuthenticated = true
        };

        return _currentContext;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(() => _currentContext = null);
        return;
    }

    public AuthenticationInfo? GetCurrentContext()
    {
        return _currentContext;
    }

    private async Task<string> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app);

        var result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
        LogTokenMetadata(result);
        return result.AccessToken;
    }

    private async Task<string> AcquireTokenWithDeviceCodeAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app);

        var result = await app.AcquireTokenWithDeviceCode(scopes ?? [ArmDefaultScope], deviceCodeResult =>
        {
            Logger.Log(deviceCodeResult.Message);
            return Task.CompletedTask;
        }).ExecuteAsync(cancellationToken);

        LogTokenMetadata(result);
        return result.AccessToken;
    }

    // todo: reuse app instance but allow reinitialization when tenantId changes
    private IPublicClientApplication BuildApp(string? tenantId) => PublicClientApplicationBuilder.Create("1950a258-227b-4e31-a9cf-717495945fc2")
                .WithRedirectUri("http://localhost:50000")
                // todo: what should be the client id here?
                .WithTenantId(tenantId ?? "72f988bf-86f1-41af-91ab-2d7cd011db47") // todo: fallback to tenant id in the context
                .Build();

    private async Task RegisterTokenCacheAsync(IPublicClientApplication app, bool plaintextUnsafe = false)
    {
        var filename = "MSAL.cache";
        if (plaintextUnsafe)
        {
            filename += ".plaintext";
        }

        var builder =
            new StorageCreationPropertiesBuilder(filename,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus"));
        builder = plaintextUnsafe ?
            builder.WithUnprotectedFile() :
            builder.WithLinuxKeyring(
                "Microsoft.Project.Cirrus",
                "default", // todo: post MVP: configurable?
                "Tokens and credentials for Microsoft Project Cirrus",
                default, // todo: post MVP: is it valuable to set attributes?
                default)
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
    public async Task<string> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        var app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app);

        var accounts = await app.GetAccountsAsync();

        AuthenticationResult result;
        try
        {
            // todo: somehow this call always return a refreshed token even if the token in cache is still valid
            result = await app.AcquireTokenSilent(scopes ?? [ArmDefaultScope], accounts.FirstOrDefault()).ExecuteAsync(cancellationToken);
        }
        catch (MsalUiRequiredException) when (promptOnError)
        {
            result = await app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
        }

        LogTokenMetadata(result);
        return result.AccessToken;
    }

    private void LogTokenMetadata(AuthenticationResult result)
    {
        Logger.Log(@$"[Verbose] Token acquired silently:
Username: {result.Account.Username}
DurationTotalInMs: {result.AuthenticationResultMetadata.DurationTotalInMs}
TokenSource: {result.AuthenticationResultMetadata.TokenSource}
CacheRefreshReason: {result.AuthenticationResultMetadata.CacheRefreshReason}");
    }
}
