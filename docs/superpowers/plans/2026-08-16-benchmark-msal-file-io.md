# MSAL Token-Cache File IO in In-Process Benchmarks — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the in-process CLI and PowerShell benchmarks run the real `MsalAuthService` so their measured times include genuine token-cache disk reads, while making any MSAL network call impossible.

**Architecture:** A new `MsalClientOptions` record, resolved from DI, parameterizes the two inputs `MsalAuthService` currently hardcodes: the token-cache directory and MSAL's HTTP factory. Production registers a default instance that reproduces today's behavior exactly. The benchmark harness overrides it with a scratch directory seeded from a committed synthetic cache document, plus an `IMsalHttpClientFactory` that throws on any request. Because the seeded access token expires in the year 3026, MSAL always resolves from cache and never has a reason to reach the network; the throwing factory is a tripwire that turns any lapse into a hard benchmark failure.

**Tech Stack:** .NET 10 (`net10.0`), xUnit 2.9.3, Moq 4.20.72, BenchmarkDotNet 0.13.12, `Microsoft.Identity.Client` 4.83.1, `Microsoft.Identity.Client.Extensions.Msal` 4.83.1.

## Global Constraints

- Use the .NET SDK pinned in `global.json`. Build with `dotnet build azclips.slnx`.
- C# with nullable enabled and implicit usings; follow existing naming, namespace, and folder conventions.
- Test method naming: `MethodName_ShouldBehavior_WhenCondition`.
- Every new source file starts with the two-line copyright header used across the repo:
  `// Copyright (c) Microsoft Corporation.` / `// Licensed under the MIT License.`
- Preserve existing public behavior. Production auth behavior must be byte-for-byte unchanged.
- Do not commit secrets. All token material in fixtures is synthetic filler, never a real token.
- MSAL package version stays at 4.83.1; do not upgrade.
- Do not modify the startup benchmarks (`tests/Benchmark/Startup/`) — they run out-of-process and are out of scope.
- Keep `[WarmupCount(5000)]` and `[IterationCount(5000)]` at 5000 in committed code.

## Reference Values

These exact values appear in several files and must agree:

| Value | Literal |
|---|---|
| MSAL client id | `1950a258-227b-4e31-a9cf-717495945fc2` |
| Tenant / realm | `00000000-0000-0000-0000-000000000000` |
| Object id (uid) | `11111111-1111-1111-1111-111111111111` |
| Home account id | `11111111-1111-1111-1111-111111111111.00000000-0000-0000-0000-000000000000` |
| Cache environment | `login.windows.net` |
| ARM scope | `https://management.core.windows.net//.default` |
| Benchmark UPN | `benchmark@contoso.com` |
| Access-token expiry | `33324096000` (≈ year 3026) |

`login.windows.net` is deliberate: it is MSAL's *preferred cache* alias for the `login.microsoftonline.com` authority, and it is what a real cache file on disk contains. Using the authority host instead causes a cache miss.

---

## File Structure

**Create:**
- `src/Core/Core.Authentication/MsalClientOptions.cs` — the DI-resolved options record.
- `tests/Cli.Tests/MsalClientOptionsTests.cs` — unit tests for the seam.
- `tests/Benchmark/Fixtures/MsalCache.seed.json` — committed synthetic token cache (generated once by a script in Task 3).- `tests/Benchmark/Auth/MsalCacheFixture.cs` — constants shared across the harness, plus `Seed`.
- `tests/Benchmark/Auth/ThrowingMsalHttpClientFactory.cs` — the network tripwire.

**Modify:**
- `src/Core/Core.Authentication/MsalAuthService.cs` — take options; read cache directory and HTTP factory from them.
- `src/Core/Core.Authentication/DIRegisterExtensions.cs` — register the default options instance.
- `tests/Common/Stubs/StubProfileService.cs` — optional `userPrincipalName` seed.
- `tests/Benchmark/Benchmark.csproj` — fixture copy-to-output, explicit project/package references.
- `tests/Benchmark/BenchmarkBase.cs` — seed the cache, stop stubbing auth, add the fixture verification.
- `tests/Benchmark/CLI/CLIBenchmarkBase.cs` and `tests/Benchmark/PS/PSBenchmarkBase.cs` — call the verification from `GlobalSetup`.
- `wiki/02-deep-dive/testing-benchmarking.md` — correct the now-false claim that benchmarks stub auth.

---

## Task 1: Optional UPN seed on `StubProfileService`

**Files:**
- Modify: `tests/Common/Stubs/StubProfileService.cs:17-33`
- Test: covered by Task 2's third test plus the existing `Cli.Tests` / `Scenario.Tests` suites.

