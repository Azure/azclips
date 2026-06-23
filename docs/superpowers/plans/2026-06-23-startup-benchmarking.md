# Startup-Time Benchmarking (CLI + PowerShell) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a permanent, CI-integrated benchmark that measures process-level **cold/warm startup time** of the bundled built-in extensions (`extension`, `login`, `config`) on both the `azv2` CLI and PowerShell surfaces, stopping at a resolve-only seam before any command executes.

**Architecture:** A ~3-line product seam (`CIRRUS_BENCH_RESOLVE_ONLY=1`) in `Core.Engine.ExecutionSession` makes both surfaces resolve the command processor and return *before* `ProcessAsync()`. New BenchmarkDotNet classes spawn the real published `azv2` / `pwsh` as fresh OS processes (`Process.Start` + `WaitForExit`) with that env var set; warm = hot OS page cache, cold = a per-iteration page-cache evictor. Results flow through the existing runner, gh-pages dashboards, and AppInsights export (extended with `Mode` / `Extension` dimensions). Warm is tracked-not-gated; cold is informational and best-effort.

**Tech Stack:** .NET 10 (`net10.0`), C#, BenchmarkDotNet, xUnit, PowerShell (`pwsh`), GitHub Actions, `benchmark-action/github-action-benchmark`, Application Insights.

---

## Key facts established during research (read before starting)

- **Seam location:** `src/Core/Core.Engine/ExecutionSession.cs`. Two code paths both call `await (await GetProcessor()).ProcessAsync();`:
  - `RunAsync()` (line 54-60) — CLI/async path.
  - `Run()` → local `ExecuteProcessorAsync()` (line 95-108) — PowerShell sync-over-async path.
  `GetProcessor()` (line 210-226) performs all startup work (context, extension assembly load, command-metadata resolve, processor init + arg bind). The seam returns between `GetProcessor()` and `ProcessAsync()`.
- **Env-var convention:** `Environment.GetEnvironmentVariable("CIRRUS_BENCH_RESOLVE_ONLY") == "1"`, mirroring `CIRRUS_NO_EXTENSION_CACHE` / `CIRRUS_AGENT_DISABLED` (see `src/CLI/Program.cs:228,261`).
- **Published `azv2` path:** `artifacts/<Config>/Core.Cli/azv2` (`azv2.exe` on Windows). Output layout fixed by `Shared.props:9` (`OutputPath = artifacts/<Config>/<ExtName>/`, CLI `ExtName=Core.Cli`).
- **Built-ins resolve from the artifacts tree (no inject step needed for benchmarks).** `BuiltinExtensionLoader.GetCandidateRoots` (`src/CLI/AssemblyLoading/ExtensionLoader.cs:65-78`) falls back to `AppContext.BaseDirectory/..` = `artifacts/<Config>/`, where each extension already builds to `artifacts/<Config>/Microsoft.Azure.Cirrus.Extensions.<Name>/`. **Verified empirically:** spawning `artifacts/Release/Core.Cli/azv2 config list`, `login show`, `extension list` each exit `0`.
- **Real probe commands (no required params → reach the seam):**
  - CLI: `config list`, `login show`, `extension list` (`src/Extensions/{Config,Login,Extension}/ExtensionIndex.cs`).
  - PS cmdlets: `Get-CirrusConfigList` (Config), `Get-CirrusAccount` (Login). Both have no required params.
- **PS modules import from staged manifests:** `build/Set-ModuleConfiguration.ps1 -Configuration <Config>` writes `artifacts/<Config>/PS/<Module>/<Module>.psd1` with absolute DLL paths. `Config.psd1` lists `Microsoft.Azure.Cirrus.Extensions.Config.dll` in `RequiredAssemblies` and `RequiredModules → Login`; `Login.psd1` lists `...Extensions.Login.dll`. Importing triggers `ModuleInitializer.OnImport → CirrusServiceProvider.Initialize(PowerShell)`.
- **Existing benchmark suite:** `tests/Benchmark/` — `Program.cs` (`--product` filter + `GetBenchmarkTypes`), `BenchmarkBase.cs`, `CLI/PS/API ResourceGroupBenchmarks.cs`, `ChartGenerator.cs` (`ChartDataPoint` record line 434; `ExtractChartData` line 52), `AppInsightsExporter.cs` (dimensions at line 55-59). `RepoLocator.GetRepoRoot()` (`tests/Common/Utilities/RepoLocator.cs`) finds repo root by scanning upward for `cirrus.slnx`.
- **Tests:** `dotnet test cirrus.slnx` runs every test project in the solution. CI `build-and-test.yml` builds/tests in **Debug** (Release has known extension-load issues for in-repo runs). `tests/Directory.Build.props` already provides xUnit + `Using Include="Xunit"`, so a new test csproj only needs `<ProjectReference>`s. The repo already uses `<InternalsVisibleTo Include="..." />` directly in csproj (e.g. `Core.Runtime.csproj:18`).
- **`ExecutionSession` is `internal`** with public ctor `ExecutionSession(ClientType, CommandInvocation, OutputStreams, CancellationToken=default)` and `internal` settable delegate properties wired by `Engine.cs:50-55`. `CommandMetadata.GetInstance()` (`src/Core/Core.Runtime/CommandLoadingExtension.cs:24`) does `Activator.CreateInstance(ProcessorType)` and requires a parameterless ctor + `IProcessor`. `OutputStreamDelegate` = `Task (object value, CancellationToken ct=default)` (`src/Core/Core.Abstractions/Runtime/delegates.cs:30`). `ClientType` ∈ `Core.Abstractions`.

