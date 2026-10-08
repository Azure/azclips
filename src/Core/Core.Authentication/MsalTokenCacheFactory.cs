// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client.Extensions.Msal;

namespace Core.Authentication;

/// <summary>
/// Cross-platform MSAL cache store, shared by the public and confidential clients so both agree on
/// location and protection. Protection is always attempted; when the OS credential store is unusable
/// the cache falls back to an unprotected file and login warns.
///
/// Layout follows Azure CLI's: DPAPI on Windows (.bin), libsecret or Keychain elsewhere, where the
/// .sig file is only a change signal. Keys stay distinct from az's — both stores look records up by
/// attributes, not file path, so reusing its schema would overwrite az's own cache.
/// </summary>
internal static class MsalTokenCacheFactory
{
    private const string ProductName = "Microsoft Azure CLI and PowerShell";

    private const string KeyringCollection = "default";

    // Separates the token caches from the secret store, as az's 'type' attribute does.
    public const string TokenCacheType = "Token cache";

    public const string SecretStoreType = "Secret store";

    // Payload held by DPAPI, which writes to the file itself.
    private const string EncryptedExtension = ".bin";

    // Payload held in the clear.
    private const string PlaintextExtension = ".json";

    // Payload held by libsecret or Keychain; the file only signals that it changed.
    private const string SignalExtension = ".sig";

    // Lower case to match Azure PowerShell's msal.cache and Azure CLI's msal_token_cache.
    public const string UserCachePrefix = "msal.cache.user";

    /// <summary>
    /// One app cache for every service principal. MSAL rejects a file shared between the app and user
    /// caches, but keys tokens inside by client id, so one file serves all applications.
    /// <para>
    /// The cost is that logout cannot be selective: app caches hold no accounts, so RemoveAsync has
    /// nothing to remove and emptying the file signs out every service principal. Acceptable because
    /// every entry is cheaply re-mintable.
    /// </para>
    /// </summary>
    public const string AppCachePrefix = "msal.cache.app";

    /// <summary>Shared by every cache file name, so a clear can find partitions it was never told about.</summary>
    private const string CachePrefixRoot = "msal.cache";

    // A clear removes every layout, not just this run's: which one holds the payload depends on
    // whether the credential store was usable, and that can change between runs.
    private static readonly string[] AllExtensions = [EncryptedExtension, PlaintextExtension, SignalExtension];

    // Clearing means emptying the payload, not deleting the file: on Linux and macOS the tokens live
    // in the credential store under a key that names no file. Azure CLI writes the same object.
    private static readonly byte[] EmptyCachePayload = "{}"u8.ToArray();

    /// <summary>
    /// Test seam, not configuration. Lets tests seed an unprotected file instead of writing to the
    /// developer's real keyring; they share one xUnit collection so they never see each other's value.
    /// </summary>
    internal static bool ForcePlaintextForTests { get; set; }

    // Stays set: the machine is still storing credentials unprotected, so every login has something
    // worth saying.
    private static bool _encryptionFallback;

    public static bool EncryptionFallback => _encryptionFallback;

    public static string GetCacheFileName(bool plaintextUnsafe, string prefix = UserCachePrefix)
    {
        if (plaintextUnsafe)
        {
            return prefix + PlaintextExtension;
        }

        return prefix + (OperatingSystem.IsWindows() ? EncryptedExtension : SignalExtension);
    }

    public static string GetCacheFilePath(bool plaintextUnsafe, string prefix = UserCachePrefix, string? cacheDirectory = null)
        => Path.Combine(cacheDirectory ?? HomePaths.GetDefaultLocalAppDataRoot(), GetCacheFileName(plaintextUnsafe, prefix));

    /// <summary>Resolves the cache and reports which layout ended up holding it.</summary>
    public static async Task<TokenCacheResolution> CreateAsync(
        string prefix = UserCachePrefix,
        string? cacheDirectory = null,
        ILoggingService? logger = null,
        string type = TokenCacheType)
        => await ResolveAsync(prefix, cacheDirectory, logger, type);