**Interfaces:**
- Consumes: nothing.
- Produces: `StubProfileService(bool seedDefault = true, string? userPrincipalName = null)`. Tasks 2 and 4 both use the `userPrincipalName` argument.

This task comes first because Task 2's tests construct `StubProfileService` with a UPN; without
this overload that test file does not compile.

- [ ] **Step 1: Add the optional parameter**

In `tests/Common/Stubs/StubProfileService.cs`, replace the constructor:

```csharp
    /// <param name="seedDefault">Seed a single active "default" profile.</param>
    /// <param name="userPrincipalName">
    /// UPN stamped on the seeded profile. Required by callers that exercise the real
    /// <c>MsalAuthService</c>, because account selection matches the cached account by UPN.
    /// Defaults to null, which preserves the original behaviour for existing callers.
    /// </param>
    public StubProfileService(bool seedDefault = true, string? userPrincipalName = null)
    {
        _profiles = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        if (seedDefault)
        {
            var profile = new Profile
            {
                Name = "default",
                TenantId = "00000000-0000-0000-0000-000000000000",
                SubscriptionId = "00000000-0000-0000-0000-000000000000",
                CloudName = "AzureCloud",
                AuthMethod = AuthMethod.Browser,
                UserPrincipalName = userPrincipalName,
            };
            _profiles[profile.Name] = profile;
            _active = profile.Name;
        }
    }
```

- [ ] **Step 2: Verify existing suites are unaffected**

```powershell
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tests/Scenario.Tests/Scenario.Tests.csproj
```

Expected: PASS. The default `null` reproduces the previous profile exactly, so no existing
assertion changes.

- [ ] **Step 3: Commit**

```powershell
git add tests/Common/Stubs/StubProfileService.cs
git commit -m "test: allow StubProfileService to seed a user principal name"
```

---

## Task 2: `MsalClientOptions` seam

**Files:**
- Create: `src/Core/Core.Authentication/MsalClientOptions.cs`
- Modify: `src/Core/Core.Authentication/MsalAuthService.cs:10-17` (constructor), `:95-106` (`BuildApp`), `:108-136` (`RegisterTokenCacheAsync`), `:43-44` (`LogoutAsync` builder)
- Modify: `src/Core/Core.Authentication/DIRegisterExtensions.cs:14-21`
- Test: `tests/Cli.Tests/MsalClientOptionsTests.cs`

**Interfaces:**
- Consumes: `StubProfileService(bool, string?)` from Task 1.
- Produces: `public sealed record Core.Authentication.MsalClientOptions` with `string CacheDirectory { get; init; }` and `IMsalHttpClientFactory? HttpClientFactory { get; init; }`. Task 4 constructs this record.

- [ ] **Step 1: Write the failing tests**

