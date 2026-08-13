# Real Profile File IO in Benchmarks — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the CLI and PowerShell benchmarks resolve profiles from real files on disk instead of an in-memory stub, so published timings include the profile file IO a real user pays.

**Architecture:** `ProfileService` already exposes a `baseDirectory` constructor parameter that DI leaves null in production. Benchmarks pass an explicit isolated directory (`artifacts/Benchmark/home/.azclips`) instead, seeded before every iteration with a single active `default` profile whose values match today's `StubProfileService`. No production code changes.

**Tech Stack:** C# / .NET 10, xUnit 2.9.3, BenchmarkDotNet 0.13.12, `Microsoft.Extensions.DependencyInjection`.

**Spec:** `docs/superpowers/specs/2026-08-13-benchmark-real-profile-io-design.md`

## Global Constraints

- Use the .NET SDK pinned in `global.json` — version `10.0.203`, `rollForward: latestMinor`.
- All projects target `net10.0` with `Nullable` and `ImplicitUsings` enabled.
- Test method names use `MethodName_ShouldBehavior_WhenCondition`. Analyzer rule `CA1707` is relaxed in `tests/Directory.Build.props` specifically to allow the underscores.
- New source files start with the two-line header:
  ```csharp
  // Copyright (c) Microsoft Corporation.
  // Licensed under the MIT License.
  ```
- Do not modify any file under `src/` or `Agent/`. This change is confined to `tests/`, `wiki/`, and `docs/`.
- Do not remove `StubProfileService`. `Cli.Tests` and `Scenario.Tests` still depend on it.
- Do not change `[WarmupCount]` or `[IterationCount]` values in committed code. They are `5000` in both benchmark base classes.

## Background an implementer needs

**How benchmarks stub services today.** `BenchmarkBase.RegisterBenchmarkStubs()` calls `AzclipsServiceProvider.OverrideRegistration(...)`, which *enqueues* a registration lambda. The container is not built until `AzclipsServiceProvider.Initialize(...)` runs, which happens inside the measured benchmark method (`Core.Cli.Program.Main` for CLI, module import for PS). `IterationCleanup` calls `AzclipsServiceProvider.Dispose()`, which disposes the container, clears the queued registrations, and nulls the cached provider. So every iteration builds a brand-new container and a brand-new `ProfileService` — nothing caches profiles across iterations.

**Consequence:** anything running in `[GlobalSetup]` or `[IterationSetup]` executes *outside* the DI container and cannot resolve services. That is why the seeder must construct its own `ProfileService` and supply its own `ILoggingService`.

**Why not the real logger in the seeder.** `ProfileService` calls `_logger.Verbose(...)` for each file it writes. The real `ScopedLoggingService` falls back to `ConsoleLoggingService`, whose `Verbose` reaches `ConsoleSink.Verbose`, which writes to stderr **unconditionally** — there is no verbosity gate (`src/Core/Core.Runtime/ConsoleSink.cs:75`). Seeding runs ~10,000 times per benchmark method, so the real logger would emit ~20,000 colored `VERBOSE:` lines into BenchmarkDotNet's output.

**Why the store must be seeded.** `Engine.ResolveProfileAsync` (`src/Core/Core.Engine/Engine.cs:151-169`) calls `GetActiveProfileNameAsync()` and returns null immediately if it is blank — `GetProfileAsync` is never reached. An empty store would cost one `File.Exists` and measure almost nothing, while still exiting 0. That silent-degradation risk is why Task 1 builds a `VerifySeededAsync` guard.

**Async in BenchmarkDotNet setup.** `[GlobalSetup]` and `[IterationSetup]` methods here are `void`. `ProfileService` is async-only, so callers block with `.GetAwaiter().GetResult()`. This matches the existing convention in `CLIBenchmarkBase.RunCli` (`tests/Benchmark/CLI/CLIBenchmarkBase.cs:99`).

## File Structure