---

## File structure

**Product code (1 file modified + 1 csproj):**
- Modify `src/Core/Core.Engine/ExecutionSession.cs` — add `_resolveOnly` field + two short-circuits.
- Modify `src/Core/Core.Engine/Core.Engine.csproj` — add `InternalsVisibleTo` for the new test project.

**Tests (1 new project, 2 files + slnx):**
- Create `tests/Core.Engine.Tests/Core.Engine.Tests.csproj`.
- Create `tests/Core.Engine.Tests/ExecutionSessionSeamTests.cs`.
- Modify `cirrus.slnx` — register the new test project.

**Benchmark code (6 new files + 2 modified):**
- Create `tests/Benchmark/Startup/StartupBenchmarkBase.cs` — paths, `ProcessStartInfo` builders, run helper.
- Create `tests/Benchmark/Startup/StartupEviction.cs` — page-cache evictor + capability probe.
- Create `tests/Benchmark/Startup/CliWarmStartupBenchmarks.cs`
- Create `tests/Benchmark/Startup/CliColdStartupBenchmarks.cs`
- Create `tests/Benchmark/Startup/PsWarmStartupBenchmarks.cs`
- Create `tests/Benchmark/Startup/PsColdStartupBenchmarks.cs`
- Modify `tests/Benchmark/Program.cs` — `--product STARTUP` + eviction-gated cold classes.
- Modify `tests/Benchmark/ChartGenerator.cs` + `tests/Benchmark/AppInsightsExporter.cs` — `Mode` / `Extension` dimensions.

**CI (1 modified):**
- Modify `.github/workflows/benchmark.yml` — STARTUP run + dashboard steps.

**Docs (1 modified, final, optional):**
- Modify `docs/superpowers/specs/2026-06-23-measuring-startup-time.md` — Status note (only if the user agrees).

---

## Task 1: Resolve-only seam in ExecutionSession + unit tests

**Files:**
- Modify: `src/Core/Core.Engine/ExecutionSession.cs`
- Modify: `src/Core/Core.Engine/Core.Engine.csproj`
- Create: `tests/Core.Engine.Tests/Core.Engine.Tests.csproj`
- Create: `tests/Core.Engine.Tests/ExecutionSessionSeamTests.cs`
- Modify: `cirrus.slnx`

- [ ] **Step 1: Add `InternalsVisibleTo` to Core.Engine**

In `src/Core/Core.Engine/Core.Engine.csproj`, add a new `ItemGroup` after the existing `PackageReference` ItemGroup (after line 10):

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="Core.Engine.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Create the test project file**