Create `tests/Cli.Tests/MsalClientOptionsTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Core.Abstractions;
using Core.Authentication;
using Microsoft.Identity.Client;
using Tests.Common.Stubs;
using Xunit;

namespace Cli.Tests;

// Guards the benchmark seam: MsalAuthService must honour the injected cache directory and
// MSAL HTTP factory, and production defaults must stay byte-for-byte what they were before.
public class MsalClientOptionsTests
{
    private const string ClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
    private const string TenantId = "00000000-0000-0000-0000-000000000000";
    private const string ObjectId = "11111111-1111-1111-1111-111111111111";
    private const string Environment = "login.windows.net";
    private const string Scope = "https://management.core.windows.net//.default";
    private const string Upn = "unit-test@contoso.com";
    private static string HomeAccountId => $"{ObjectId}.{TenantId}";

    private sealed class RecordingLogger : ILoggingService
    {
        public List<string> Messages { get; } = new();
        public void Log(string message) => Messages.Add(message);
        public void Info(string message) => Messages.Add(message);
        public void Verbose(string message) => Messages.Add(message);
        public void Debug(string message) => Messages.Add(message);
        public void Warn(string message) => Messages.Add(message);
        public void Error(string message) => Messages.Add(message);
    }

    private sealed class RecordingMsalHttpClientFactory : IMsalHttpClientFactory
    {
        public bool WasInvoked { get; private set; }

        public HttpClient GetHttpClient()
        {
            WasInvoked = true;
            return new HttpClient(new BlockingHandler());
        }

        private sealed class BlockingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken) =>
                throw new InvalidOperationException($"Blocked MSAL request to '{request.RequestUri}'.");
        }
    }

    private static MsalAuthService CreateService(
        MsalClientOptions options, ILoggingService logger, IProfileService profileService)
    {
        var telemetry = new StubTelemetryService();
        var managedIdentity = new ManagedIdentityAuthenticator(
            new AzureCredentialFactory(), logger, telemetry);
        return new MsalAuthService(logger, telemetry, profileService, managedIdentity, options);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "azclips-msal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    // Minimal MSAL v3 cache document with a single account whose access token is already expired,
    // so a silent acquisition is forced to attempt a refresh over HTTP.
    private static void WriteExpiredCache(string directory)
    {
        var accessTokenKey =
            $"{HomeAccountId}-{Environment}-accesstoken-{ClientId}-{TenantId}-{Scope}".ToLowerInvariant();
        var refreshTokenKey =
            $"{HomeAccountId}-{Environment}-refreshtoken-{ClientId}--".ToLowerInvariant();
        var accountKey =
            $"{HomeAccountId}-{Environment}-{TenantId}".ToLowerInvariant();

        var json = $$"""
        {
          "AccessToken": {
            "{{accessTokenKey}}": {
              "credential_type": "AccessToken",
              "secret": "expired-access-token",
              "home_account_id": "{{HomeAccountId}}",
              "environment": "{{Environment}}",
              "client_id": "{{ClientId}}",
              "target": "{{Scope}}",
              "realm": "{{TenantId}}",
              "token_type": "Bearer",
              "cached_at": "1600000000",
              "expires_on": "1600003600",
              "extended_expires_on": "1600003600"
            }
          },
          "RefreshToken": {
            "{{refreshTokenKey}}": {
              "credential_type": "RefreshToken",
              "secret": "unit-test-refresh-token",
              "home_account_id": "{{HomeAccountId}}",
              "environment": "{{Environment}}",
              "client_id": "{{ClientId}}"
            }
          },
          "Account": {
            "{{accountKey}}": {
              "home_account_id": "{{HomeAccountId}}",
              "environment": "{{Environment}}",
              "realm": "{{TenantId}}",
              "local_account_id": "{{ObjectId}}",
              "username": "{{Upn}}",
              "authority_type": "MSSTS"
            }
          }
        }
        """;

        File.WriteAllText(Path.Combine(directory, "MSAL.cache.plaintext"), json, new UTF8Encoding(false));
    }

    [Fact]
    public void CacheDirectory_ShouldDefaultToLocalAppDataRoot_WhenNotOverridden()
    {
        var options = new MsalClientOptions();

        Assert.Equal(HomePaths.GetDefaultLocalAppDataRoot(), options.CacheDirectory);
        Assert.Null(options.HttpClientFactory);
    }

    [Fact]
    public async Task AcquireTokenSilentlyAsync_ShouldUseConfiguredCacheDirectory_WhenOptionsOverrideIt()
    {
        var directory = CreateTempDirectory();
        var logger = new RecordingLogger();
        var options = new MsalClientOptions { CacheDirectory = directory };
        var service = CreateService(options, logger, new StubProfileService());

        // No account matches, so this throws — the directory is resolved before that point.
        await Assert.ThrowsAsync<MsalUiRequiredException>(
            () => service.AcquireTokenSilentlyAsync(scopes: null, tenantId: null));

        var expected = Path.Combine(directory, "MSAL.cache.plaintext");
        Assert.Contains(logger.Messages, m => m.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcquireTokenSilentlyAsync_ShouldUseInjectedHttpClientFactory_WhenCachedTokenIsExpired()
    {
        var directory = CreateTempDirectory();
        WriteExpiredCache(directory);

        var factory = new RecordingMsalHttpClientFactory();
        var options = new MsalClientOptions { CacheDirectory = directory, HttpClientFactory = factory };
        var service = CreateService(options, new RecordingLogger(), new StubProfileService(userPrincipalName: Upn));

        // The refresh attempt fails because the injected handler blocks it — that failure is the proof.
        await Assert.ThrowsAnyAsync<Exception>(
            () => service.AcquireTokenSilentlyAsync(scopes: null, tenantId: null));

        Assert.True(factory.WasInvoked, "MSAL did not use the injected IMsalHttpClientFactory.");
    }
}
```

Note: this test file depends on Task 1's `StubProfileService(userPrincipalName:)` overload. Task 1
must be complete before this compiles.

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~MsalClientOptionsTests"
```

Expected: FAIL to compile — `MsalClientOptions` does not exist and `MsalAuthService` has a four-parameter constructor.

- [ ] **Step 3: Create `MsalClientOptions`**

Create `src/Core/Core.Authentication/MsalClientOptions.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Identity.Client;

namespace Core.Authentication;

/// <summary>
/// The two inputs to MSAL that vary between production and test hosts. The default instance
/// reproduces production behaviour exactly, so registering it changes nothing; benchmarks
/// override it to point MSAL at a scratch token cache and to block its network access.
/// </summary>
public sealed record MsalClientOptions
{
    /// <summary>Directory holding the MSAL token cache file.</summary>
    public string CacheDirectory { get; init; } = HomePaths.GetDefaultLocalAppDataRoot();