| File | Action | Responsibility |
| --- | --- | --- |
| `tests/Common/Tests.Common.csproj` | Modify | Add `Core.Runtime` project reference so the store can use `ProfileService` |
| `tests/Common/Stubs/NullLoggingService.cs` | Create | Shared no-op `ILoggingService` for use outside DI |
| `tests/Common/Utilities/BenchmarkProfileStore.cs` | Create | Store path, seeding, verification, cleanup |
| `tests/Cli.Tests/BenchmarkProfileStoreTests.cs` | Create | Unit tests for the store |
| `tests/Benchmark/BenchmarkBase.cs` | Modify | Seed the store; register real `ProfileService`; expose cleanup |
| `tests/Benchmark/CLI/CLIBenchmarkBase.cs` | Modify | Re-seed in `IterationSetup`; call cleanup in `GlobalCleanup` |
| `tests/Benchmark/PS/PSBenchmarkBase.cs` | Modify | Re-seed in `IterationSetup`; add a `GlobalCleanup` |
| `wiki/02-deep-dive/testing-benchmarking.md` | Modify | Correct the docs that state benchmarks stub the profile service |

`BenchmarkProfileStore` lives in `Tests.Common` rather than `tests/Benchmark` because `Benchmark.csproj` is an `OutputType=Exe` that no test project references, which would leave the store untestable. `Cli.Tests` already references both `Core.Runtime` and `Tests.Common`, so no dependency cycle is created.

---

### Task 1: Seedable benchmark profile store

Builds the store abstraction and its tests. The `NullLoggingService` stub and the csproj reference are scaffolding this deliverable needs, so they are folded in here.

**Files:**
- Modify: `tests/Common/Tests.Common.csproj:3-6`
- Create: `tests/Common/Stubs/NullLoggingService.cs`
- Create: `tests/Common/Utilities/BenchmarkProfileStore.cs`
- Test: `tests/Cli.Tests/BenchmarkProfileStoreTests.cs`

**Interfaces:**
- Consumes: `Core.Abstractions.ILoggingService`, `Core.Abstractions.Profile`, `Core.Abstractions.AuthMethod`, `Core.Runtime.ProfileService`.
- Produces, all used by Task 2:
  - `Tests.Common.Stubs.NullLoggingService` — parameterless, implements `ILoggingService`.
  - `static string BenchmarkProfileStore.GetRoot(string repoRoot)`
  - `static Task BenchmarkProfileStore.SeedAsync(string root)`
  - `static Task BenchmarkProfileStore.VerifySeededAsync(string root)`
  - `static void BenchmarkProfileStore.Cleanup(string root)`
  - `const string BenchmarkProfileStore.ProfileName` = `"default"`
  - `const string BenchmarkProfileStore.BenchmarkTenantId` = `"00000000-0000-0000-0000-000000000000"`
  - `const string BenchmarkProfileStore.BenchmarkSubscriptionId` = `"00000000-0000-0000-0000-000000000000"`

- [ ] **Step 1: Add the `Core.Runtime` project reference**

`Tests.Common` currently references only `Core.Abstractions` and `Core.Extensions`. Replace the first `ItemGroup` in `tests/Common/Tests.Common.csproj` with:

```xml
  <ItemGroup>
    <ProjectReference Include="..\..\src\Core\Core.Abstractions\Core.Abstractions.csproj" />
    <ProjectReference Include="..\..\src\Core\Core.Extensions\Core.Extensions.csproj" />
    <ProjectReference Include="..\..\src\Core\Core.Runtime\Core.Runtime.csproj" />
  </ItemGroup>
```

Leave the second `ItemGroup` (the `Stubs\Fixtures\*.json` content) untouched.

- [ ] **Step 2: Write the failing tests**

