// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Core.Authentication;

namespace Benchmark.Auth;

/// <summary>
/// Places a synthetic MSAL token cache on disk so benchmarks measure a real cache read
/// instead of a cheap miss on environments (such as CI) that have no cache.
/// </summary>
internal static class MsalCacheFixture
{
    private const string SeedApplicationId = "1950a258-227b-4e31-a9cf-717495945fc2";

    /// <summary>
    /// The seeded account's user principal name, read from the seed document so it always matches
    /// the account MSAL selects.
    /// </summary>
    public static string UserPrincipalName => s_userPrincipalName.Value;

    private static readonly Lazy<string> s_userPrincipalName = new(ReadUserPrincipalName);

    public static string AccessTokenSecret => s_accessTokenSecret.Value;

    private static readonly Lazy<string> s_accessTokenSecret = new(ReadAccessTokenSecret);

    private const string SeedFileName = "MsalCache.seed.json";

    /// <summary>Where the factory puts an unprotected user cache, which is what benchmarks force.</summary>
    public static string CacheFileName { get; } = MsalTokenCacheFactory.GetCacheFileName(plaintextUnsafe: true);

    /// <summary>
    /// Copies the seed document into <paramref name="cacheDirectory"/> as MSAL's cache file,
    /// replacing its application ID with <paramref name="firstPartyApplicationId"/> and stamping
    /// the access token's expiry nine years ahead of now so the fixture never goes stale.
    /// </summary>
    public static void Seed(string cacheDirectory, string firstPartyApplicationId, string[]? scopes = null)
    {
        Directory.CreateDirectory(cacheDirectory);
        var seedJson = File.ReadAllText(SeedPath())
            .Replace(
                SeedApplicationId,
                firstPartyApplicationId,
                StringComparison.OrdinalIgnoreCase);
        File.WriteAllText(
            Path.Combine(cacheDirectory, CacheFileName),
            StampExpiry(seedJson, scopes));
    }

    // Stamps the access token expiry nine years ahead of now so the fixture never goes stale.
    private static string StampExpiry(string seedJson, string[]? scopes)
    {
        var expiry = DateTimeOffset.UtcNow.AddYears(9).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var node = JsonNode.Parse(seedJson)
            ?? throw new InvalidOperationException($"Seed document '{SeedPath()}' is not valid JSON.");

        var accessTokens = node["AccessToken"]?.AsObject()
            ?? throw new InvalidOperationException($"Seed document '{SeedPath()}' contains no AccessToken object.");

        foreach (var (key, entry) in accessTokens.ToArray())
        {
            if (entry is null)
            {
                continue;
            }

            entry["expires_on"] = expiry;
            entry["extended_expires_on"] = expiry;
            if (scopes is { Length: > 0 })
            {
                string oldTarget = entry["target"]!.GetValue<string>();
                string target = string.Join(" ", scopes);
                entry["target"] = target;
                accessTokens.Remove(key);
                accessTokens[key.Replace(oldTarget, target, StringComparison.Ordinal)] = entry;
            }
        }

        return node.ToJsonString();
    }

    private static string SeedPath()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", SeedFileName);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                $"MSAL cache seed not found: '{source}'. Ensure Fixtures/{SeedFileName} is copied to output.",
                source);
        }

        return source;
    }

    private static string ReadAccessTokenSecret()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        foreach (var entry in document.RootElement.GetProperty("AccessToken").EnumerateObject())
        {
            return entry.Value.GetProperty("secret").GetString()
                ?? throw new InvalidOperationException("Seed access token has a null secret.");
        }

        throw new InvalidOperationException(
            $"Seed document '{SeedPath()}' contains no AccessToken entry.");
    }

    private static string ReadUserPrincipalName()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SeedPath()));
        foreach (var entry in document.RootElement.GetProperty("Account").EnumerateObject())
        {
            return entry.Value.GetProperty("username").GetString()
                ?? throw new InvalidOperationException("Seed account has a null username.");
        }

        throw new InvalidOperationException(
            $"Seed document '{SeedPath()}' contains no Account entry.");
    }
}
