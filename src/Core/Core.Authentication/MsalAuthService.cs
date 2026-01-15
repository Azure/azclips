using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Text.Json;

namespace Core.Authentication;

public class MsalAuthService(ILoggingService Logger, ITelemetryService TelemetryService) : IAuthService // todo: testability, consider injecting MSAL API wrappers
{
    private const string ArmDefaultScope = "https://management.core.windows.net//.default";
    private static readonly string ContextDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".cirrus");
    private static readonly string ContextFilePath = Path.Combine(ContextDirectory, "context.json");
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

        await SaveContextAsync(_currentContext);
        return _currentContext;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentContext = null;
        if (File.Exists(ContextFilePath))
        {
            File.Delete(ContextFilePath);
        }
        await Task.CompletedTask;
    }

    public AuthenticationInfo? GetCurrentContext()
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(MsalAuthService)}.{nameof(GetCurrentContext)}");
        if (_currentContext == null)
        {
            _currentContext = LoadContextAsync().GetAwaiter().GetResult();
        }
        return _currentContext;
    }

    private async Task SaveContextAsync(AuthenticationInfo context)
    {
        try
        {
            Directory.CreateDirectory(ContextDirectory);
            var json = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ContextFilePath, json);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to save authentication context: {ex.Message}");
        }
    }

    private async Task<AuthenticationInfo?> LoadContextAsync()
    {
        try
        {
            if (File.Exists(ContextFilePath))
            {
                var json = await File.ReadAllTextAsync(ContextFilePath);
                return JsonSerializer.Deserialize<AuthenticationInfo>(json);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load authentication context: {ex.Message}");
        }
        return null;
    }

    private async Task<string> AcquireTokenInteractiveAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var result = await app.AcquireTokenInteractive(scopes ?? [ArmDefaultScope]).WithUseEmbeddedWebView(false).ExecuteAsync(cancellationToken);
        LogTokenMetadataDebug(result);
        return result.AccessToken;
    }

    private async Task<string> AcquireTokenWithDeviceCodeAsync(string[]? scopes, string? tenantId, CancellationToken cancellationToken = default)
    {
        IPublicClientApplication app = BuildApp(tenantId);
        await RegisterTokenCacheAsync(app, plaintextUnsafe: true);

        var result = await app.AcquireTokenWithDeviceCode(scopes ?? [ArmDefaultScope], deviceCodeResult =>
        {
            Logger.Log(deviceCodeResult.Message);
            return Task.CompletedTask;
        }).ExecuteAsync(cancellationToken);

        LogTokenMetadataDebug(result);
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
    public async Task<string> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
    {
        using var _ = TelemetryService.ActivitySource?.StartActivity($"{nameof(MsalAuthService)}.{nameof(AcquireTokenSilentlyAsync)}");

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
        return result.AccessToken;
    }

    private void LogTokenMetadataDebug(AuthenticationResult result)
    {
        Logger.Debug(@$"Token acquired silently:
Username: {result.Account.Username}
DurationTotalInMs: {result.AuthenticationResultMetadata.DurationTotalInMs}
TokenSource: {result.AuthenticationResultMetadata.TokenSource}
CacheRefreshReason: {result.AuthenticationResultMetadata.CacheRefreshReason}");
    }
}