Create `tests/Cli.Tests/BenchmarkProfileStoreTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Tests.Common.Stubs;
using Tests.Common.Utilities;
using Xunit;

namespace Cli.Tests;

public sealed class BenchmarkProfileStoreTests : IDisposable
{
    private readonly string _dir;

    public BenchmarkProfileStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "azclips-benchmark-store-tests", Guid.NewGuid().ToString("N"));
    }

    private static ProfileService NewService(string dir) => new(new NullLoggingService(), dir);

    [Fact]
    public void GetRoot_ShouldPointAtBenchmarkHome_WhenGivenRepoRoot()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "fake-repo");

        var root = BenchmarkProfileStore.GetRoot(repoRoot);

        Assert.Equal(
            Path.Combine(repoRoot, "artifacts", "Benchmark", "home", ".azclips"),
            root);
    }

    [Fact]
    public async Task SeedAsync_ShouldWriteBothStoreFiles_WhenDirectoryDoesNotExist()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        Assert.True(File.Exists(Path.Combine(_dir, "profiles.json")));
        Assert.True(File.Exists(Path.Combine(_dir, "active-profile")));
    }

    [Fact]
    public async Task SeedAsync_ShouldProduceResolvableActiveProfile_WhenReadByFreshService()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        var active = await NewService(_dir).GetActiveProfileAsync();

        Assert.NotNull(active);
        Assert.Equal(BenchmarkProfileStore.ProfileName, active!.Name);
        Assert.Equal(BenchmarkProfileStore.BenchmarkSubscriptionId, active.SubscriptionId);
        Assert.Equal(BenchmarkProfileStore.BenchmarkTenantId, active.TenantId);
        Assert.Equal("AzureCloud", active.CloudName);
        Assert.Equal(AuthMethod.Browser, active.AuthMethod);
    }

    [Fact]
    public async Task SeedAsync_ShouldRemoveProfilesWrittenByAPreviousIteration()
    {
        await NewService(_dir).SaveProfileAsync(new Profile
        {
            Name = "leftover",
            TenantId = "t",
            SubscriptionId = "s",
            CloudName = "AzureCloud",
            AuthMethod = AuthMethod.Browser,
        });

        await BenchmarkProfileStore.SeedAsync(_dir);

        var all = await NewService(_dir).GetAllProfilesAsync();
        Assert.Equal(BenchmarkProfileStore.ProfileName, Assert.Single(all).Name);
    }

    [Fact]
    public async Task SeedAsync_ShouldRestoreActivePointer_WhenAPreviousIterationClearedIt()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);
        await NewService(_dir).SetActiveProfileNameAsync(null);

        await BenchmarkProfileStore.SeedAsync(_dir);

        Assert.Equal(
            BenchmarkProfileStore.ProfileName,
            await NewService(_dir).GetActiveProfileNameAsync());
    }

    [Fact]
    public async Task SeedAsync_ShouldBeIdempotent_WhenCalledRepeatedly()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);
        await BenchmarkProfileStore.SeedAsync(_dir);
        await BenchmarkProfileStore.SeedAsync(_dir);

        Assert.Single(await NewService(_dir).GetAllProfilesAsync());
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldSucceed_WhenStoreIsSeeded()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        var ex = await Record.ExceptionAsync(() => BenchmarkProfileStore.VerifySeededAsync(_dir));

        Assert.Null(ex);
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldThrow_WhenStoreIsEmpty()
    {
        Directory.CreateDirectory(_dir);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => BenchmarkProfileStore.VerifySeededAsync(_dir));

        Assert.Contains(_dir, ex.Message);
    }

    [Fact]
    public async Task VerifySeededAsync_ShouldThrow_WhenActivePointerNamesAMissingProfile()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);
        await NewService(_dir).RemoveProfileAsync(BenchmarkProfileStore.ProfileName);
        await NewService(_dir).SetActiveProfileNameAsync(BenchmarkProfileStore.ProfileName);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => BenchmarkProfileStore.VerifySeededAsync(_dir));
    }

    [Fact]
    public async Task Cleanup_ShouldRemoveTheStoreDirectory()
    {
        await BenchmarkProfileStore.SeedAsync(_dir);

        BenchmarkProfileStore.Cleanup(_dir);

        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Cleanup_ShouldNotThrow_WhenDirectoryIsAbsent()
    {
        var ex = Record.Exception(() => BenchmarkProfileStore.Cleanup(Path.Combine(_dir, "never-created")));

        Assert.Null(ex);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~BenchmarkProfileStoreTests"
```

Expected: compile failure — `NullLoggingService` and `BenchmarkProfileStore` do not exist.

- [ ] **Step 4: Create the shared `NullLoggingService`**

Create `tests/Common/Stubs/NullLoggingService.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;

namespace Tests.Common.Stubs;

/// <summary>
/// No-op <see cref="ILoggingService"/> for code that must construct a service outside the DI
/// container. The real <c>ScopedLoggingService</c> falls back to console output whose
/// <c>Verbose</c> channel has no verbosity gate, so using it in a tight setup loop floods stderr.
/// </summary>
public sealed class NullLoggingService : ILoggingService
{
    public void Log(string message) { }

    public void Info(string message) { }

    public void Verbose(string message) { }

    public void Debug(string message) { }

    public void Warn(string message) { }

    public void Error(string message) { }
}
```

