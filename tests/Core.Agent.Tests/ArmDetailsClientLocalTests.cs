using Azure.Core;
using Azure.Identity;
using Core.Abstractions;
using Core.Agent.Tools.Cache;
using Xunit;
using Xunit.Sdk;

namespace Core.Agent.Tests;

/// <summary>
/// Live-Azure smoke tests for <see cref="ArmDetailsClient"/>. These hit the
/// real ARM REST API using the developer's local Azure credentials (shared
/// MSAL cache from <c>az login</c>) and are intentionally NOT included in
/// CI runs.
///
/// To run locally:
///   az login --tenant &lt;your-tenant&gt;
///   $env:CIRRUS_TEST_ARM_ID = "/subscriptions/&lt;sub&gt;/resourceGroups/&lt;rg&gt;"
///   dotnet test --filter "Category=Local"
///
/// To skip in CI (default behaviour):
///   dotnet test --filter "Category!=Local"
///
/// Why no fixture / replay: a record/replay setup (Azure.Core.TestFramework)
/// is the right shape for shared CI tests, but it requires refactoring
/// ArmDetailsClient's static singleton into an injectable client and managing
/// sanitized HTTP fixtures. For a thin live-ARM helper that's invoked only
/// when get_resource_details(live=true) is called, the simpler local-only
/// trait pattern catches regressions without that overhead.
/// </summary>
public sealed class ArmDetailsClientLocalTests
{
    private const string ArmIdEnvVar = "CIRRUS_TEST_ARM_ID";

    // Local tests use an AzureCliCredential-backed IAuthService stub so
    // they exercise the full IAuthService -> AuthServiceTokenCredential -> ArmClient
    // path against the developer's `az login` session.
    private static ArmDetailsClient NewClient() =>
        new ArmDetailsClient(new AzureCliAuthServiceStub());

    [Fact]
    [Trait("Category", "Local")]
    public async Task GetByIdAsync_ShouldReturnResourceBody_ForRealArmId()
    {
        var armId = RequireArmIdEnvVar();

        var result = await NewClient().GetByIdAsync(armId);

        Assert.NotNull(result);
        var body = result.Value;
        Assert.True(body.TryGetProperty("id", out var idProp), "Response body missing 'id' property.");
        Assert.Equal(armId, idProp.GetString(), ignoreCase: true);
    }

    [Fact]
    [Trait("Category", "Local")]
    public async Task GetByIdAsync_ShouldReturnNull_ForEmptyInput()
    {
        // No env var needed — this exercises the fast-path validation only.
        var result = await NewClient().GetByIdAsync("");
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Local")]
    public async Task GetByIdAsync_ShouldCacheRepeatedCalls()
    {
        var armId = RequireArmIdEnvVar();
        var client = NewClient();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var first = await client.GetByIdAsync(armId);
        var firstElapsed = sw.Elapsed;

        sw.Restart();
        var second = await client.GetByIdAsync(armId);
        var secondElapsed = sw.Elapsed;

        Assert.NotNull(first);
        Assert.NotNull(second);
        // Second call hits the 2-minute process-local cache and should be
        // dramatically faster than the first (which paid a token + ARM RTT).
        Assert.True(secondElapsed.TotalMilliseconds < firstElapsed.TotalMilliseconds / 2,
            $"Expected cache hit to be >2x faster: first={firstElapsed.TotalMilliseconds}ms, second={secondElapsed.TotalMilliseconds}ms");
    }

    private static string RequireArmIdEnvVar()
    {
        var armId = Environment.GetEnvironmentVariable(ArmIdEnvVar);
        if (string.IsNullOrWhiteSpace(armId))
        {
            // xunit v2 has no Assert.Skip; throw the framework's skip
            // sentinel exception. Same effect: test reports as Skipped.
            throw new SkipTestException(
                $"Set {ArmIdEnvVar} to an existing ARM resource ID to run this test (e.g. an RG you can read).");
        }
        return armId;
    }

    /// <summary>
    /// xunit v2 doesn't expose Assert.Skip; this is the framework's
    /// equivalent — derives from FrameworkException so the runner reports
    /// as Skipped rather than Failed.
    /// </summary>
    private sealed class SkipTestException : XunitException
    {
        public SkipTestException(string reason) : base(reason) { }
    }

    /// <summary>
    /// Minimal IAuthService stub backed by AzureCliCredential — uses the
    /// developer's `az login` session to acquire ARM tokens. Only
    /// AcquireTokenSilentlyAsync is implemented (the only method the
    /// AuthServiceTokenCredential adapter calls).
    /// </summary>
    private sealed class AzureCliAuthServiceStub : IAuthService
    {
        private readonly AzureCliCredential _credential = new();

        public async Task<AuthResult> AcquireTokenSilentlyAsync(string[]? scopes, string? tenantId, bool promptOnError = false, CancellationToken cancellationToken = default)
        {
            var requestedScopes = scopes ?? new[] { "https://management.core.windows.net//.default" };
            var ctx = new TokenRequestContext(requestedScopes, tenantId: tenantId);
            var token = await _credential.GetTokenAsync(ctx, cancellationToken);
            return new AuthResult
            {
                AccessToken = token.Token,
                ExpiresOn = token.ExpiresOn,
                Scopes = requestedScopes,
                TenantId = tenantId ?? string.Empty,
                CorrelationId = Guid.Empty,
            };
        }

        public Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task LogoutAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