Create `tests/Core.Engine.Tests/Core.Engine.Tests.csproj` (framework/xUnit come from `tests/Directory.Build.props`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\src\Core\Core.Engine\Core.Engine.csproj" />
    <ProjectReference Include="..\..\src\Core\Core.Abstractions\Core.Abstractions.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Register the test project in the solution**

In `cirrus.slnx`, add this line immediately after the `tests/Core.Telemetry.Tests/...` entry (inside the `/tests/` folder block):

```xml
    <Project Path="tests/Core.Engine.Tests/Core.Engine.Tests.csproj" />
```

- [ ] **Step 4: Write the failing seam tests**

Create `tests/Core.Engine.Tests/ExecutionSessionSeamTests.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Core.Abstractions;
using Core.Engine;

namespace Core.Engine.Tests;

public class ExecutionSessionSeamTests
{
    private const string SeamEnvVar = "CIRRUS_BENCH_RESOLVE_ONLY";

    private sealed class SeamStubProcessor : IProcessor
    {
        public static bool WasExecuted;
        public IProcessor Init(ProcessorContext context) => this;
        public Task ProcessAsync()
        {
            WasExecuted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class SeamStubIndex : IExtensionIndex
    {
        private static readonly CommandMetadata Meta = new(
            Name: "SeamStub",
            Aliases: new List<string>(),
            FullName: typeof(SeamStubProcessor).FullName!,
            AssemblyName: typeof(SeamStubProcessor).Assembly.GetName().Name!,
            ProcessorType: typeof(SeamStubProcessor),
            Parameters: new Dictionary<string, ParameterMetadata>(),
            ParameterAliases: new Dictionary<string, string>());

        public CommandMetadata GetCommandMetadata(string name, ClientType clientType) => Meta;
        public IReadOnlyDictionary<string, CommandMetadata> GetCommandTable(ClientType clientType)
            => new Dictionary<string, CommandMetadata> { ["x"] = Meta };
        public IEnumerable<string> GetCommandKeys(ClientType clientType) => new[] { "x" };
        public IEnumerable<CommandMetadata> GetAllCommands(ClientType clientType) => new[] { Meta };
        public IReadOnlyList<string> GetVerbs() => new[] { "seam" };
    }

    private static ExecutionSession CreateSession(ClientType clientType)
    {
        OutputStreamDelegate noop = (_, _) => Task.CompletedTask;
        var streams = new OutputStreams(noop, noop, noop, noop, noop, noop);
        var invocation = new CommandInvocation("seam", "x", new Dictionary<string, object>(), IsTransient: false);

        var session = new ExecutionSession(clientType, invocation, streams)
        {
            GetExtensionVersion = () => string.Empty,
            LoadAssembly = (_, _) => throw new InvalidOperationException("LoadAssembly must not be called"),
            CacheGetOrAdd = (_, _, _) => new SeamStubIndex(),
            CreateProcessorContext = _ => Task.FromResult<ProcessorContext>(null!),
            BindProcessor = (p, _, _, _) => p,
        };
        return session;
    }

    [Fact]
    public async Task RunAsync_ShouldNotExecuteProcessor_WhenResolveOnlySet()
    {
        SeamStubProcessor.WasExecuted = false;
        Environment.SetEnvironmentVariable(SeamEnvVar, "1");
        try
        {
            using var session = CreateSession(ClientType.CLI);
            await session.RunAsync();
            Assert.False(SeamStubProcessor.WasExecuted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeamEnvVar, null);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldExecuteProcessor_WhenResolveOnlyNotSet()
    {
        SeamStubProcessor.WasExecuted = false;
        Environment.SetEnvironmentVariable(SeamEnvVar, null);
        using var session = CreateSession(ClientType.CLI);
        await session.RunAsync();
        Assert.True(SeamStubProcessor.WasExecuted);
    }

    [Fact]
    public void Run_ShouldNotExecuteProcessor_WhenResolveOnlySet()
    {
        SeamStubProcessor.WasExecuted = false;
        Environment.SetEnvironmentVariable(SeamEnvVar, "1");
        try
        {
            using var session = CreateSession(ClientType.PowerShell);
            session.Run();
            Assert.False(SeamStubProcessor.WasExecuted);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SeamEnvVar, null);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they fail**

Run: `dotnet test tests/Core.Engine.Tests/Core.Engine.Tests.csproj -c Debug --filter FullyQualifiedName~ExecutionSessionSeamTests`

Expected: `RunAsync_ShouldNotExecuteProcessor_WhenResolveOnlySet` and `Run_ShouldNotExecuteProcessor_WhenResolveOnlySet` **FAIL** (`Assert.False(WasExecuted)` fails because the seam does not exist yet, so `ProcessAsync` runs). `RunAsync_ShouldExecuteProcessor_WhenResolveOnlyNotSet` passes.

- [ ] **Step 6: Implement the seam — add the field and constructor read**

In `src/Core/Core.Engine/ExecutionSession.cs`, add the field next to `_disposed` (line 11):

```csharp
    private bool _disposed = false;
    private readonly bool _resolveOnly;
```

At the end of the constructor (after `loggingScope = ScopedLoggingService.UseScope(outputStreamAdapter);`, line 40), add:

```csharp
        loggingScope = ScopedLoggingService.UseScope(outputStreamAdapter);
        // Benchmark/test seam: when CIRRUS_BENCH_RESOLVE_ONLY=1, the session performs all
        // startup work (GetProcessor: runtime/DI/context init, extension assembly load,
        // command-metadata resolve, processor Init + arg bind) and returns WITHOUT executing
        // the command. Mirrors CIRRUS_NO_EXTENSION_CACHE / CIRRUS_AGENT_DISABLED. Read once per
        // session so the startup benchmark can measure up to the seam with zero network/auth.
        _resolveOnly = Environment.GetEnvironmentVariable("CIRRUS_BENCH_RESOLVE_ONLY") == "1";
```

- [ ] **Step 7: Implement the seam — short-circuit `RunAsync`**

Replace the body of `RunAsync()` (lines 54-60):

```csharp
    public async Task RunAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EventBus.Token.ThrowIfCancellationRequested();
        ProcessorOutputWriter = sessionOutputWriter;
        var processor = await GetProcessor();
        if (_resolveOnly) return;   // stop at the seam — startup is measured up to here
        await processor.ProcessAsync();
    }
