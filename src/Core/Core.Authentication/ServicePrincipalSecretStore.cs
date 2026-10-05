// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Abstractions;

namespace Core.Authentication;

/// <summary>
/// What a service principal signs in with, kept so a later command need not ask for it again. Same
/// shape as Azure CLI's <c>service_principal_entries</c>: one entry per application.
/// <para>
/// Stored alongside the token cache through the same tiers, told apart by the <c>type</c> attribute.
/// Separating them is what lets a logout empty the cache without discarding the credential that would
/// mint the next token.
/// </para>
/// </summary>
internal static class ServicePrincipalSecretStore
{
    /// <summary>
    /// Deliberately not named <c>msal.cache.*</c>: this is not a cache and must not be swept by the
    /// enumeration that finds cache partitions.
    /// </summary>
    public const string Prefix = "service.principal.entries";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The store is a single blob, so concurrent saves would drop each other's entries.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>One application's credential, keyed by the pair that identifies it to Entra.</summary>
    public sealed record Entry
    {
        [JsonPropertyName("client_id")]
        public string ClientId { get; init; } = default!;

        [JsonPropertyName("tenant")]
        public string TenantId { get; init; } = default!;

        /// <summary>The federated (OIDC) assertion. Named as Azure CLI names it.</summary>
        [JsonPropertyName("client_assertion")]
        public string? ClientAssertion { get; init; }
    }

    /// <summary>Records the credential an application just signed in with, replacing any earlier one.</summary>
    public static async Task SaveAssertionAsync(
        string clientId,
        string tenantId,
        string assertion,
        string? cacheDirectory = null,
        ILoggingService? logger = null)
    {
        await Gate.WaitAsync();
        try
        {
            var entries = await LoadAsync(cacheDirectory, logger);
            entries.RemoveAll(e => Matches(e, clientId, tenantId));
            entries.Add(new Entry { ClientId = clientId, TenantId = tenantId, ClientAssertion = assertion });
            await SaveAsync(entries, cacheDirectory, logger);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Null when nothing was kept; the caller has the context to report that better.</summary>
    public static async Task<string?> TryGetAssertionAsync(
        string clientId,
        string tenantId,
        string? cacheDirectory = null,
        ILoggingService? logger = null)
    {
        var entries = await LoadAsync(cacheDirectory, logger);
        return entries.FirstOrDefault(e => Matches(e, clientId, tenantId))?.ClientAssertion;
    }

    /// <summary>Forgets one application's credential, so a logout cannot sign straight back in.</summary>
    public static async Task RemoveAsync(
        string clientId,
        string tenantId,
        string? cacheDirectory = null,
        ILoggingService? logger = null)
    {
        await Gate.WaitAsync();
        try
        {
            var entries = await LoadAsync(cacheDirectory, logger);
            if (entries.RemoveAll(e => Matches(e, clientId, tenantId)) == 0)
            {
                return;
            }

            await SaveAsync(entries, cacheDirectory, logger);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static bool Matches(Entry entry, string clientId, string tenantId) =>
        string.Equals(entry.ClientId, clientId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.TenantId, tenantId, StringComparison.OrdinalIgnoreCase);

    private static async Task<List<Entry>> LoadAsync(string? cacheDirectory, ILoggingService? logger)
    {
        var resolution = await ResolveAsync(cacheDirectory, logger);

        // Policy keeps credentials in memory only, so nothing was ever written to read back.
        if (resolution.Helper is null)
        {
            return [];
        }

        try
        {
            var payload = resolution.Helper.LoadUnencryptedTokenCache();
            if (payload is null || payload.Length == 0)
            {
                return [];
            }

            return JsonSerializer.Deserialize<List<Entry>>(payload, JsonOptions) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // A corrupt store must not fail every command; this is the same position as an empty one.
            logger?.Warn($"Stored service principal credentials could not be read and are being ignored: {ex.Message}");
            return [];
        }
    }

    private static async Task SaveAsync(List<Entry> entries, string? cacheDirectory, ILoggingService? logger)
    {
        var resolution = await ResolveAsync(cacheDirectory, logger);

        if (resolution.Helper is null)
        {
            return;
        }

        resolution.Helper.SaveUnencryptedTokenCache(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entries, JsonOptions)));
    }

    private static async Task<TokenCacheResolution> ResolveAsync(string? cacheDirectory, ILoggingService? logger) =>
        await MsalTokenCacheFactory.CreateAsync(
            prefix: Prefix,
            cacheDirectory: cacheDirectory,
            logger: logger,
            type: MsalTokenCacheFactory.SecretStoreType);
}