- [ ] **Step 5: Create `BenchmarkProfileStore`**

Create `tests/Common/Utilities/BenchmarkProfileStore.cs`:

```csharp
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Core.Abstractions;
using Core.Runtime;
using Tests.Common.Stubs;

namespace Tests.Common.Utilities;

/// <summary>
/// Owns the isolated, file-backed profile store the benchmarks run against.
/// <para>
/// Benchmarks use the real <see cref="ProfileService"/> so their timings include genuine profile
/// file IO. The store is deliberately kept out of the developer's real
/// <c>{LocalApplicationData}/.azclips</c>: redirecting that via environment variables does not work
/// in-process on Windows, because .NET resolves <see cref="Environment.SpecialFolder.LocalApplicationData"/>
/// through the Win32 shell API rather than <c>%LOCALAPPDATA%</c>.
/// </para>
/// </summary>
public static class BenchmarkProfileStore
{
    /// <summary>Name of the single seeded profile. Matches the profile <c>StubProfileService</c> seeded.</summary>
    public const string ProfileName = "default";

    public const string BenchmarkTenantId = "00000000-0000-0000-0000-000000000000";

    public const string BenchmarkSubscriptionId = "00000000-0000-0000-0000-000000000000";

    private const string ProfilesFileName = "profiles.json";
    private const string ActiveProfileFileName = "active-profile";

    /// <summary>
    /// Store location: a sibling of the existing benchmark extensions home
    /// (<c>artifacts/Benchmark/home/extensions</c>), so the whole benchmark home is one tree.
    /// </summary>
    public static string GetRoot(string repoRoot) =>
        Path.Combine(repoRoot, "artifacts", "Benchmark", "home", ".azclips");

    /// <summary>
    /// Resets the store to exactly one active <see cref="ProfileName"/> profile, discarding anything
    /// a previous iteration wrote. Writes go through the production <see cref="ProfileService"/> so
    /// the on-disk bytes are exactly what a real user's store contains.
    /// </summary>
    public static async Task SeedAsync(string root)
    {
        Directory.CreateDirectory(root);
        DeleteIfExists(Path.Combine(root, ProfilesFileName));
        DeleteIfExists(Path.Combine(root, ActiveProfileFileName));

        var service = new ProfileService(new NullLoggingService(), root);
        await service.SaveAndActivateProfileAsync(CreateSeedProfile());
    }

    /// <summary>
    /// Throws unless the store resolves to the seeded active profile. Without this guard a failed
    /// seed would leave <c>Engine.ResolveProfileAsync</c> short-circuiting on a blank active-profile
    /// name, silently benchmarking a profile-less path that still exits 0.
    /// </summary>
    public static async Task VerifySeededAsync(string root)
    {
        var service = new ProfileService(new NullLoggingService(), root);
        var active = await service.GetActiveProfileAsync();

        if (active is null || !string.Equals(active.Name, ProfileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Benchmark profile store at '{root}' did not resolve the seeded '{ProfileName}' profile " +
                $"(resolved: '{active?.Name ?? "<null>"}'). Benchmarks would measure a profile-less path.");
        }
    }

    /// <summary>Best-effort removal of the store. Never throws.</summary>
    public static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    private static Profile CreateSeedProfile() => new()
    {
        Name = ProfileName,
        TenantId = BenchmarkTenantId,
        SubscriptionId = BenchmarkSubscriptionId,
        CloudName = "AzureCloud",
        AuthMethod = AuthMethod.Browser,
    };

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~BenchmarkProfileStoreTests"
```

Expected: PASS, 11 tests.

If the build fails while restoring the GitHub Copilot npm package from `registry.npmjs.org` (a TLS handshake / SSL failure originating from `Core.Agent`), use the `building-the-solution` skill — that failure is a known environment issue, not a defect in this change.

- [ ] **Step 7: Confirm no existing tests regressed**

```bash
dotnet test tests/Cli.Tests/Cli.Tests.csproj
```