    /// <summary>
    /// HTTP factory handed to MSAL. Null leaves MSAL on its own HTTP stack, which is what
    /// production uses. Tests supply one to observe or block MSAL's network calls.
    /// </summary>
    public IMsalHttpClientFactory? HttpClientFactory { get; init; }
}
```

- [ ] **Step 4: Wire the options into `MsalAuthService`**

In `src/Core/Core.Authentication/MsalAuthService.cs`, add the parameter to the primary constructor:

```csharp
public class MsalAuthService(
    ILoggingService Logger,
    ITelemetryService TelemetryService,
    IProfileService ProfileService,
    ManagedIdentityAuthenticator ManagedIdentityAuthenticator,
    MsalClientOptions Options) : IAuthService
```

Replace the body of `BuildApp` so the factory is applied when present:

```csharp
    // todo: reuse app instance but allow reinitialization when tenantId changes
    private IPublicClientApplication BuildApp(string? tenantId)
    {
        var builder = PublicClientApplicationBuilder.Create(ClientId)
            .WithRedirectUri("http://localhost");

        // No tenant supplied: use the multi-tenant "organizations" authority
        builder = string.IsNullOrWhiteSpace(tenantId)
            ? builder.WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
            : builder.WithTenantId(tenantId);

        if (Options.HttpClientFactory is not null)
        {
            builder = builder.WithHttpClientFactory(Options.HttpClientFactory);
        }

        return builder.Build();
    }
```

In `LogoutAsync`, replace the inline builder so it goes through the same path:

```csharp
        // https://learn.microsoft.com/en-us/entra/msal/dotnet/acquiring-tokens/clear-token-cache#desktop-command-line-and-mobile-applications
        var app = BuildApp(tenantId: null);
```

In `RegisterTokenCacheAsync`, replace the first line:

```csharp
        var cacheDirectory = Options.CacheDirectory;
```

- [ ] **Step 5: Register the default options instance**

In `src/Core/Core.Authentication/DIRegisterExtensions.cs`, add the using and the registration:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Authentication;

public static class DIRegisterExtensions
{
    /// <summary>
    /// Extension method to register authentication-related services.
    /// </summary>
    public static IServiceCollection RegisterAuthenticationServices(this IServiceCollection services)
    {
        // TryAdd so a host that registered its own options first (benchmarks) keeps them.
        services.TryAddSingleton(new MsalClientOptions());
        services.AddSingleton<AzureCredentialFactory>();
        services.AddSingleton<ManagedIdentityAuthenticator>();
        services.AddSingleton<IAuthService, MsalAuthService>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        return services;
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```powershell
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~MsalClientOptionsTests"
```

Expected: PASS, 3 tests.

If `AcquireTokenSilentlyAsync_ShouldUseInjectedHttpClientFactory_WhenCachedTokenIsExpired` fails with `MsalUiRequiredException` and `WasInvoked` false, the cache document was not matched. Check, in order: the composite keys are fully lowercased; `environment` is `login.windows.net`; `realm` equals the stub profile's tenant; `username` equals the profile UPN. Fix the document, not the assertion — the same schema is used by the benchmark fixture in Task 3.

- [ ] **Step 7: Commit**

```powershell
git add src/Core/Core.Authentication/MsalClientOptions.cs src/Core/Core.Authentication/MsalAuthService.cs src/Core/Core.Authentication/DIRegisterExtensions.cs tests/Cli.Tests/MsalClientOptionsTests.cs
git commit -m "feat(auth): parameterise MSAL cache directory and HTTP factory via MsalClientOptions"
```

---

## Task 3: Benchmark fixture assets

**Files:**
- Create: `tests/Benchmark/Fixtures/MsalCache.seed.json` (generated, then committed)
- Create: `tests/Benchmark/Auth/MsalCacheFixture.cs`
- Create: `tests/Benchmark/Auth/ThrowingMsalHttpClientFactory.cs`
- Modify: `tests/Benchmark/Benchmark.csproj`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `internal static class Benchmark.Auth.MsalCacheFixture` with `public const string UserPrincipalName`, `public static string AccessTokenSecret` (read from the seed document, not duplicated as a literal), and `public static void Seed(string cacheDirectory)`.
  - `internal sealed class Benchmark.Auth.ThrowingMsalHttpClientFactory : IMsalHttpClientFactory`.
  Task 4 uses all of these.

- [ ] **Step 1: Generate the seed cache document**

Save this script outside the repo — it is a one-time authoring tool, not a committed asset. The
session artifacts folder is a good home. It is deterministic: re-running reproduces the file
byte-for-byte.

```powershell
# New-MsalCacheSeed.ps1
param([Parameter(Mandatory)][string]$OutputPath)

