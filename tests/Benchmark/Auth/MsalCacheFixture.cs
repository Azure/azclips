// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;

namespace Benchmark.Auth;

/// <summary>
/// Places a synthetic MSAL token cache on disk so benchmarks measure a real cache read.
/// Without it there is nothing to read: CI has no cache, and reading a missing file costs
/// almost nothing, which would make the measurement meaningless.
/// </summary>
/// <remarks>
/// The seeded access token expires in the year 3026 and carries no <c>refresh_on</c>, so MSAL
/// never has a reason to contact the network. That is deliberate and load-bearing — do not
/// "fix" the expiry. JSON cannot carry a comment, which is why the note lives here.
///
/// The seed document's <c>realm</c> must equal the tenant that <see cref="Tests.Common.Stubs.StubProfileService"/>
/// seeds, and its <c>Account.username</c> must equal <see cref="UserPrincipalName"/>. Nothing
/// enforces those at compile time; <c>BenchmarkBase.VerifyAuthFixture</c> catches a mismatch at
/// run time by failing setup.
/// </remarks>
internal static class MsalCacheFixture
{
    /// <summary>Must match <c>Account.username</c> in the seed document.</summary>
    public const string UserPrincipalName = "benchmark@contoso.com";

    /// <summary>
    /// The seeded access token's secret, read from the seed document itself so the value cannot
    /// drift from what is actually on disk. Task 4's verification compares an acquired token
    /// against this.
    /// </summary>
    public static string AccessTokenSecret => s_accessTokenSecret.Value;

    private static readonly Lazy<string> s_accessTokenSecret = new(ReadAccessTokenSecret);

    private const string SeedFileName = "MsalCache.seed.json";
    private const string CacheFileName = "MSAL.cache.plaintext";

    /// <summary>Copies the seed document into <paramref name="cacheDirectory"/> as MSAL's cache file.</summary>
    public static void Seed(string cacheDirectory)
    {
        Directory.CreateDirectory(cacheDirectory);
        File.Copy(SeedPath(), Path.Combine(cacheDirectory, CacheFileName), overwrite: true);
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

    // Read rather than duplicate: a hand-copied literal would silently drift from the document,
    // and the verification that depends on it would then be asserting against the wrong value.
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
}
