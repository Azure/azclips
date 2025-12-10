using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

public class DefaultAuthService(ILoggerService TelemetryService) : IAuthService
{
    private const string ArmDefaultScope = "https://management.core.windows.net//.default";

    public async Task<string> AcquireTokenInteractiveAsync(CancellationToken cancellationToken = default)
    {
        string[] scopes = [ArmDefaultScope];
        IPublicClientApplication app = BuildApp();
        await RegisterTokenCacheAsync(app, true);

        var result = await app.AcquireTokenInteractive(scopes).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
        LogTokenMetadata(result);
        return result.AccessToken;
    }

    // todo: what should be the client id here?
    private static IPublicClientApplication BuildApp() => PublicClientApplicationBuilder.Create("1950a258-227b-4e31-a9cf-717495945fc2")
                .WithDefaultRedirectUri()
                .WithTenantId("72f988bf-86f1-41af-91ab-2d7cd011db47") // todo: read from input/context
                .Build();

    private async static Task RegisterTokenCacheAsync(IPublicClientApplication app, bool plaintext = false)
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
    public async Task<string> AcquireTokenSilentlyAsync(bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        string[] scopes = [ArmDefaultScope];

        var app = BuildApp();
        await RegisterTokenCacheAsync(app, true);

        var accounts = await app.GetAccountsAsync();

        AuthenticationResult result;
        try
        {
            // todo: somehow this call always return a refreshed token even if the token in cache is still valid
            result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync(cancellationToken);
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