Expected: PASS. The new `Core.Runtime` reference on `Tests.Common` must not disturb anything — `Cli.Tests` already referenced `Core.Runtime` directly.

- [ ] **Step 8: Commit**

```bash
git add tests/Common/Tests.Common.csproj tests/Common/Stubs/NullLoggingService.cs tests/Common/Utilities/BenchmarkProfileStore.cs tests/Cli.Tests/BenchmarkProfileStoreTests.cs
git commit -m "test: add seedable benchmark profile store and shared null logger"
```

---

### Task 2: Point the benchmarks at the real ProfileService

Swaps `StubProfileService` for the real file-backed service across the in-process CLI and PowerShell benchmark bases.

**Files:**
- Modify: `tests/Benchmark/BenchmarkBase.cs:55-86`
- Modify: `tests/Benchmark/CLI/CLIBenchmarkBase.cs:72-91`
- Modify: `tests/Benchmark/PS/PSBenchmarkBase.cs:43-76`

**Interfaces:**
- Consumes: everything Task 1 produced — `BenchmarkProfileStore.GetRoot/SeedAsync/VerifySeededAsync/Cleanup`.
- Produces (used by the two derived bases): `protected string ProfileStoreRoot`, `protected void SeedProfileStore()`, `protected void CleanupGlobal()` on `BenchmarkBase`.

- [ ] **Step 1: Replace the profile stub in `BenchmarkBase`**

In `tests/Benchmark/BenchmarkBase.cs`, add `using Core.Runtime;` to the existing using block, then replace the `InitializeCommon` / `RegisterBenchmarkStubs` / `CleanupCommon` members (currently lines 55-86, from the `/// <summary>` above `InitializeCommon` to the closing brace of `CleanupCommon`) with:

```csharp
    /// <summary>
    /// The isolated, file-backed profile store the benchmarks resolve profiles from.
    /// </summary>
    protected string ProfileStoreRoot { get; private set; } = null!;

    /// <summary>
    /// Initialize repo root and the seeded profile store (one-time setup).
    /// </summary>
    protected void InitializeCommon()
    {
        RepoRoot = RepoLocator.GetRepoRoot();
        ProfileStoreRoot = BenchmarkProfileStore.GetRoot(RepoRoot);

        SeedProfileStore();

        // Fail loudly here rather than silently benchmarking a profile-less path: a store that does
        // not resolve an active profile makes Engine.ResolveProfileAsync short-circuit, and the
        // benchmark would still exit 0 while measuring far less work than a real user pays.
        BenchmarkProfileStore.VerifySeededAsync(ProfileStoreRoot).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Resets the profile store to its seeded state. Called from each iteration's setup so every
    /// iteration starts from identical bytes even when a benchmark writes profiles.
    /// </summary>
    protected void SeedProfileStore() =>
        BenchmarkProfileStore.SeedAsync(ProfileStoreRoot).GetAwaiter().GetResult();

    /// <summary>
    /// Registers stub DI overrides for benchmark isolation.
    /// Call before each iteration since <see cref="AzclipsServiceProvider.Dispose"/>
    /// clears all registrations.
    /// </summary>
    /// <remarks>
    /// <see cref="IProfileService"/> is deliberately NOT stubbed: benchmarks use the real
    /// file-backed <see cref="ProfileService"/> against <see cref="ProfileStoreRoot"/> so measured
    /// timings include the profile file IO a real user pays. It receives the real
    /// <see cref="ILoggingService"/> from the container, exactly as in production.
    /// </remarks>
    protected void RegisterBenchmarkStubs()
    {
        AzclipsServiceProvider.OverrideRegistration(services =>
        {
            services.AddSingleton<IHttpTerminateHandler, StubHttpTerminateHandler>();
            services.AddSingleton<ITelemetryService, StubTelemetryService>();
            services.AddSingleton<IProfileService>(sp =>
                new ProfileService(sp.GetRequiredService<ILoggingService>(), ProfileStoreRoot));
            services.AddSingleton<IAuthService, StubAuthService>();
        });
    }

    /// <summary>
    /// Cleanup: dispose DI container (also clears registrations).
    /// </summary>
    protected void CleanupCommon()
    {
        AzclipsServiceProvider.Dispose();
    }

    /// <summary>
    /// One-time cleanup: remove the benchmark profile store. Best-effort.
    /// </summary>
    protected void CleanupGlobal() => BenchmarkProfileStore.Cleanup(ProfileStoreRoot);
```