    /// <summary>
    /// Empties one partition through whichever accessor holds it, so the other layout is left alone
    /// rather than guessed at.
    /// </summary>
    internal static async Task RemoveAppCacheAsync(
        string prefix,
        string? cacheDirectory = null,
        ILoggingService? logger = null,
        string type = TokenCacheType)
    {
        try
        {
            var (helper, _) = await ResolveAsync(prefix, cacheDirectory, logger, type);

            if (helper is null)
            {
                return;
            }

            // Empty rather than delete: MSAL's only eviction API is RemoveAsync(IAccount), which app
            // caches hold no accounts for.
            helper.SaveUnencryptedTokenCache(EmptyCachePayload);
        }
        catch (Exception ex)
        {
            // Logout must still unset the active profile even if the cache cannot be emptied.
            logger?.Warn($"Could not clear cached tokens for '{prefix}': {ex.Message}");
        }
    }

    private static void TryDelete(string path, ILoggingService? logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.Warn($"Could not delete '{path}': {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the cache the way this machine can actually store it, so creating and clearing agree on
    /// which backing store holds the payload.
    /// </summary>
    private static async Task<TokenCacheResolution> ResolveAsync(
        string prefix,
        string? cacheDirectory,
        ILoggingService? logger,
        string type)
    {
        var directory = cacheDirectory ?? HomePaths.GetDefaultLocalAppDataRoot();

        // Back door for test to use the plaintext store since currently we have not implemented the isolation storage for OS secret store
        // and we do not want the test to ruin the developer OS secret store
        if (ForcePlaintextForTests)
        {
            return await PlaintextResolutionAsync(prefix, directory);
        }

        var helper = await MsalCacheHelper.CreateAsync(BuildProtectedProperties(prefix, directory, type));

        try
        {
            // Round-trips a dummy record through a throwaway accessor. Catches libsecret being
            // installed but unusable, such as when D-Bus cannot start over SSH.
            helper.VerifyPersistence();

            // An orphaned plaintext file from an earlier run is left alone: every token refresh comes
            // through here, and reclaiming it belongs to an explicit clear.
            return new TokenCacheResolution
            {
                Helper = helper,
                Protection = TokenCacheProtection.OsCredentialStore,
            };
        }
        catch (MsalCachePersistenceException ex)
        {
            // Covered by CredentialStoreLiveTests, which runs az against a dead D-Bus address — the
            // branch is unreachable in-process, since libsecret reads the native environment block.
            logger?.Debug($"[{nameof(MsalTokenCacheFactory)}] OS credential store unavailable for '{prefix}': {ex.Message}");

            // Unprotected file so the command can still run; login warns.
            _encryptionFallback = true;
            return await PlaintextResolutionAsync(prefix, directory);
        }
    }

    private static async Task<TokenCacheResolution> PlaintextResolutionAsync(string prefix, string directory) =>
        new()
        {
            Helper = await CreatePlaintextAsync(prefix, directory),
            Protection = TokenCacheProtection.PlaintextFile,
        };

    private static async Task<MsalCacheHelper> CreatePlaintextAsync(string prefix, string directory)
        => await MsalCacheHelper.CreateAsync(BuildPlaintextProperties(prefix, directory));

    private static StorageCreationProperties BuildPlaintextProperties(string prefix, string directory) =>
        new StorageCreationPropertiesBuilder(GetCacheFileName(plaintextUnsafe: true, prefix), directory)
            .WithUnprotectedFile()
            .Build();

    private static StorageCreationProperties BuildProtectedProperties(string prefix, string directory, string type)
    {
        // Records are keyed by attributes, not file path, so the prefix is what keeps the user and app
        // caches from overwriting each other.
        // TODO: identify whether CLI and PowerShell should share token cache
        return new StorageCreationPropertiesBuilder(GetCacheFileName(plaintextUnsafe: false, prefix), directory)
            .WithLinuxKeyring(
                ProductName,
                KeyringCollection,
                $"{ProductName} - {prefix}",
                new KeyValuePair<string, string>("type", type),
                new KeyValuePair<string, string>("cache", prefix))
            .WithMacKeyChain(ProductName, prefix)
            .Build();
    }
}