$ErrorActionPreference = 'Stop'

$ClientId    = '1950a258-227b-4e31-a9cf-717495945fc2'
$TenantId    = '00000000-0000-0000-0000-000000000000'
$Upn         = 'benchmark@contoso.com'
$ObjectId    = '11111111-1111-1111-1111-111111111111'
$Environment = 'login.windows.net'
$Scope       = 'https://management.core.windows.net//.default'
$HomeId      = "$ObjectId.$TenantId"
$ExpiresOn   = '33324096000'   # ~year 3026: never expires during a run, so MSAL never refreshes
$CachedAt    = '1700000000'

function ConvertTo-Base64Url([string]$text) {
    [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($text)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Structurally valid JWT: MSAL parses the id token to materialise the account and its claims.
$header  = '{"typ":"JWT","alg":"none"}'
$payload = '{"aud":"' + $ClientId + '","iss":"https://login.microsoftonline.com/' + $TenantId + '/v2.0",' +
           '"iat":1700000000,"nbf":1700000000,"exp":' + $ExpiresOn + ',"name":"Benchmark User",' +
           '"oid":"' + $ObjectId + '","preferred_username":"' + $Upn + '","sub":"benchmark-subject",' +
           '"tid":"' + $TenantId + '","ver":"2.0"}'
$idToken = (ConvertTo-Base64Url $header) + '.' + (ConvertTo-Base64Url $payload) + '.'

# Opaque to MSAL; padded so the file size resembles a real ARM token cache.
$accessSecret  = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJub25lIn0.' + ('A' * 1400) + '.benchmarksignature'
$refreshSecret = '0.AbcBenchmarkRefreshToken.' + ('B' * 700)

$doc = [ordered]@{
    AccessToken = [ordered]@{
        "$HomeId-$Environment-accesstoken-$ClientId-$TenantId-$Scope".ToLowerInvariant() = [ordered]@{
            credential_type     = 'AccessToken'
            secret              = $accessSecret
            home_account_id     = $HomeId
            environment         = $Environment
            client_id           = $ClientId
            target              = $Scope
            realm               = $TenantId
            token_type          = 'Bearer'
            cached_at           = $CachedAt
            expires_on          = $ExpiresOn
            extended_expires_on = $ExpiresOn
        }
    }
    RefreshToken = [ordered]@{
        "$HomeId-$Environment-refreshtoken-$ClientId--".ToLowerInvariant() = [ordered]@{
            credential_type = 'RefreshToken'
            secret          = $refreshSecret
            home_account_id = $HomeId
            environment     = $Environment
            client_id       = $ClientId
        }
    }
    IdToken = [ordered]@{
        "$HomeId-$Environment-idtoken-$ClientId-$TenantId-".ToLowerInvariant() = [ordered]@{
            credential_type = 'IdToken'
            secret          = $idToken
            home_account_id = $HomeId
            environment     = $Environment
            client_id       = $ClientId
            realm           = $TenantId
        }
    }
    Account = [ordered]@{
        "$HomeId-$Environment-$TenantId".ToLowerInvariant() = [ordered]@{
            home_account_id  = $HomeId
            environment      = $Environment
            realm            = $TenantId
            local_account_id = $ObjectId
            username         = $Upn
            authority_type   = 'MSSTS'
        }
    }
    AppMetadata = [ordered]@{
        "appmetadata-$Environment-$ClientId" = [ordered]@{
            client_id   = $ClientId
            environment = $Environment
        }
    }
}

[IO.File]::WriteAllText($OutputPath, ($doc | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
Write-Output "Wrote $OutputPath ($((Get-Item $OutputPath).Length) bytes)"
```

Run it:

```powershell
$script = "$HOME\.copilot\session-state\New-MsalCacheSeed.ps1"   # or any path outside the repo
New-Item -ItemType Directory -Force -Path tests/Benchmark/Fixtures | Out-Null
& $script -OutputPath tests/Benchmark/Fixtures/MsalCache.seed.json
```

Expected: `Wrote ... (5245 bytes)`.

- [ ] **Step 2: Verify the generated document's shape**

```powershell
$j = Get-Content tests/Benchmark/Fixtures/MsalCache.seed.json -Raw | ConvertFrom-Json
$j.PSObject.Properties.Name
```

Expected output, in order: `AccessToken`, `RefreshToken`, `IdToken`, `Account`, `AppMetadata`.

- [ ] **Step 3: Create the fixture helper**

Create `tests/Benchmark/Auth/MsalCacheFixture.cs`:

```csharp
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
```

- [ ] **Step 4: Create the network tripwire**

Create `tests/Benchmark/Auth/ThrowingMsalHttpClientFactory.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Identity.Client;

namespace Benchmark.Auth;

/// <summary>
/// Makes MSAL network access impossible during a benchmark. Benchmarks measure token-cache
/// file IO; a token request, refresh, or instance-discovery call would silently add network
/// latency to the reported number. Any attempt throws, which fails the benchmark rather than
/// producing a quietly wrong measurement.
/// </summary>
internal sealed class ThrowingMsalHttpClientFactory : IMsalHttpClientFactory
{
    public HttpClient GetHttpClient() => new(new ThrowingHandler());

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"MSAL attempted a network request to '{request.RequestUri}' during a benchmark. " +
                "Benchmarks must resolve tokens from the seeded cache; a network call would " +
                "contaminate the measurement. Check the seed fixture and the stub profile's UPN.");
    }
}
```

- [ ] **Step 5: Wire the project file**

In `tests/Benchmark/Benchmark.csproj`, add `Microsoft.Identity.Client` to the package group, `Core.Authentication` to the project group, and a content item for the fixture:

```xml
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.13.12" />
    <PackageReference Include="Microsoft.PowerShell.SDK" Version="7.6.1" />
    <PackageReference Include="Microsoft.ApplicationInsights" Version="2.23.0" />
    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.10" />
    <PackageReference Include="Microsoft.Identity.Client" Version="4.83.1" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Core\Core.Runtime\Core.Runtime.csproj" />
    <ProjectReference Include="..\..\src\Core\Core.DependencyInjection\Core.DependencyInjection.csproj" />
    <ProjectReference Include="..\..\src\Core\Core.Authentication\Core.Authentication.csproj" />
    <ProjectReference Include="..\..\src\Extensions\Resources\Extensions.Resources.csproj" />
    <ProjectReference Include="..\..\src\Extensions\Compute\Extensions.Compute.csproj" />
    <ProjectReference Include="..\..\src\CLI\Core.Cli.csproj" />
    <ProjectReference Include="..\Common\Tests.Common.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Fixtures\*.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <Link>Fixtures\%(Filename)%(Extension)</Link>
    </Content>
  </ItemGroup>
```

- [ ] **Step 6: Verify the project builds and the fixture reaches the output directory**

```powershell
dotnet build tests/Benchmark/Benchmark.csproj -c Release
Get-ChildItem -Path tests/Benchmark/bin -Recurse -Filter MsalCache.seed.json | Select-Object FullName
```

Expected: build succeeds and `MsalCache.seed.json` appears under the build output's `Fixtures` folder.

- [ ] **Step 7: Commit**

```powershell
git add tests/Benchmark/Fixtures/MsalCache.seed.json tests/Benchmark/Auth/MsalCacheFixture.cs tests/Benchmark/Auth/ThrowingMsalHttpClientFactory.cs tests/Benchmark/Benchmark.csproj
git commit -m "test(benchmark): add synthetic MSAL cache fixture and network tripwire"
```

---

## Task 4: Run the real `MsalAuthService` in benchmarks

**Files:**
- Modify: `tests/Benchmark/BenchmarkBase.cs:58-85`
- Modify: `tests/Benchmark/CLI/CLIBenchmarkBase.cs:43-70` (`GlobalSetup`)
- Modify: `tests/Benchmark/PS/PSBenchmarkBase.cs:33-41` (`GlobalSetup`)

**Interfaces:**
- Consumes: `MsalClientOptions` (Task 2), `StubProfileService(bool, string?)` (Task 1), `MsalCacheFixture` and `ThrowingMsalHttpClientFactory` (Task 3).
- Produces: `protected string MsalCacheDirectory { get; private set; }` and `protected void VerifyAuthFixture()` on `BenchmarkBase`.

- [ ] **Step 1: Replace the auth stub with the real service**

In `tests/Benchmark/BenchmarkBase.cs`, add the usings:

```csharp
using Benchmark.Auth;
using Core.Authentication;
```

Add the property next to the other protected members:

```csharp
    /// <summary>Scratch directory holding the seeded MSAL token cache for this run.</summary>
    protected string MsalCacheDirectory { get; private set; } = null!;
```

Replace `InitializeCommon` and `RegisterBenchmarkStubs`:

```csharp
    /// <summary>
    /// Initialize repo root and seed the MSAL token cache (one-time setup).
    /// </summary>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();

        MsalCacheDirectory = Path.Combine(RepoRoot, "artifacts", "Benchmark", "home", "msal");
        MsalCacheFixture.Seed(MsalCacheDirectory);
    }

    /// <summary>
    /// Registers stub DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="AzclipsServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    /// <remarks>
    /// <see cref="IAuthService"/> is deliberately NOT stubbed: benchmarks measure the real
    /// <see cref="MsalAuthService"/> token-cache disk read. It is pointed at the seeded scratch
    /// cache and given an HTTP factory that throws, so the read is real but the network is not
    /// reachable.
    /// </remarks>
    protected void RegisterBenchmarkStubs()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IProfileService>(
                _ => new StubProfileService(userPrincipalName: MsalCacheFixture.UserPrincipalName));
            services.AddSingleton(new MsalClientOptions
            {
                CacheDirectory = MsalCacheDirectory,
                HttpClientFactory = new ThrowingMsalHttpClientFactory(),
            });
        });
    }
