// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
    /// Mirrors the access-token secret built by the seed generator. Written as an expression
    /// rather than a literal so the 1400-character padding stays readable.
    /// </summary>
    public static readonly string AccessTokenSecret =
        "******" + new string('A', 1400) + ".benchmarksignature";

    private const string SeedFileName = "MsalCache.seed.json";
    private const string CacheFileName = "MSAL.cache.plaintext";

    /// <summary>Copies the seed document into <paramref name="cacheDirectory"/> as MSAL's cache file.</summary>
    public static void Seed(string cacheDirectory)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", SeedFileName);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                $"MSAL cache seed not found: '{source}'. Ensure Fixtures/{SeedFileName} is copied to output.",
                source);
        }

        Directory.CreateDirectory(cacheDirectory);
        File.Copy(source, Path.Combine(cacheDirectory, CacheFileName), overwrite: true);
    }
}