`Tests.Common.Stubs` and `Tests.Common.Utilities` are already imported at the top of the file, so `BenchmarkProfileStore` resolves without a new using. `StubProfileService` is no longer referenced from this file — that is expected; it remains in use by `Cli.Tests` and `Scenario.Tests`.

- [ ] **Step 2: Re-seed each iteration in the CLI base**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, replace the `IterationSetup` and `GlobalCleanup` methods with:

```csharp
    [IterationSetup]
    public void IterationSetup()
    {
        SeedProfileStore();
        RegisterBenchmarkStubs();
        RegisterExtensionResolution();
    }
```

```csharp
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupGlobal();

        // Best-effort: InstalledExtensionLoader caches the extension ALC for the process
        // lifetime, so the staged DLL may still be locked here. A leftover home is harmless.
        TryDeleteDirectory(Path.Combine(RepoRoot, "artifacts", "Benchmark", "home"));
    }
```

Leave `IterationCleanup` and `GlobalSetup` unchanged.

- [ ] **Step 3: Re-seed each iteration in the PS base and add cleanup**

In `tests/Benchmark/PS/PSBenchmarkBase.cs`, add the seeding call as the first statement of `IterationSetup`:

```csharp
    [IterationSetup]
    public void IterationSetup()
    {
        SeedProfileStore();
        RegisterBenchmarkStubs();

        // Prime the runspace and import the module. Module import execution is excluded from the measured region, so that the benchmark measures only the cmdlet invocation.
        _runspace = RunspaceFactory.CreateRunspace();
```

Leave the rest of the method body unchanged. Then add a new method immediately after `IterationCleanup` (this class has no `[GlobalCleanup]` today):

```csharp
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        CleanupGlobal();
    }
```

- [ ] **Step 4: Verify the solution builds**

```bash
dotnet build azclips.slnx
```

Expected: build succeeds with no errors.

- [ ] **Step 5: Prove the store is actually used — run the benchmark smoke test**

Use the `validate-benchmarks` skill.

**Important — that skill's text is stale.** It instructs you to replace `[IterationCount(10000)]` at `PSBenchmarkBase.cs:17` and `CLIBenchmarkBase.cs:21`. The current source instead has `[WarmupCount(5000)]` / `[IterationCount(5000)]` at lines 17-18 (PS) and 21-22 (CLI). Apply the skill's intent with the correct values: temporarily set **both** `WarmupCount` and `IterationCount` to `5` in both files, and restore both to `5000` afterwards. A `5000`-warmup smoke run is far too slow to be useful.

The skill's other steps still apply, in particular the Release prerequisite:

```powershell
dotnet build azclips.slnx -c Release
./build/Set-ModuleConfiguration.ps1 -c Release
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL -c Release
```

Expected: exit code 0 and a `[Benchmark] PASS` summary line. Do not interpret the timing numbers.

Treat the temporary count edits as try/finally — restore them even if the run fails, then confirm:

```bash
git diff -- tests/Benchmark/PS/PSBenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs
```

The diff must show only your intentional `IterationSetup` / `GlobalCleanup` changes, with `WarmupCount(5000)` and `IterationCount(5000)` intact in both files.

- [ ] **Step 6: Prove the guard works**

This confirms the fail-loud behavior that protects against a silently degraded benchmark. Temporarily comment out the `SeedProfileStore();` call inside `InitializeCommon` in `tests/Benchmark/BenchmarkBase.cs`, delete any existing store, and re-run the smoke command from Step 5:

```powershell
Remove-Item -Recurse -Force artifacts/Benchmark/home/.azclips -ErrorAction SilentlyContinue
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL -c Release
```

Expected: the run fails with the `InvalidOperationException` message from `VerifySeededAsync`, naming the store path. If it instead passes, the guard is not wired correctly — fix it before continuing.

Restore the `SeedProfileStore();` call and confirm:

```bash
git diff -- tests/Benchmark/BenchmarkBase.cs
```