```

- [ ] **Step 8: Implement the seam — short-circuit `ExecuteProcessorAsync` (PS path)**

In the local `ExecuteProcessorAsync()` inside `Run()` (lines 95-108), replace the `try` body so it stops at the seam while the `finally` still completes the channel:

```csharp
        async Task ExecuteProcessorAsync()
        {
            try
            {
                EventBus.Token.ThrowIfCancellationRequested();
                var processor = await GetProcessor();
                if (_resolveOnly) return;   // stop at the seam; finally still completes the channel
                await processor.ProcessAsync();
            }
            finally
            {
                // Publish completion to the reader — must run regardless of success,
                // failure, or cancellation to unblock the channel pump in Process().
                OutputChannel.Writer.Complete();
            }
        }
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/Core.Engine.Tests/Core.Engine.Tests.csproj -c Debug --filter FullyQualifiedName~ExecutionSessionSeamTests`

Expected: all 3 tests **PASS**.

- [ ] **Step 10: Verify nothing else broke**

Run: `dotnet build cirrus.slnx -c Debug`
Expected: `Build succeeded`, `0 Error(s)`.

- [ ] **Step 11: Commit**

```bash
git add src/Core/Core.Engine/ExecutionSession.cs src/Core/Core.Engine/Core.Engine.csproj tests/Core.Engine.Tests cirrus.slnx
git commit -m "feat(engine): add CIRRUS_BENCH_RESOLVE_ONLY startup seam

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 2: Startup benchmark harness base + page-cache evictor

**Files:**
- Create: `tests/Benchmark/Startup/StartupBenchmarkBase.cs`
- Create: `tests/Benchmark/Startup/StartupEviction.cs`

- [ ] **Step 1: Create the harness base**

Create `tests/Benchmark/Startup/StartupBenchmarkBase.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Tests.Common.Utilities;

namespace Benchmark.Startup;

/// <summary>
/// Shared setup for process-level startup benchmarks. Each [Benchmark] spawns the real
/// published azv2 / pwsh as a fresh OS process with CIRRUS_BENCH_RESOLVE_ONLY=1 so the child
/// stops at the resolve-only seam (startup work done, command not executed).
/// </summary>
public abstract class StartupBenchmarkBase
{
#if DEBUG
    protected const string Configuration = "Debug";
#else
    protected const string Configuration = "Release";
#endif

    protected string RepoRoot { get; private set; } = null!;

    /// <summary>Resolve repo root once (RepoLocator scans upward for cirrus.slnx).</summary>
    protected void InitializeCommon() => RepoRoot = RepoLocator.GetRepoRoot();

    private string Azv2Path()
    {
        var exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "azv2.exe" : "azv2";
        return Path.Combine(RepoRoot, "artifacts", Configuration, "Core.Cli", exe);
    }

    /// <summary>Absolute path to a staged PS manifest written by build/Set-ModuleConfiguration.ps1.</summary>
    protected string StagedModulePath(string module) =>
        Path.Combine(RepoRoot, "artifacts", Configuration, "PS", module, $"{module}.psd1");

    /// <summary>ProcessStartInfo for an azv2 invocation, stopping at the seam.</summary>
    protected ProcessStartInfo CliStartInfo(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Azv2Path(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.Environment["CIRRUS_BENCH_RESOLVE_ONLY"] = "1";
        return psi;
    }

    /// <summary>ProcessStartInfo for a pwsh module-import + cmdlet invocation, stopping at the seam.</summary>
    protected ProcessStartInfo PsStartInfo(string modulePsd1, string cmdlet)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "pwsh",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add($"Import-Module '{modulePsd1}'; {cmdlet} | Out-Null");
        psi.Environment["CIRRUS_BENCH_RESOLVE_ONLY"] = "1";
        return psi;
    }

    /// <summary>Start the process, drain its streams, wait for exit, and assert exit code 0.</summary>
    protected static void RunToExit(ProcessStartInfo psi)
    {
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException($"Process.Start returned null for {psi.FileName}");
        // Output at the seam is negligible; sequential drain before WaitForExit is safe here.
        _ = p.StandardOutput.ReadToEnd();
        _ = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Startup probe exited {p.ExitCode}: {psi.FileName} {string.Join(' ', psi.ArgumentList)}");
        }
    }
}
```

- [ ] **Step 2: Create the page-cache evictor + capability probe**

Create `tests/Benchmark/Startup/StartupEviction.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Benchmark.Startup;

/// <summary>
/// Best-effort OS page-cache eviction for cold-start measurement.
/// Linux (CI): sync + drop_caches via passwordless sudo. Windows (dev): EmptyStandbyList.exe.
/// When eviction is not available the cold benchmarks are skipped by Program.cs (never reported
/// as cold-but-actually-warm).
/// </summary>
public static class StartupEviction
{
    /// <summary>True if this machine can evict the OS page cache for cold runs.</summary>
    public static bool IsAvailable() => TryEvict(probe: true);

    /// <summary>Evict the OS page cache. Logs and continues if eviction is unavailable.</summary>
    public static void Evict()
    {
        if (!TryEvict(probe: false))
        {
            Console.WriteLine("[Startup] cold eviction unavailable at runtime — launch was NOT cold");
        }
    }

    private static bool TryEvict(bool probe)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (!File.Exists("/proc/sys/vm/drop_caches")) return false;
                if (probe) return true;
                return RunBash("sync; echo 3 | sudo tee /proc/sys/vm/drop_caches > /dev/null");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var tool = FindTool("EmptyStandbyList.exe");
                if (tool is null) return false;
                if (probe) return true;
                return Run(tool, "standbylist");
            }

            return false; // macOS / other — not part of the supported cold matrix
        }
        catch
        {
            return false;
        }
    }

    private static string? FindTool(string fileName)
    {
        // 1) next to the benchmark binary (repo tooling), 2) PATH
        var local = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(local)) return local;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static bool RunBash(string script) => Run("/bin/bash", $"-c \"{script}\"");

    private static bool Run(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi);
        if (p is null) return false;
        _ = p.StandardOutput.ReadToEnd();
        _ = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
```

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build tests/Benchmark/Benchmark.csproj -c Debug`
Expected: `Build succeeded`, `0 Error(s)`. (No benchmarks reference these yet; this just checks the harness compiles.)

- [ ] **Step 4: Commit**

```bash
git add tests/Benchmark/Startup/StartupBenchmarkBase.cs tests/Benchmark/Startup/StartupEviction.cs
git commit -m "feat(benchmark): add startup harness base and page-cache evictor

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 3: CLI + PowerShell startup benchmark classes (warm + cold)

