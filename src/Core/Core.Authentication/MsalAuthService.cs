using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

public class MsalAuthService(ILoggerService TelemetryService) : IAuthService
{
    private const string ArmDefaultScope = "https://management.core.windows.net//.default";
    private AuthenticationInfo? _currentContext;

    public async Task<AuthenticationInfo> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await AcquireTokenInteractiveAsync(request.Scopes, request.TenantId, cancellationToken);
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
        // MOCKUP: Clear authentication context
        await Task.Run(() => _currentContext = null);
        return;
    }

    public AuthenticationInfo? GetCurrentContext()
    {
        // MOCKUP: Return current context or null
        return _currentContext;
    }

    private async Task<string> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, true);

        var result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
        LogTokenMetadata(result);
        return result.AccessToken;
    }

    // todo: what should be the client id here?
    private IPublicClientApplication BuildApp(string? tenantId) => PublicClientApplicationBuilder.Create("1950a258-227b-4e31-a9cf-717495945fc2")
                .WithDefaultRedirectUri()
                .WithTenantId(tenantId ?? "72f988bf-86f1-41af-91ab-2d7cd011db47") // todo: read from input/context
                .Build();

    private async Task RegisterTokenCacheAsync(IPublicClientApplication app, bool plaintext = false)
    {
        var builder =
            new StorageCreationPropertiesBuilder("MSAL.cache" + ".plaintext",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus"));
        if (!plaintext)
        {
            builder = builder
            .WithMacKeyChain(
                "Microsoft.Project.Cirrus", // Config.KeyChainServiceName,
                "MSAL.cache"); // Config.KeyChainAccountName)
        }
        else
        {
            builder = builder.WithUnprotectedFile();
        }
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
    /// <throws cref="MsalUiRequiredException">Thrown if silent acquisition fails and prompting is disabled.</throws>
    public async Task<string> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        var app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, true);

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
        TelemetryService.Log(@$"[Verbose] Token acquired silently:
Username: {result.Account.Username}
DurationTotalInMs: {result.AuthenticationResultMetadata.DurationTotalInMs}
TokenSource: {result.AuthenticationResultMetadata.TokenSource}
CacheRefreshReason: {result.AuthenticationResultMetadata.CacheRefreshReason}");
    }
}
