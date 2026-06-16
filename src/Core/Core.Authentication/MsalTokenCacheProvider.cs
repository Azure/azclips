using Core.Abstractions;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

/// <summary>
/// Shared MSAL token cache registration with platform-protected storage.
/// Same code pattern as azure-powershell's MsalCacheHelperProvider:
///   - Windows: DPAPI encryption
///   - macOS: Keychain
///   - Linux: libsecret/GNOME Keyring → WithLinuxUnprotectedFile() fallback
///   - Thread-safe double-checked locking (singleton helper)
///   - Catches MsalCachePersistenceException specifically
///
/// Uses Azclips-specific names so the cache is NOT shared with PowerShell.
/// See: azure-powershell/src/Accounts/Authentication/Utilities/MsalCacheHelperProvider.cs
/// </summary>
public class MsalTokenCacheProvider
{
    private const string KeyChainServiceName = "Microsoft.Azure.Azclips.IdentityService";
    private const string KeyRingAttrName = "MsalClientID";
    private const string KeyRingCollection = "default";
    private const string TokenCacheName = "msal.user.cache";
    private const string CaeSuffix = ".cae";

    private static readonly string CacheDirectory = AuthContextService.BaseDirectory;

    private readonly ILoggingService _logger;
    private MsalCacheHelper? _cacheHelper;
    private readonly object _lock = new();

    public MsalTokenCacheProvider(ILoggingService logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers the platform-protected MSAL token cache on the given app's UserTokenCache.
    /// The cache helper is created once (thread-safe) and reused for subsequent registrations.
    /// </summary>
    public async Task RegisterCacheAsync(IPublicClientApplication app)
    {
        var helper = await GetOrCreateCacheHelperAsync();
        helper.RegisterCache(app.UserTokenCache);
    }

    /// <summary>
    /// Mirrors PowerShell's MsalCacheHelperProvider.GetCacheHelper() with double-checked locking.
    /// </summary>
    private Task<MsalCacheHelper> GetOrCreateCacheHelperAsync()
    {
        if (_cacheHelper is not null)
            return Task.FromResult(_cacheHelper);

        lock (_lock)
        {
            if (_cacheHelper is not null)
                return Task.FromResult(_cacheHelper);

            Directory.CreateDirectory(CacheDirectory);

            var tokenCacheName = TokenCacheName + CaeSuffix;
            var keyRingSchema = TokenCacheName; // PowerShell: GetTokenCacheNameWithoutSuffix(tokenCacheName)

            // Try protected storage first (same as PowerShell)
            try
            {
                var storageProperties = new StorageCreationPropertiesBuilder(tokenCacheName, CacheDirectory)
                    .WithMacKeyChain(KeyChainServiceName, tokenCacheName)
                    .WithLinuxKeyring(
                        keyRingSchema,
                        KeyRingCollection,
                        tokenCacheName,
                        new KeyValuePair<string, string>(KeyRingAttrName, KeyChainServiceName),
                        new KeyValuePair<string, string>(KeyChainServiceName, "1.0.0.0"))
                    .Build();

                var cacheHelper = MsalCacheHelper.CreateAsync(storageProperties)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                cacheHelper.VerifyPersistence();
                _cacheHelper = cacheHelper;

                _logger.Debug($"[{nameof(MsalTokenCacheProvider)}] Using protected MSAL token cache: {tokenCacheName}");
                return Task.FromResult(_cacheHelper);
            }
            catch (MsalCachePersistenceException ex)
            {
                _logger.Debug($"[{nameof(MsalTokenCacheProvider)}] Protected cache unavailable ({ex.Message}), falling back to unprotected.");
            }

            // Fallback: unprotected file (same as PowerShell)
            {
                var storageProperties = new StorageCreationPropertiesBuilder(tokenCacheName, CacheDirectory)
                    .WithMacKeyChain(KeyChainServiceName, "MSALCache")
                    .WithLinuxUnprotectedFile()
                    .Build();

                _cacheHelper = MsalCacheHelper.CreateAsync(storageProperties)
                    .ConfigureAwait(false).GetAwaiter().GetResult();

                _logger.Debug($"[{nameof(MsalTokenCacheProvider)}] Using unprotected MSAL token cache: {tokenCacheName}");
                return Task.FromResult(_cacheHelper);
            }
        }
    }
}