The diff must contain an uncommented `SeedProfileStore();` inside `InitializeCommon`.

- [ ] **Step 7: Commit**

```bash
git add tests/Benchmark/BenchmarkBase.cs tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
git commit -m "test: benchmark against the real file-backed ProfileService"
```

---

### Task 3: Update the benchmarking documentation

The wiki currently tells readers that benchmarks stub the profile service. Leaving it is a factual error about the thing this change alters.

**Files:**
- Modify: `wiki/02-deep-dive/testing-benchmarking.md:92`, `:114`, `:156`

- [ ] **Step 1: Correct the stub paragraph**

Replace the paragraph at line 92:

```markdown
Benchmarks stay deterministic by reusing the _same_ stub infrastructure as the unit tests. `BenchmarkBase.RegisterBenchmarkStubs()` overrides the DI container to register `StubHttpTerminateHandler`, `StubTelemetryService`, and `StubAuthService` before each iteration, then disposes the container in cleanup (`tests/Benchmark/BenchmarkBase.cs`).

`IProfileService` is deliberately **not** stubbed. Benchmarks resolve profiles through the real, file-backed `ProfileService`, pointed at an isolated store at `artifacts/Benchmark/home/.azclips` and seeded with a single active `default` profile before every iteration (`tests/Common/Utilities/BenchmarkProfileStore.cs`). Without this the measured timings would omit the two file reads and JSON deserialize that `Engine.ResolveProfileAsync` costs a real user on every invocation. The store is re-seeded in `[IterationSetup]`, outside the measured region, so each iteration starts from identical bytes.
```

- [ ] **Step 2: Correct the mermaid diagram label**

At line 114, change the `STUBS` node label so it no longer claims profiles are stubbed:

```
    STUBS["Stub* handlers<br>(HTTP / telemetry / auth)"]
```

- [ ] **Step 3: Correct the references list**

At line 156, replace the `BenchmarkBase.cs:41-55` bullet with these two:

```markdown
- `tests/Benchmark/BenchmarkBase.cs` — stub DI registration and real `ProfileService` wiring for benchmarks
- `tests/Common/Utilities/BenchmarkProfileStore.cs` — isolated, seeded profile store used by benchmarks
```

- [ ] **Step 4: Verify the line references you cited are accurate**

```powershell
Select-String -Path wiki/02-deep-dive/testing-benchmarking.md -Pattern 'RegisterBenchmarkStubs|BenchmarkProfileStore'
```

Confirm the text you wrote matches the real file paths. Documentation changes need no build or test run.

- [ ] **Step 5: Commit**

```bash
git add wiki/02-deep-dive/testing-benchmarking.md
git commit -m "docs: benchmarks use the real file-backed ProfileService"
```

---

### Task 4: Measure and record the baseline delta (indicative)

The spec accepts a one-time baseline shift, on the condition that it is measured and documented. The PR gate fails anything above 115% of the historical baseline, so a reviewer needs numbers to distinguish "expected cost of real IO" from "accidental regression".

**This task produces an INDICATIVE measurement, not an authoritative baseline.** Both runs use a temporarily lowered `WarmupCount(5)` / `IterationCount(5)` so the comparison completes in minutes rather than hours. At 5 iterations BenchmarkDotNet's run-to-run variance is large relative to the profile-IO delta being measured, so the result answers "did this get slower, and roughly by how much" — it does not produce a number anyone should quote as the CI gate figure. The authoritative numbers come from CI on the real `5000` counts. The results document must state this limitation prominently.

**Files:**
- Create: `docs/superpowers/plans/2026-08-13-benchmark-real-profile-io-results.md`
- Temporarily modify (must be reverted before committing): `tests/Benchmark/CLI/CLIBenchmarkBase.cs:21-22`, `tests/Benchmark/PS/PSBenchmarkBase.cs:17-18`

- [ ] **Step 1: Lower the counts in this worktree**

In `tests/Benchmark/CLI/CLIBenchmarkBase.cs`, change line 21 `[WarmupCount(5000)]` → `[WarmupCount(5)]` and line 22 `[IterationCount(5000)]` → `[IterationCount(5)]`.

In `tests/Benchmark/PS/PSBenchmarkBase.cs`, change line 17 `[WarmupCount(5000)]` → `[WarmupCount(5)]` and line 18 `[IterationCount(5000)]` → `[IterationCount(5)]`.