**Files:**
- Create: `tests/Benchmark/Startup/CliWarmStartupBenchmarks.cs`
- Create: `tests/Benchmark/Startup/CliColdStartupBenchmarks.cs`
- Create: `tests/Benchmark/Startup/PsWarmStartupBenchmarks.cs`
- Create: `tests/Benchmark/Startup/PsColdStartupBenchmarks.cs`

> Class names encode the surface (`Cli`/`Ps`) and mode (`Warm`/`Cold`); `[Benchmark(Description=...)]` encodes the extension (`config`/`login`/`extension`). Task 5 parses these for the `Product` / `Mode` / `Extension` telemetry dimensions, so do not rename them without updating Task 5.

- [ ] **Step 1: CLI warm class**

Create `tests/Benchmark/Startup/CliWarmStartupBenchmarks.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// CLI warm startup: fresh azv2 process per launch, OS page cache hot (BDN warmup primes it).
/// Stops at the resolve-only seam. Measures process create + runtime init + DI + extension load
/// + command resolve for each built-in.
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 10)]
public class CliWarmStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(CliStartInfo("config", "list"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(CliStartInfo("login", "show"));

    [Benchmark(Description = "extension")]
    public void Extension() => RunToExit(CliStartInfo("extension", "list"));
}
```

- [ ] **Step 2: CLI cold class**

Create `tests/Benchmark/Startup/CliColdStartupBenchmarks.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// CLI cold startup: OS page cache evicted before each launch, so DLLs are re-read from disk
/// (the "first run after reboot" case). Only registered by Program.cs when eviction is available.
/// </summary>
[SimpleJob(RunStrategy.ColdStart, iterationCount: 10)]
public class CliColdStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [IterationSetup]
    public void Evict() => StartupEviction.Evict();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(CliStartInfo("config", "list"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(CliStartInfo("login", "show"));

    [Benchmark(Description = "extension")]
    public void Extension() => RunToExit(CliStartInfo("extension", "list"));
}
```

- [ ] **Step 3: PS warm class**

Create `tests/Benchmark/Startup/PsWarmStartupBenchmarks.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// PowerShell warm startup: fresh pwsh process, Import-Module (triggers
/// CirrusServiceProvider.Initialize + eager RequiredAssemblies load) + first cmdlet, OS page
/// cache hot. Config imports Login transitively (RequiredModules). Stops at the resolve-only seam.
/// </summary>
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 10)]
public class PsWarmStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(PsStartInfo(StagedModulePath("Config"), "Get-CirrusConfigList"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(PsStartInfo(StagedModulePath("Login"), "Get-CirrusAccount"));
}
```

- [ ] **Step 4: PS cold class**

Create `tests/Benchmark/Startup/PsColdStartupBenchmarks.cs`:

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Benchmark.Startup;

/// <summary>
/// PowerShell cold startup: OS page cache evicted before each launch. Only registered by
/// Program.cs when eviction is available.
/// </summary>
[SimpleJob(RunStrategy.ColdStart, iterationCount: 10)]
public class PsColdStartupBenchmarks : StartupBenchmarkBase
{
    [GlobalSetup]
    public void Setup() => InitializeCommon();

    [IterationSetup]
    public void Evict() => StartupEviction.Evict();

    [Benchmark(Description = "config")]
    public void Config() => RunToExit(PsStartInfo(StagedModulePath("Config"), "Get-CirrusConfigList"));

    [Benchmark(Description = "login")]
    public void Login() => RunToExit(PsStartInfo(StagedModulePath("Login"), "Get-CirrusAccount"));
}
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build tests/Benchmark/Benchmark.csproj -c Debug`
Expected: `Build succeeded`, `0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add tests/Benchmark/Startup/CliWarmStartupBenchmarks.cs tests/Benchmark/Startup/CliColdStartupBenchmarks.cs tests/Benchmark/Startup/PsWarmStartupBenchmarks.cs tests/Benchmark/Startup/PsColdStartupBenchmarks.cs
git commit -m "feat(benchmark): add CLI and PS startup benchmark classes

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 4: Wire `--product STARTUP` into the runner + end-to-end smoke