```

- [ ] **Step 2: Add the fixture verification**

Append to `BenchmarkBase`, and add `using Core.Abstractions;` if not already present:

```csharp
    /// <summary>
    /// Proves, once per run, that the seeded cache yields a token without touching the network.
    /// Mirrors exactly what an iteration does. A drifted fixture, a UPN mismatch, or a stubbed-out
    /// auth service would otherwise change what is being measured without changing the numbers
    /// enough to notice — this turns that into a hard setup failure.
    /// </summary>
    protected void VerifyAuthFixture()
    {
        RegisterBenchmarkStubs();
        AzclipsServiceProvider.Initialize(ClientType.CLI);
        try
        {
            var authService = AzclipsServiceProvider.GetRequiredService<IAuthService>();
            if (authService is not MsalAuthService)
            {
                throw new InvalidOperationException(
                    $"Benchmarks must exercise the real MsalAuthService but resolved " +
                    $"'{authService.GetType().Name}'. Auth must not be stubbed.");
            }

            var result = authService
                .AcquireTokenSilentlyAsync(scopes: null, tenantId: null)
                .GetAwaiter()
                .GetResult();

            if (!string.Equals(result.AccessToken, MsalCacheFixture.AccessTokenSecret, StringComparison.Ordinal) ||
                !string.Equals(result.UserPrincipalName, MsalCacheFixture.UserPrincipalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Silent acquisition returned a token that does not match the seeded cache. " +
                    $"Expected UPN '{MsalCacheFixture.UserPrincipalName}', got '{result.UserPrincipalName}'.");
            }

            Console.WriteLine("[Setup] MSAL cache hit verified — auth file IO is measured, network is blocked");
        }
        catch (Exception ex)
        {
            var cacheFile = Path.Combine(MsalCacheDirectory, "MSAL.cache.plaintext");
            var exists = File.Exists(cacheFile);
            var size = exists ? new FileInfo(cacheFile).Length : 0;
            throw new InvalidOperationException(
                $"MSAL benchmark fixture verification failed. Cache file '{cacheFile}' " +
                $"exists={exists}, size={size} bytes. The benchmark cannot run without a cache hit, " +
                "because a miss would either fail or measure something other than file IO.", ex);
        }
        finally
        {
            AzclipsServiceProvider.Dispose();
        }
    }