Treat Steps 1-6 as a try/finally: these edits MUST be reverted in Step 6 regardless of what happens in between.

- [ ] **Step 2: Capture the "after" numbers from this branch**

```powershell
dotnet build azclips.slnx -c Release
./build/Set-ModuleConfiguration.ps1 -c Release
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL -c Release
```

Record the reported Mean for every benchmark.

- [ ] **Step 3: Capture the "before" numbers from `main`**

Use a scratch worktree so the current branch is undisturbed. `main` still has `5000` counts committed, so lower them there too — otherwise the two runs are not comparable.

```powershell
git worktree add ../azclips-baseline main
cd ../azclips-baseline
```

Apply the same four edits as Step 1 to the scratch worktree's copies of `tests/Benchmark/CLI/CLIBenchmarkBase.cs` and `tests/Benchmark/PS/PSBenchmarkBase.cs`. These are throwaway — the whole worktree is deleted in Step 5.

```powershell
dotnet build azclips.slnx -c Release
./build/Set-ModuleConfiguration.ps1 -c Release
dotnet run --project tests/Benchmark/Benchmark.csproj -- --local --product ALL -c Release
```

Record the Mean for every benchmark. Confirm before running that both worktrees show `WarmupCount(5)` and `IterationCount(5)`.

- [ ] **Step 4: Build the comparison table**

For each benchmark compute `after / before * 100` as a percentage. Fill in this template with the measured values — one row per benchmark reported by the run:

```markdown
| Benchmark | Before (main) | After | Change |
| --- | --- | --- | --- |
| CLI: List RG | <mean> | <mean> | <pct>% |
| CLI: Create RG | <mean> | <mean> | <pct>% |
| CLI: Get RG | <mean> | <mean> | <pct>% |
```

Note which benchmarks exceed 115%, while making clear this is indicative at 5 iterations and not a gate prediction.

- [ ] **Step 5: Remove the scratch worktree**

```powershell
cd C:\Users\jaysontai\Code\copilot-worktrees\cirrus\jaysontaimicrosoft-redesigned-waddle
git worktree remove --force ../azclips-baseline
```

`--force` is needed because the worktree has uncommitted count edits.

- [ ] **Step 6: Restore the counts in this worktree**

Revert both files to `[WarmupCount(5000)]` and `[IterationCount(5000)]`:

```powershell
git checkout -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
git diff -- tests/Benchmark/CLI/CLIBenchmarkBase.cs tests/Benchmark/PS/PSBenchmarkBase.cs
```

The diff must be empty. Confirm both files contain `5000` and neither contains `WarmupCount(5)` or `IterationCount(5)`.

- [ ] **Step 7: Record the results**

Write `docs/superpowers/plans/2026-08-13-benchmark-real-profile-io-results.md`. It must open with a clearly visible caveat that the run used 5 warmup + 5 measurement iterations, that the figures are indicative only, and that authoritative numbers come from CI at the committed `5000` counts. Then the comparison table, then the raw BenchmarkDotNet summary output for both runs.

```powershell
git add docs/superpowers/plans/2026-08-13-benchmark-real-profile-io-results.md
git commit -m "docs: record indicative benchmark delta from real profile file IO"
```

Verify `git status` shows no modification to the two benchmark base classes before committing.

---

## Definition of done

- [ ] `dotnet build azclips.slnx` succeeds.
- [ ] `dotnet test tests/Cli.Tests/Cli.Tests.csproj` passes, including 11 new `BenchmarkProfileStoreTests`.
- [ ] Benchmark smoke run exits 0 with a `[Benchmark] PASS` summary.
- [ ] Removing the seed call makes the run fail with the `VerifySeededAsync` message (guard verified, then reverted).
- [ ] `WarmupCount(5000)` and `IterationCount(5000)` are intact in both benchmark base classes.
- [ ] `StubProfileService` still exists and is still used by `Cli.Tests` and `Scenario.Tests`.
- [ ] No file under `src/` or `Agent/` is modified.
- [ ] Before/after benchmark deltas are recorded for the PR description, clearly labelled as indicative (5 warmup + 5 iterations, not the committed 5000).