**Files:**
- Modify: `tests/Benchmark/Program.cs`

- [ ] **Step 1: Add a STARTUP branch to `GetBenchmarkTypes`**

In `tests/Benchmark/Program.cs`, replace the `product switch` block inside `GetBenchmarkTypes` (lines 184-190) with:

```csharp
        return product switch
        {
            "API" => new[] { typeof(API.ResourceGroupBenchmarks) },
            "CLI" => new[] { typeof(CLI.ResourceGroupBenchmarks) },
            "PS" => new[] { typeof(PS.ResourceGroupBenchmarks) },
            "STARTUP" => GetStartupBenchmarkTypes(),
            _ => throw new ArgumentException($"Unknown product: {product}. Use: API, CLI, PS, STARTUP, or ALL")
        };
```

- [ ] **Step 2: Add the eviction-gated STARTUP type selector**

In `tests/Benchmark/Program.cs`, add this method directly after `GetBenchmarkTypes` (after line 191):

```csharp
    /// <summary>
    /// Startup benchmark classes. Warm always runs; cold runs only when OS page-cache eviction
    /// is available on this machine (otherwise it is skipped with a log — never reported as
    /// cold-but-actually-warm).
    /// </summary>
    private static Type[] GetStartupBenchmarkTypes()
    {
        var types = new List<Type>
        {
            typeof(Startup.CliWarmStartupBenchmarks),
            typeof(Startup.PsWarmStartupBenchmarks),
        };

        if (Startup.StartupEviction.IsAvailable())
        {
            types.Add(typeof(Startup.CliColdStartupBenchmarks));
            types.Add(typeof(Startup.PsColdStartupBenchmarks));
        }
        else
        {
            Console.WriteLine("[Startup] cold eviction unavailable — skipping cold startup benchmarks");
        }

        return types.ToArray();
    }
```

- [ ] **Step 3: Build the solution in Release**

Run: `dotnet build cirrus.slnx -c Release`
Expected: `Build succeeded`, `0 Error(s)`.

- [ ] **Step 4: Stage the PS manifests (Release)**

Run: `pwsh -NoProfile -File build/Set-ModuleConfiguration.ps1 -Configuration Release`
Expected: lines like `Staged Config/Config.psd1 -> ...artifacts/Release/PS/Config/Config.psd1` and the same for Login.

- [ ] **Step 5: Manual seam sanity — confirm a spawned probe stops at the seam (exit 0, no execution)**

Run (Windows PowerShell):
```powershell
$env:CIRRUS_BENCH_RESOLVE_ONLY = "1"
& artifacts/Release/Core.Cli/azv2.exe config list; "exit=$LASTEXITCODE"
Remove-Item Env:CIRRUS_BENCH_RESOLVE_ONLY
```
Expected: **no config output**, `exit=0` (the seam returns before `ListConfigCommand.ProcessAsync` runs). Contrast: without the env var, `config list` prints config rows.

- [ ] **Step 6: Smoke-run the STARTUP benchmarks locally**

Run: `dotnet run --project tests/Benchmark -c Release --no-build -- --local --product STARTUP`
Expected: BDN runs `CliWarmStartupBenchmarks` (config/login/extension) and `PsWarmStartupBenchmarks` (config/login). On Windows without `EmptyStandbyList.exe` you will see `[Startup] cold eviction unavailable — skipping cold startup benchmarks` and cold classes are skipped. Final line: `[Benchmark] PASS`. Artifacts written under `artifacts/Benchmark/<timestamp>/`.

- [ ] **Step 7: Commit**

```bash
git add tests/Benchmark/Program.cs
git commit -m "feat(benchmark): wire --product STARTUP with eviction-gated cold runs

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 5: Add `Mode` / `Extension` telemetry dimensions

**Files:**
- Modify: `tests/Benchmark/ChartGenerator.cs`
- Modify: `tests/Benchmark/AppInsightsExporter.cs`

- [ ] **Step 1: Extend the `ChartDataPoint` record**

In `tests/Benchmark/ChartGenerator.cs`, replace the record definition (line 434):

```csharp
public record ChartDataPoint(string Method, string Product, double MeanSeconds, string Mode = "", string Extension = "");
```

- [ ] **Step 2: Populate `Product` / `Mode` / `Extension` for startup classes in `ExtractChartData`**

In `tests/Benchmark/ChartGenerator.cs`, replace the body of the inner `foreach (var report in summary.Reports)` loop (lines 60-73) with:

```csharp
            foreach (var report in summary.Reports)
            {
                if (report?.ResultStatistics == null) continue;

                var methodName = report.BenchmarkCase.Descriptor.WorkloadMethodDisplayInfo;
                var fullName = report.BenchmarkCase.Descriptor.Type.FullName ?? "";
                var meanSeconds = report.ResultStatistics.Mean / 1_000_000_000.0;

                string product;
                string mode = "";
                string extension = "";
                if (fullName.Contains(".Startup."))
                {
                    product = fullName.Contains("Ps") ? "STARTUP-PS" : "STARTUP-CLI";
                    mode = fullName.Contains("Cold") ? "cold" : "warm";
                    extension = methodName.ToLowerInvariant(); // [Benchmark(Description=...)] = config/login/extension
                }
                else
                {
                    product = fullName.Contains(".API.") ? "API" :
                              fullName.Contains(".CLI.") ? "CLI" :
                              fullName.Contains(".PS.") ? "PS" : "Other";
                }

                chartData.Add(new ChartDataPoint(methodName, product, meanSeconds, mode, extension));
            }