```

- [ ] **Step 3: Call the verification from both `GlobalSetup` methods**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, replace the closing lines of `GlobalSetup`:

```csharp
        VerifyAuthFixture();

        Console.WriteLine(
            $"[Setup] CLI benchmarks ready (stub HTTP, real MSAL cache IO); staged {stagedCount} extension(s)");
```

In `tests/Benchmark/PS/PSBenchmarkBase.cs`, replace the closing line of `GlobalSetup`:

```csharp
        VerifyAuthFixture();

        Console.WriteLine($"[Setup] PS benchmarks ready for '{ModuleName}' (stub HTTP, real MSAL cache IO)");
```

- [ ] **Step 4: Verify the project still builds**

```powershell
dotnet build tests/Benchmark/Benchmark.csproj -c Release
```

Expected: build succeeds. `StubAuthService` is no longer referenced from `BenchmarkBase`; it stays in `Tests.Common` for the other test projects that still use it.

- [ ] **Step 5: Commit**

```powershell
git add tests/Benchmark/BenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
git commit -m "test(benchmark): measure real MSAL token-cache file IO instead of stubbing auth"
```

---

## Task 5: Smoke-validate the benchmark run

**Files:** none modified permanently.

**Interfaces:**
- Consumes: everything from Tasks 1–4.
- Produces: nothing.

- [ ] **Step 1: Build the full solution**

Use the `building-the-solution` skill, which handles the known npm/TLS restore failure in `Core.Agent`.

```powershell
dotnet build azclips.slnx -c Release
```

Expected: build succeeds. Release output is required because `CLIBenchmarkBase.GlobalSetup` stages extensions from `artifacts/Release`.

- [ ] **Step 2: Run the benchmark smoke test**

Use the `validate-benchmarks` skill. It temporarily lowers `[WarmupCount]` and `[IterationCount]` to 5, runs in local mode, and restores both to 5000 afterwards.

Expected in the console output:
- `[Setup] MSAL cache hit verified — auth file IO is measured, network is blocked`
- `[Benchmark] PASS — all benchmarks completed successfully.`

- [ ] **Step 3: Confirm the counts were restored**

```powershell
git diff --stat
```

Expected: no changes to `CLIBenchmarkBase.cs` or `PSBenchmarkBase.cs`. If the skill left the counts at 5, restore them to 5000 before continuing — committing lowered counts would silently destroy the benchmarks' statistical power.

- [ ] **Step 4: Confirm no cache leaked outside the scratch directory**

```powershell
Get-ChildItem "$env:LOCALAPPDATA\.azclips" -Filter "MSAL.cache*" -ErrorAction SilentlyContinue |
    Select-Object Name, LastWriteTime