```

- [ ] **Step 3: Emit the new dimensions in `AppInsightsExporter.Export`**

In `tests/Benchmark/AppInsightsExporter.cs`, add the two optional dimensions to **both** telemetry items. After `metric.Properties["Machine"] = Environment.MachineName;` (line 60):

```csharp
            metric.Properties["Machine"] = Environment.MachineName;
            if (!string.IsNullOrEmpty(result.Mode)) metric.Properties["Mode"] = result.Mode;
            if (!string.IsNullOrEmpty(result.Extension)) metric.Properties["Extension"] = result.Extension;
```

And after `eventTelemetry.Properties["Timestamp"] = timestamp;` (line 70):

```csharp
            eventTelemetry.Properties["Timestamp"] = timestamp;
            if (!string.IsNullOrEmpty(result.Mode)) eventTelemetry.Properties["Mode"] = result.Mode;
            if (!string.IsNullOrEmpty(result.Extension)) eventTelemetry.Properties["Extension"] = result.Extension;
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build tests/Benchmark/Benchmark.csproj -c Debug`
Expected: `Build succeeded`, `0 Error(s)`. (Existing RG behavior is unchanged: `Mode`/`Extension` default to `""` and are not emitted for non-startup points.)

- [ ] **Step 5: Commit**

```bash
git add tests/Benchmark/ChartGenerator.cs tests/Benchmark/AppInsightsExporter.cs
git commit -m "feat(benchmark): tag startup results with Product/Mode/Extension dimensions

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 6: CI integration — STARTUP runs + dashboards (track-not-gate)

**Files:**
- Modify: `.github/workflows/benchmark.yml`

- [ ] **Step 1: Add the STARTUP run + dashboard steps**

In `.github/workflows/benchmark.yml`, insert the following block immediately after the PS publish step (after line 102, before the `# Upload benchmark results for fork PR` step at line 104). All new steps are siblings of the existing `- name:` steps under `steps:` (6-space indent):

```yaml
      # ─────────────── STARTUP ───────────────
      - name: Run STARTUP benchmarks (with retry)
        uses: nick-fields/retry@v3
        with:
          timeout_minutes: 20
          max_attempts: 2
          retry_wait_seconds: 30
          command: dotnet run --project tests/Benchmark -c Release --no-build -- --ci --product STARTUP

      - name: Publish CLI startup dashboard (warm, tracked)
        uses: benchmark-action/github-action-benchmark@v1
        with:
          name: 'Cirrus CLI Startup (warm)'
          tool: 'benchmarkdotnet'
          output-file-path: artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.CliWarmStartupBenchmarks-report-full-compressed.json
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: ${{ github.event_name == 'push' }}
          alert-threshold: '115%'
          fail-on-alert: false
          comment-on-alert: false
          summary-always: true
          gh-pages-branch: gh-pages
          benchmark-data-dir-path: dev/bench/startup-cli
          skip-fetch-gh-pages: true

      - name: Publish PS startup dashboard (warm, tracked)
        uses: benchmark-action/github-action-benchmark@v1
        with:
          name: 'Cirrus PS Startup (warm)'
          tool: 'benchmarkdotnet'
          output-file-path: artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.PsWarmStartupBenchmarks-report-full-compressed.json
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: ${{ github.event_name == 'push' }}
          alert-threshold: '115%'
          fail-on-alert: false
          comment-on-alert: false
          summary-always: true
          gh-pages-branch: gh-pages
          benchmark-data-dir-path: dev/bench/startup-ps
          skip-fetch-gh-pages: true

      - name: Publish CLI startup dashboard (cold, informational)
        if: ${{ hashFiles('artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.CliColdStartupBenchmarks-report-full-compressed.json') != '' }}
        uses: benchmark-action/github-action-benchmark@v1
        with:
          name: 'Cirrus CLI Startup (cold)'
          tool: 'benchmarkdotnet'
          output-file-path: artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.CliColdStartupBenchmarks-report-full-compressed.json
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: ${{ github.event_name == 'push' }}
          fail-on-alert: false
          comment-on-alert: false
          summary-always: true
          gh-pages-branch: gh-pages
          benchmark-data-dir-path: dev/bench/startup-cli
          skip-fetch-gh-pages: true

      - name: Publish PS startup dashboard (cold, informational)
        if: ${{ hashFiles('artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.PsColdStartupBenchmarks-report-full-compressed.json') != '' }}
        uses: benchmark-action/github-action-benchmark@v1
        with:
          name: 'Cirrus PS Startup (cold)'
          tool: 'benchmarkdotnet'
          output-file-path: artifacts/Benchmark/ci/STARTUP/results/Benchmark.Startup.PsColdStartupBenchmarks-report-full-compressed.json
          github-token: ${{ secrets.GITHUB_TOKEN }}
          auto-push: ${{ github.event_name == 'push' }}
          fail-on-alert: false
          comment-on-alert: false
          summary-always: true
          gh-pages-branch: gh-pages
          benchmark-data-dir-path: dev/bench/startup-ps
          skip-fetch-gh-pages: true
```

- [ ] **Step 2: Validate the workflow YAML**

Run: `pwsh -NoProfile -Command "Get-Content .github/workflows/benchmark.yml | Out-Null; 'yaml read ok'"`
Then eyeball indentation (all new steps 6-space indented under `steps:`). If `actionlint` is available, run `actionlint .github/workflows/benchmark.yml` and expect no errors.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/benchmark.yml
git commit -m "ci(benchmark): add STARTUP runs and startup-cli/startup-ps dashboards

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Task 7: Full verification + spec status

**Files:**
- Modify (optional, only with user agreement): `docs/superpowers/specs/2026-06-23-measuring-startup-time.md`

- [ ] **Step 1: Clean Release build**

Run: `dotnet build cirrus.slnx -c Release`
Expected: `Build succeeded`, `0 Error(s)`.

- [ ] **Step 2: Run the full test suite (Debug, as CI does)**

Run: `dotnet test cirrus.slnx -c Debug`
Expected: all tests pass, including the 3 `ExecutionSessionSeamTests`. No regressions in `Cli.Tests` / other projects.

- [ ] **Step 3: Stage PS manifests + smoke the full STARTUP product**

Run:
```
pwsh -NoProfile -File build/Set-ModuleConfiguration.ps1 -Configuration Release
dotnet run --project tests/Benchmark -c Release --no-build -- --local --product STARTUP
```
Expected: `[Benchmark] PASS`. Warm CLI (config/login/extension) + warm PS (config/login) measured. Cold runs if eviction is available, otherwise the skip log appears. A `benchmark-summary-<timestamp>.json` under `artifacts/Benchmark/<timestamp>/` contains the startup rows.

- [ ] **Step 4: Confirm existing products still work**

Run: `dotnet run --project tests/Benchmark -c Release --no-build -- --local --product CLI`
Expected: `[Benchmark] PASS` — the existing RG CLI benchmark is unaffected by the STARTUP additions.

- [ ] **Step 5: (Optional, with user agreement) Note implementation status in the alignment doc**

Only if the user agrees to flip status: in `docs/superpowers/specs/2026-06-23-measuring-startup-time.md`, change `### Status: Draft` to `### Status: Implemented` and commit. Otherwise leave the doc unchanged. (Setting team-facing X/Y targets in #47 from the first baseline is a follow-up, not part of this implementation.)

- [ ] **Step 6: Final commit (if the doc changed)**

```bash
git add docs/superpowers/specs/2026-06-23-measuring-startup-time.md
git commit -m "docs(startup): mark measuring-startup-time as implemented

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>"
```

---

## Self-review notes (spec coverage)

- **Seam (spec §5):** Task 1 — `CIRRUS_BENCH_RESOLVE_ONLY` in both `RunAsync` and `ExecuteProcessorAsync`, read once, exit 0 / no output / no network; XML-doc comment included.
- **Approach A process-spawn (spec §3):** Tasks 2-4 — `Process.Start` of real `azv2` / `pwsh`, `WaitForExit`, assert exit 0.
- **Matrix = 10 (spec §4):** Task 3 — CLI {config, login, extension} + PS {config, login}, each warm + cold.
- **Cold/warm (spec §2, §6):** warm = `RunStrategy.Monitoring` hot cache; cold = `RunStrategy.ColdStart` + `[IterationSetup]` eviction; Linux `drop_caches` / Windows `EmptyStandbyList`; capability probe logs-and-skips (Task 2 + Task 4 gating).
- **Probe commands:** real, param-free commands verified to reach the seam (`config list`, `login show`, `extension list`, `Get-CirrusConfigList`, `Get-CirrusAccount`).
- **Runner wiring (spec §6):** Task 4 — `--product STARTUP`.
- **Telemetry/dashboards (spec §7, §9):** Task 5 (Mode/Extension/STARTUP product) + Task 6 (`fail-on-alert: false` AND `comment-on-alert: false` for warm; cold informational + `if hashFiles` guard; `dev/bench/startup-cli` + `dev/bench/startup-ps`).
- **Testing (spec §8):** Task 1 unit tests (both paths); Task 4 manual seam sanity; Task 7 smoke + regression guard.
- **Out of scope (spec §10):** no PR gating, no core-only baseline, no R2R/AOT, no PS `extension` module — none added.