```

Expected: either no results, or a pre-existing file whose `LastWriteTime` predates the run. A file written during the run means `MsalClientOptions` was not applied and the benchmark touched the developer's real cache.

---

## Task 6: Correct the documentation

**Files:**
- Modify: `wiki/02-deep-dive/testing-benchmarking.md:92` and the Mermaid diagram at `:103-126`

**Interfaces:**
- Consumes: the behavior established in Task 4.
- Produces: nothing.

- [ ] **Step 1: Replace the stub-registration paragraph**

Replace the paragraph beginning "Benchmarks stay deterministic by reusing the _same_ stub infrastructure":

```markdown
Benchmarks stay deterministic by reusing most of the same stub infrastructure as the unit tests.
`BenchmarkBase.RegisterBenchmarkStubs()` overrides the DI container to register
`StubHttpTerminateHandler`, `StubTelemetryService`, and `StubProfileService` before each iteration,
then disposes the container in cleanup (`tests/Benchmark/BenchmarkBase.cs`).

Authentication is the deliberate exception. `IAuthService` is **not** stubbed, because a real `az`
invocation pays to read the MSAL token cache off disk and the benchmarks are meant to include that
cost. Instead, `MsalClientOptions` points the real `MsalAuthService` at a scratch cache directory
seeded from a committed synthetic cache document (`tests/Benchmark/Fixtures/MsalCache.seed.json`),
and hands MSAL a `ThrowingMsalHttpClientFactory`. The seeded token expires in the year 3026, so
MSAL always resolves from cache and never has a reason to reach the network; the throwing factory
is a tripwire that converts any lapse into a benchmark failure rather than a silently inflated
number. `BenchmarkBase.VerifyAuthFixture()` asserts the cache hit once per run during
`[GlobalSetup]`, so a drifted fixture fails setup instead of quietly changing what is measured.
```

- [ ] **Step 2: Correct the Mermaid diagram**

In the `flowchart LR` block, replace the `STUBS` node definition and add an auth node:

```
    STUBS["Stub* handlers<br>(HTTP / telemetry / profile)"]
    AUTH["Real MsalAuthService<br>seeded cache + throwing MSAL HTTP factory"]
    BDN --> BASE --> STUBS
    BASE --> AUTH
    BDN --> NC --> RUN --> TEL
    RUN -.uses.-> STUBS
    RUN -.reads cache.-> AUTH
```

Add the style line next to the existing `style STUBS ...`:

```
    style AUTH fill:#2d4a3e,stroke:#4aba8a,color:#e6edf3
```

- [ ] **Step 3: Update the references list**

In the `## References` section, replace the `tests/Benchmark/BenchmarkBase.cs:41-55` entry:

```markdown
- `tests/Benchmark/BenchmarkBase.cs` — stub DI registration, MSAL cache seeding, and fixture verification
- `tests/Benchmark/Auth/MsalCacheFixture.cs`, `tests/Benchmark/Auth/ThrowingMsalHttpClientFactory.cs` — seeded MSAL cache and network tripwire
- `src/Core/Core.Authentication/MsalClientOptions.cs` — cache directory and MSAL HTTP factory seam
```

- [ ] **Step 4: Commit**

```powershell
git add wiki/02-deep-dive/testing-benchmarking.md
git commit -m "docs: describe real MSAL cache IO in the benchmark harness"
```

---

## Post-Merge Note

The reported benchmark numbers now include auth disk cost, so they rise. The introducing pull
request will read as a regression against the historical baseline and the 115% gate will flag it.
This is expected and accepted — see the design document. Rebaseline once on merge to `main`.
