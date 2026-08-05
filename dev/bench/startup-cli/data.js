window.BENCHMARK_DATA = {
  "lastUpdate": 1785904915240,
  "repoUrl": "https://github.com/Azure/azclips",
  "entries": {
    "Azclips CLI Startup (warm)": [
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "962b6bc594aeb8b1a6e7e2cc27b47235f3119c72",
          "message": "[Benchmark] Benchmark start up time (#401)\n\n## What\nMeasures how long `az` and `pwsh` take to start up (process-create →\ncommand ready),\nseparately from command execution. Adds warm/cold benchmarks, richer\nreporting, and\nCI dashboards to track startup regressions.\n\nLoop for context: [Measuring start up\ntime](https://microsoft.sharepoint.com/:fl:/g/contentstorage/CSP_2e24e7fe-1ac4-440f-bfce-2f4be2ac6e50/IQDPWhjRrPT5QpMalKvnTZ5hAWWJkEXTX3Pw5_zDdkRmRt8?e=OCVdLa&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF8yZTI0ZTdmZS0xYWM0LTQ0MGYtYmZjZS0yZjRiZTJhYzZlNTAmZD1iJTIxX3Vja0xzUWFEMFNfemk5TDRxeHVVQzFBYVZFbklmeE11dWIyS1dMa0RwWkJNRjBzMThzbVJZMTQwM01YclJWUSZmPTAxWTZETVdLNlBMSU1ORExIVTdGQkpHR1VVVlBUVTNIVEImYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lcg%3D%3D)\n\n## How it works\nA new `ProcessorAcquired` engine event fires once the command is\nresolved but before\nit runs. When `AZCLIPS_BENCH_RESOLVE_ONLY=true`, the engine exits right\nthere — timing\nstartup only, with no network or auth side effects. The flag defaults\noff, so\nproduction behavior is unchanged.\n\n## Changes\n- **Engine seam**: `ProcessorAcquired` event +\n`BenchmarkOptions.ResolveOnly`, wired\nvia config and the `AZCLIPS_BENCH_RESOLVE_ONLY` env var.\n`Environment.Exit(0)` only\n  when the flag is set.\n- **Harness** (`tests/Benchmark/Startup/`): spawns real `az`/`pwsh` per\niteration for\n  the `config`, `login`, and `extension` built-ins.\n  - Warm — page cache hot.\n- Cold — page cache evicted first (Linux `drop_caches`; Windows\nstandby-list purge,\nneeds an elevated terminal). Cold is skipped with a reason when eviction\nisn't\n    available, never faked as cold.\n- **Reporting**: adds `Mode` (warm/cold) + `Extension` dimensions to\ncharts and\n  App Insights; warm/cold shown in separate sections.\n- **CI**: new `--product STARTUP` step publishing four dashboards — warm\n(tracked,\n  115% alert) and cold (informational).\n- **Tests**: new `Core.Engine.Tests` verifying `ProcessorAcquired` fires\nonce, before\n  execution, in both CLI and PowerShell paths.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-10T15:16:33+10:00",
          "tree_id": "345b6594b7ab67f144d89b8775255a2ab69203c0",
          "url": "https://github.com/Azure/azclips/commit/962b6bc594aeb8b1a6e7e2cc27b47235f3119c72"
        },
        "date": 1783661226494,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 218073676.09,
            "unit": "ns",
            "range": "± 3232297.14456742"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 215791647.77,
            "unit": "ns",
            "range": "± 5859087.275422695"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 214230163.46,
            "unit": "ns",
            "range": "± 2799592.819666783"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "711696cf8d7ac741290034c73f46b6844a53171e",
          "message": "feat(agent): prettier interactive tool-event rendering via Spectre.Console (#435)\n\n## What\n\nRender the **interactive** agent's tool events through\n**Spectre.Console** so long commands, args, and output wrap and indent\nto the terminal — instead of being crammed onto one line or hard-cut\nmid-token.\n\nBefore, in interactive mode:\n- `▶ powershell({...huge JSON...})` — the entire args JSON on one line\n- `✓ powershell 10025ms · <output>` — output's first line crammed after\nthe `·`\n- `⚠ approve args:` — long commands sliced by byte budget, cutting\nwords/tokens in half\n- long tool output truncated to the **last 20 lines** (dropping the\nhead)\n\n## Changes\n\n- **Tool call/complete rendering** (`ConsoleAgentRenderer`): tool name\non its own line; args printed as indented `key: value` pairs (parsed\nfrom args JSON); output indented and word-wrapped to terminal width.\n- **`ConsoleApprovalPrompt.WriteArgs`**: wrap long commands at\nwhitespace instead of slicing by byte budget, so flags / quoted values\nstay intact.\n- **`SanitizeToolResult`**: drop the 4000-char / last-20-lines\ntruncation. It is **display-only** (never fed to the model — that path\nuses the SDK's raw tool result) and the terminal has scrollback, so the\nold cap only risked dropping the **head** of a long result. ANSI /\noverwrite-run / adjacent-dedupe cleanup is kept.\n- **New config `agent.interactive.verbose`** (bool, default `false`):\nwhen on, the interactive session also shows non-shell tool details\n(`get_command` / `find_*` args and output); off keeps the session\nuncluttered. Documented in `docs/howto/config.md`.\n\n## Scope / non-goals\n\n- **Plan rendering (`RenderPlan`) is unchanged** — it's a flat list that\nneeds no layout engine.\n- **one-shot mode is unaffected** — it prints no tool events (`quiet`),\nso verbose is interactive-only by design (hence the `agent.interactive.`\nnamespace).\n\n## Dependency\n\nAdds `Spectre.Console` 0.57.2 (MIT). Only the core rendering package (+\nits `Spectre.Console.Ansi`); **not** `Spectre.Console.Cli` (not\nAOT-safe) or `.ImageSharp`. Self-contained publish verified clean (no\ntrim/AOT warnings under `TreatWarningsAsErrors`); ~1.1 MB added DLLs.\n\n## Testing\n\n- `Core.Agent.Tests`: 168 passed\n- `Cli.Tests` (excl. `ManagedIdentityE2E`): 405 passed\n- Manually verified in a real terminal: long output shows in full (no\ntruncation), args/output wrap and indent, `verbose` on/off toggles\nnon-shell tool detail.",
          "timestamp": "2026-07-10T16:53:52+10:00",
          "tree_id": "a9d139fbe5e6a60420fa23a024cb4efff1d44545",
          "url": "https://github.com/Azure/azclips/commit/711696cf8d7ac741290034c73f46b6844a53171e"
        },
        "date": 1783667047561,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 216796631.45,
            "unit": "ns",
            "range": "± 3866003.6619004756"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 217668516.01,
            "unit": "ns",
            "range": "± 4815060.017489887"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 214779891.42,
            "unit": "ns",
            "range": "± 2682303.74703395"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "11f4c58369c804f845ef62eb8f3975f4b32f0d24",
          "message": "fix(packaging): agent DLL casing + executable bit on Linux/macOS (#436)\n\n## What\n\nLinux/macOS-only packaging bugs that stopped the agent from launching.\nInvisible on Windows (NTFS is case-insensitive), so they only surfaced\nwhen a teammate tested on Linux.\n\n## Root cause\n\nThe agent's `Azclips.Agent.Tools.dll` is a **project reference** that is\n**agent-only** (unlike the other `Core.*` project refs, it has no\nbyte-identical copy in the CLI publish root). Two different loaders\nresolve it from two different flat directories:\n\n- **agent subprocess** (`az <failing-cmd>` fallback): hostfxr, app base\n= CLI publish root. Every other project ref already has a copy there;\n`Azclips.Agent.Tools` did not → the subprocess crashed with `assembly\nnot found in azclips-agent.deps.json`. (The `deps/` probing path does\n**not** help project refs — it only carries NuGet packages.)\n- **`az agent tool ...`**: the CLI's custom extension\n`AssemblyLoadContext`, which only looks flat at the extension top level.\n\n`stage_agent` had also blindly lowercased **every** dep folder name,\nwhich additionally broke the case-sensitive `deps/` layout for project\nrefs on Linux/macOS.\n\n## Fix\n\nIn `stage_agent.sh` / `stage_agent.ps1` (kept in sync):\n\n1. **Casing**: derive each dep's `deps/` subfolder from\n`libraries[].path` when present (NuGet, already lowercased), else the\n**original-case** library id (project refs). No more blanket `.lower()`.\n2. **Placement**: for each agent-only project ref (one the CLI root\nlacks), copy it into **both** the CLI publish root (for the agent\nsubprocess's app-base probe) **and** the extension top level (for the\nCLI extension loader), then still move the original under `deps/`. The\nstep-6 cleanup is exempted for these flat copies so it doesn't delete\nthem as `deps/`-redundant.\n\n**Note on executable bit**: an earlier revision added a `chmod +x` step\nto the tarball scripts. It was removed — verified against the shipped\nv0.0.4 release (built on native Linux/macOS CI agents) that `az` /\n`azclips-agent` / `copilot` / `.so` / `.dylib` already ship `0755`;\n`dotnet publish` sets the apphost executable on Linux/macOS and `tar`\npreserves it. The missing `+x` only appeared in local Windows/NTFS\ncross-compiles, which is never the release path.\n\n## Files\n\n- `devops/scripts/packaging/common/stage_agent.sh` / `stage_agent.ps1` —\ncasing + dual-placement of agent-only project refs (kept in sync)\n- `Agent/tests/Core.Agent.Tests/AgentToolsLiveTests.cs` — new\nSkip-by-default live smoke test (see Testing)\n\n## Testing\n\n- Rebuilt the linux-x64 and win-x64 full packages; confirmed\n`Azclips.Agent.Tools.dll` lands in all three needed spots (CLI root,\nextension top level, `deps/Azclips.Agent.Tools/1.0.0/`) while NuGet\npackages stay lowercase under `deps/`.\n- **Windows and WSL Ubuntu 24.04**, both load paths verified after\ninstall: `az agent tool env` (CLI extension loader) returns the env\nJSON, and `az profile show` (one-shot agent subprocess) reaches its\nnormal AI-help output — previously both crashed on the missing DLL.\n- Added `AgentToolsLiveTests`: a `Skip`-by-default live smoke test that,\nagainst an installed package, exercises **both** load paths — the seven\n`agent tool` commands and a one-shot `profile show` — so a \"fixed one\nloader, broke the other\" regression is caught by one test after install.\nIt snapshots/restores `agent.enabled` + `agent.sessionStyle` so it\nleaves the installed config unchanged.",
          "timestamp": "2026-07-13T10:06:38+10:00",
          "tree_id": "7b22b0e19616a26f5338367dacd25148180e9e0d",
          "url": "https://github.com/Azure/azclips/commit/11f4c58369c804f845ef62eb8f3975f4b32f0d24"
        },
        "date": 1783901808025,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 233810038.18,
            "unit": "ns",
            "range": "± 5419561.657684888"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 232595754.87,
            "unit": "ns",
            "range": "± 4703847.962358627"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 231510448.49,
            "unit": "ns",
            "range": "± 3836884.0343969623"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "xumingk1@gmail.com",
            "name": "xuming-ms",
            "username": "xuming-ms"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "da6a74ad8be4ef88d67a284a335f15371241d538",
          "message": "Tenant resolution logic for auth flow (#437)\n\n## Summary\nResolves the tenant that actually owns the requested subscription during\n`login --subscription-id` (without `--tenant`), instead of persisting\nthe signed-in home tenant.\n\nAlso use the temp directory for MI e2e test instead of the developer's\nreal profile storage\n\n## Behavior\n- On a fresh interactive/device-code login with `--subscription-id` and\nno `--tenant`, the owning tenant is resolved from the subscription (via\nthe ARM 401 challenge) and persisted to the profile.\n- If the owning tenant cannot be determined, the login fails **without**\nsaving a profile (avoids persisting an unverified tenant).\n- For a cross-tenant subscription, the owning tenant's access is\nverified with a read-only silent-token probe **before** saving. If\ninteractive sign-in is required (e.g. MFA / conditional access) or the\nprobe fails, the login errors and the profile is **not** saved.\n\n## Tests\n- New `SubscriptionServiceTenantResolutionTests` covering the ARM\nchallenge parsing paths.\n\n---------\n\nCo-authored-by: Ming Xu <xumi@microsoft.com>",
          "timestamp": "2026-07-13T10:43:49+10:00",
          "tree_id": "ee181fbeba9410a5730810edab5d579e18477555",
          "url": "https://github.com/Azure/azclips/commit/da6a74ad8be4ef88d67a284a335f15371241d538"
        },
        "date": 1783904090377,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 232287805.93,
            "unit": "ns",
            "range": "± 4945689.6603823565"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 231133775.16,
            "unit": "ns",
            "range": "± 4530518.587077736"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 233361918.34,
            "unit": "ns",
            "range": "± 4463455.368802122"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8ebd7f0968a39d333b19bc9d4cf74588c72c31b5",
          "message": "Trigger stable release 0.0.5 (#441)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.5 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.5.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-13T10:47:53+10:00",
          "tree_id": "120893bf33fcd12bcd32e164f0d9dbfc1a42fb91",
          "url": "https://github.com/Azure/azclips/commit/8ebd7f0968a39d333b19bc9d4cf74588c72c31b5"
        },
        "date": 1783904306636,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 214272840.16,
            "unit": "ns",
            "range": "± 4342713.703129871"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 215658105.92,
            "unit": "ns",
            "range": "± 5427501.0112745715"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 213691299.48,
            "unit": "ns",
            "range": "± 4605606.162082393"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "msarfraz@microsoft.com",
            "name": "mansoor sarfraz",
            "username": "msarfraz"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d93ef16042c9bbafaff05e7058c1022f26c4ac9d",
          "message": "{Core} extensions Dir added in version output (#421)\n\n**Description**\n\n- extensions Dir added in version output\n- version intrinsic verb handling improved for future use\n\n---------\n\nCo-authored-by: Mansoor Sarfraz <msarfraz+microsoft@microsoft.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-13T14:23:45+10:00",
          "tree_id": "1db298bd21f5edc5d784030ace14e3839c450cfe",
          "url": "https://github.com/Azure/azclips/commit/d93ef16042c9bbafaff05e7058c1022f26c4ac9d"
        },
        "date": 1783917243691,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 213564612.86,
            "unit": "ns",
            "range": "± 2702403.6229167455"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 214633207.92,
            "unit": "ns",
            "range": "± 2419668.760209762"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 212435470.06,
            "unit": "ns",
            "range": "± 2638977.9141988535"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3d7c72ee31fdde25b8b15c2f3442d292ab8262b3",
          "message": "Rename Compute/Resources PowerShell cmdlets to Az-prefixed Azure Powe… (#446)\n\nAlign extension PowerShell command names with Azure PowerShell.\n\n## Renamed cmdlets\n- **Compute:** `Get-VM`/`New-VM`/`Remove-VM` →\n`Get-AzVM`/`New-AzVM`/`Remove-AzVM`\n- **Resources:** `New-ResourceGroup`/`Get-ResourceGroup` →\n`New-AzResourceGroup`/`Get-AzResourceGroup`;\n`New-ResourceGroupDeployment`/`New-ResourceGroupDeploymentWhatIf` →\nAz-prefixed; `Invoke-AzclipsRestMethod` → `Invoke-AzRestMethod`\n\n## Compute network cmdlets — removed, not renamed\n`New-VirtualNetwork` / `New-NetworkInterface` /\n`New-NetworkSecurityGroup` are **not** part of the Compute PowerShell\nsurface. Their dispatch was never wired up (the `s_psTable` keys were\ncommented out), so they were removed rather than renamed/exported:\n- Deleted the cmdlet classes, their PS `CommandMetadata`/commented\ndispatch entries, and their help files\n- Dropped them from `Compute.psd1` `CmdletsToExport`\n\nThey will live in a future **Network** extension (mirroring\n`Az.Network`). Networking is still created internally by the `New-AzVM`\norchestrator, so the underlying network **processors** are retained.\n\n## Scope of edits\nCmdlet `[Cmdlet]` nouns + invocation strings, `ExtensionIndex`\nInvocationNames + dispatch-table keys, `.psd1` `CmdletsToExport`,\n`exports/*.ps1` proxy functions (renamed), `help/*.md` (renamed +\ncontent), tests, benchmarks, example scripts, and docs.",
          "timestamp": "2026-07-13T15:44:30+10:00",
          "tree_id": "e1db67c70e25c7ae47c2ed7d30ca7468ddfdd8f8",
          "url": "https://github.com/Azure/azclips/commit/3d7c72ee31fdde25b8b15c2f3442d292ab8262b3"
        },
        "date": 1783922097436,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 216827181.45,
            "unit": "ns",
            "range": "± 2931641.411113388"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 217979545.15,
            "unit": "ns",
            "range": "± 3527571.2685140823"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 216216764.62,
            "unit": "ns",
            "range": "± 3055528.869974422"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "tritorto@gmail.com",
            "name": "Teresa Ritorto",
            "username": "teresaritorto"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "0520c13a51f790f98853c2931495b2d1eab24b79",
          "message": "Feature/telemetry tags (#438)\n\nApplied several fixes to telemetry tagging\n- Included missing instalationId in allowlist\n- Re-added TenantId, SubscriptionId and authType (removed from\nAuthContextService as part of the profile management work).",
          "timestamp": "2026-07-13T16:05:19+10:00",
          "tree_id": "e555a57ba0545160db476ad6c58d8f6f4cb233ff",
          "url": "https://github.com/Azure/azclips/commit/0520c13a51f790f98853c2931495b2d1eab24b79"
        },
        "date": 1783923405150,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 211940144.7,
            "unit": "ns",
            "range": "± 4125825.5759269143"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 212252499.41,
            "unit": "ns",
            "range": "± 4478033.709675354"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 211148867.34,
            "unit": "ns",
            "range": "± 2162818.9410918383"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "68524fbf95541d9e3f81aed3d92e433102613a80",
          "message": "{Help} Handle `help` for nested sub groups (#450)\n\nFixes [Azure/CLIPS#504](https://github.com/Azure/CLIPS/issues/504): `az\ndeployment group --help` (and any intermediate sub-group) rendered a\nblank summary because only verb-root and leaf files had a home in the\ncorpus.\n\n**Structural fix, not a bandage.** CLI help markdown moves from flat\nkebab (`deployment/group-create.md`) to nested directories\n(`deployment/group/create.md` + `deployment/group/index.md`). The\nfilesystem now mirrors the command tree 1:1, with `index.md` at every\ngroup node. Intermediate-group summary falls out of the same `index.md`\nrule that already covers verb roots. `HelpCompletenessValidator`\nrequires `index.md` for every prefix of every routed command key, so the\nblank-summary regression cannot come back silently.\n\nTouches:\n\n- Runtime: `MarkdownCommandHelpProvider.ResolveHelpFilePath` (nested\nprobing: leaf `<path>.md` first, then `<path>/index.md`),\n`ExtensionIndexHelpAdapter` (drop hard-coded empty summaries; source\nfrom markdown at every level).\n- Tool: `HelpCommandKey` (path-style filenames),\n`HelpCompletenessValidator` (require intermediate `index.md`),\n`Scaffolder` (emit nested stubs).\n- Corpus: 9 leaves renamed via `git mv` (blame preserved); 3 new\nintermediate `index.md` files (`deployment/group/`, `agent/tool/`,\n`agent/tool/resource/`).\n- Docs: spec + how-to + wiki updated to describe the nested authoring\nmodel.\n- Tests added for path resolution, adapter summary population,\npath-style filenames, and nested tree completeness.\n\nVerification: `dotnet build azclips.slnx` clean; `build.ps1\n-GenerateHelpArtifacts` reports no issues (11 manifests); `Cli.Tests`\n407/407 non-E2E passed; `Azclips.Help.Tool.Tests` 174/174 passed;\nlive-checked `az deployment group --help` and `az agent tool resource\n--help`.",
          "timestamp": "2026-07-14T16:13:02+10:00",
          "tree_id": "e54cebed21ba091864451225d3ee36b2c18e422f",
          "url": "https://github.com/Azure/azclips/commit/68524fbf95541d9e3f81aed3d92e433102613a80"
        },
        "date": 1784010452374,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 211644092.5,
            "unit": "ns",
            "range": "± 2482855.004676386"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 212712854.07,
            "unit": "ns",
            "range": "± 4653863.359507176"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 211682603.49,
            "unit": "ns",
            "range": "± 3147208.981366671"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e9c54603958428a4c363e75d6bb52df1bfce90fc",
          "message": "[CLI] `az vm list`: Report missing-parameter errors using CLI flag names instead of internal property names (#461)\n\n**Related command**\n`az vm list` (and any CLI command with required parameters)\n\n**Description**\nWhen a required parameter was missing on a CLI command, the validation\nerror reported the C# property name (e.g. `SubscriptionId`) instead of\nthe actual CLI flag. For example, `az vm list` produced `Required\nparameter 'SubscriptionId' is missing.` rather than the flag the user is\nexpected to type.\n\nThis fix makes the missing-required-parameter error use the parameter's\nCLI invocation name with a `--` prefix, matching the suggestion style\nalready used by `az login`:\n\n```\nRequired parameter '--subscription-id' is missing.\n```\n\n**Testing Guide**\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~CliArgumentValidationTests\"`\n- Manual: run `az vm list` with no arguments and confirm the error lists\n`--subscription-id` / `--resource-group`.\n\n**History Notes**\n\n[CLI] `az vm list`: Report CLI flag names instead of internal property\nnames in missing-required-parameter errors\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-15T15:01:25+10:00",
          "tree_id": "c9bd97a11589b4f624063aa8e0d78433aa686b51",
          "url": "https://github.com/Azure/azclips/commit/e9c54603958428a4c363e75d6bb52df1bfce90fc"
        },
        "date": 1784092345779,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 241882073.71,
            "unit": "ns",
            "range": "± 4846757.870839466"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 245118534.8,
            "unit": "ns",
            "range": "± 3389167.608757798"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 243413853.09,
            "unit": "ns",
            "range": "± 3427032.241642003"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "ed9842fea2ee85cc793dd258bdfbf35c9b7b1ec0",
          "message": "{DevOps} Trigger stable release 0.0.6 (#521)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.6 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.6.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-16T11:56:56+10:00",
          "tree_id": "f58364c5d024853577b19d853b1e166c584d3b6d",
          "url": "https://github.com/Azure/azclips/commit/ed9842fea2ee85cc793dd258bdfbf35c9b7b1ec0"
        },
        "date": 1784167774311,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 213637419.69,
            "unit": "ns",
            "range": "± 2165282.6361332918"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 215243589.82,
            "unit": "ns",
            "range": "± 2415326.5926579437"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 215007968.04,
            "unit": "ns",
            "range": "± 4642794.473737224"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "22533913f1469da9676d255e2b09a43f47fc7c62",
          "message": "{Config} Include CompletionOptions in config default entries. (#522)\n\nso az config get --key completion.enabled' returns the default value\n\nfix https://github.com/Azure/azclips/issues/519\n\n<img width=\"1423\" height=\"743\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/eaa0e93e-f8aa-441e-9990-a3e3b3e3f676\"\n/>\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-16T02:15:05Z",
          "tree_id": "ff4183cbd79551f74cffa348851080af099b3077",
          "url": "https://github.com/Azure/azclips/commit/22533913f1469da9676d255e2b09a43f47fc7c62"
        },
        "date": 1784168862539,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 211718396.73,
            "unit": "ns",
            "range": "± 2720185.4308781032"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 213783715.03,
            "unit": "ns",
            "range": "± 6522805.23167102"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 211073707.49,
            "unit": "ns",
            "range": "± 2304556.914228296"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3dfa70d256110f91b8ffba456e818ccc78815f94",
          "message": "{Core} Command not found error message adjustments (#525)\n\nCloses Azure/CLIPS#511.\n\nCloses https://github.com/Azure/azclips/issues/471\n\nTypos inside an installed extension group produced a misleading \"not\ninstalled\" error:\n\n```\n$ az vm --list\nerror: [ExtensionNotFound] Verb 'vm' is provided by extension '...Compute',\nwhich is not installed. Install it with: az extension add --name Compute\n```\n\n…even when Compute was already installed and `az vm --help` worked.\n\n**Cause.** `ExtensionDispatcher`'s verb-only fallback consulted\n`ExtensionHintTable` (a static build-time verb→package map) on any leaf\nmiss, ignoring whether the extension was already installed.\n\n**Fix.** Gate the hint-table throw on `LeafIndex.HasVerb(verb)`.\nLeafIndex is discovery-backed, so a hit means the extension IS installed\n— surface a new `CommandNotFoundException` (`code=CommandNotFound`, exit\n4) pointing at `az <verb> --help` instead.\n\nAfter:\n\n```\n$ az vm --list\nerror: [CommandNotFound] 'az vm' is not a recognized command. Run 'az vm --help' to see available commands.\n\n$ az group foobar\nerror: [CommandNotFound] 'az group foobar' is not a recognized command. Run 'az group --help' to see available commands.\n\n$ az vm list      # valid leaf still reaches the extension\n[ERROR] Failed to parse arguments for command 'vm list': ... 'SubscriptionId' is missing ...\n```\n\n`ExtensionNotFound` still fires when the verb is genuinely unknown\n(regression-tested).\n\n**Changes**\n- `LeafIndex.HasVerb` (O(1))\n- New `CommandNotFoundException`\n- Dispatcher gate + tests (`LeafIndexTests` +4,\n`ExtensionDispatcherTests` +3)\n- Docs: error-code contract in spec + Troubleshooting/exit-code table in\nhowto\n\n`CommandNotFound` is a new documented `code` (contract-relevant); exit\ncode stays `4` for scripting continuity.\n\n_Re-opened directly on the upstream repo (was #462 from fork) so CI can\nrun the fork-restricted steps._",
          "timestamp": "2026-07-16T13:30:59+10:00",
          "tree_id": "2b2bdc9d3165734e5c8e0f0e15083c47f7eac2fe",
          "url": "https://github.com/Azure/azclips/commit/3dfa70d256110f91b8ffba456e818ccc78815f94"
        },
        "date": 1784173265906,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 215063667.05,
            "unit": "ns",
            "range": "± 4933950.430338764"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 214831145.68,
            "unit": "ns",
            "range": "± 5022676.528913493"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 212439561.67,
            "unit": "ns",
            "range": "± 4050090.0609584954"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "5b207522ac08f2467f68e8117643da72b070d7f9",
          "message": "{Agent} Render plan review values from the command; fix one-shot exit code (#464)\n\n**Related command**\n\nAgent / error-recovery flows — any command that triggers the agent\nfallback (e.g. a failing `az vm create`). Internal agent behavior only.\n\n**Description**\n\n- **Plan review rendering (root fix).** The \"Values to review\" block\nused to render a model-supplied `{name, value}` verbatim, which\nmis-shaped switch flags (a model listing `--active` as `{name:active,\nvalue:true}` showed `active true`, reading as if the user had to type\n`true`). Now the model lists only the flag in `defaults`, and the host\nreads the real token straight from the step command — so review rows\nalways match what runs: `--active` for a switch, `--location eastus2`\nfor a valued flag, `--admin-password <admin-password>` for a\nplaceholder. Placeholders render their flag too. `PlanSanitizer` (the\nearlier post-hoc patch) is removed since the malformed output can no\nlonger be produced.\n- **One-shot exit code.** A one-shot agent fallback only *proposes* a\nfix — it never runs anything, so the original command still failed. It\nnow returns a non-zero exit code (was masking failure with the agent\nsubprocess's own 0), so `&&`, CI, and `$?` see the failure. Interactive\nis unchanged.\n<img width=\"1078\" height=\"534\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/cf784e35-842f-4423-ac56-da94bfdad0b0\"\n/>\n- **Branding.** Dropped the unsettled \"Azclips\" name from LLM-facing\nprompts, the runtime reminder, and tool descriptions; kept the \"not the\nclassic azure-cli\" anti-hallucination boundary. Internal identifiers\n(env vars, paths, assembly names) untouched.\n- **Cleanup.** Removed the unused `tui.md` prompt (TUI not planned) and\ndropped `tui` from the entrypoint whitelist.",
          "timestamp": "2026-07-16T17:16:51+10:00",
          "tree_id": "a6c7fcbc3d86d19ca14270de8c774f00950efc98",
          "url": "https://github.com/Azure/azclips/commit/5b207522ac08f2467f68e8117643da72b070d7f9"
        },
        "date": 1784187207534,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 246675752.05,
            "unit": "ns",
            "range": "± 3102619.1010059863"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 248306606.02,
            "unit": "ns",
            "range": "± 3223734.460326969"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 238214187.37,
            "unit": "ns",
            "range": "± 5384826.967070904"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "95b8ea4a3362282de8779c39f633b9332dafb033",
          "message": "{Help} Default `az` command returns top-level help and include welcome banner (#535)\n\nFixes https://github.com/Azure/azclips/issues/482\nFixes https://github.com/Azure/CLIPS/issues/503\n\n## What\n\nBare `az` (no arguments) now prints top-level help instead of erroring,\nwith a small ASCII welcome banner above it.\n\n**Before**\n```\n$ az\n[ERROR] Arguments cannot be empty. (Parameter 'args')\n```\n\n**After**\n```\n$ az\n\n     _        ___ _    ___ ___  ___ \n    /_\\   ___/ __| |  |_ _| _ \\/ __|\n   / _ \\ |_ / (__| |__ | ||  _/\\__ \\\n  /_/ \\_\\/__|\\___|____|___|_|  |___/\n\n  Welcome to AzCLIPS - Azure CLI, PowerShell & automation, in one host.\n  Run `az --help` for command groups, or `az <verb> --help` for a specific verb.\n\nGroups:\n    agent  : ...\n    config : ...\n    ...\n```\n\n## Changes\n\n- `ExtensionDispatcher.Parse`: empty args throw `DispatchHelpException`\n(same signal as `az --help`) instead of `ArgumentException`. Also covers\n`az --debug`, `az --output json`, etc., where global-flag stripping\nleaves nothing behind.\n- `Program.Main`: captures `isBareInvocation` before flag stripping so\nthe banner fires only on truly bare `az`. `az --help`, `az -h`, and `az\nhelp` stay grep-friendly.\n- New `WelcomeBanner` (figlet-small 'AzCLIPS') printed above root help\non bare invocation.\n- New hidden `PrintProbe` diagnostic for eyeballing terminal wrap /\nencoding / column alignment during triage (cold-path, no DI, not listed\nin `--help`).\n- Wiki updates so the enumerated bare-help shapes include bare `az`.\n\n## Verified\n\n- `dotnet build azclips.slnx` clean.\n- `Cli.Tests` all pass, including a regression test for the new\nempty-args dispatch.\n- Manual smoke: `az`, `az --help`, `az --debug`, `az version` all behave\ncorrectly.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-16T17:31:27+10:00",
          "tree_id": "a53de16c728d32c9fd1339f6060b5ff78c74fbb7",
          "url": "https://github.com/Azure/azclips/commit/95b8ea4a3362282de8779c39f633b9332dafb033"
        },
        "date": 1784187722580,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 222924990.39,
            "unit": "ns",
            "range": "± 3727701.745055884"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 223913949.93,
            "unit": "ns",
            "range": "± 5290346.69417506"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 221494126.29,
            "unit": "ns",
            "range": "± 4009652.701538785"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b28c14debd913c390d2bf76791ad3edf479afab4",
          "message": "{DevOps} Trigger stable release 0.0.7 (#538)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.7 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.7.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-16T21:37:55+10:00",
          "tree_id": "cf47d606296f0224af5d156555888fa01f8d4e9d",
          "url": "https://github.com/Azure/azclips/commit/b28c14debd913c390d2bf76791ad3edf479afab4"
        },
        "date": 1784202729266,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 232820853.74,
            "unit": "ns",
            "range": "± 3300817.3094495204"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 234078346.67,
            "unit": "ns",
            "range": "± 3833678.189611168"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 231602171.65,
            "unit": "ns",
            "range": "± 3705288.175395022"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "xumingk1@gmail.com",
            "name": "xuming-ms",
            "username": "xuming-ms"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "4d4c98593228f89a45946fcc5908c5ca224542ee",
          "message": "[Auth] Select the active profile's account from the shared MSAL token cache (#537)\n\n## Summary\nThe MSAL token cache is shared across every identity the user has ever\nsigned in with. The previous code took `accounts.FirstOrDefault()`,\nwhich could pick a stale/unrelated account (e.g. a personal account for\na work profile), producing 403 AuthorizationFailed on ARM.\n\nThis change introduces `MsalAccountSelector` to choose the cached\naccount that matches the active profile, mirroring Azure PowerShell\n(`SharedTokenCacheCredential` with `EnableGuestTenantAuthentication =\ntrue`):\n- match by username (UPN) first,\n- tie-break by tenant only when the UPN is ambiguous,\n- require exactly one match, otherwise return null so the caller\nre-authenticates instead of signing requests with the wrong identity.\n\nAlso, `BuildApp` now uses the multi-tenant `organizations` authority\nwhen no tenant is known (initial login) instead of a hardcoded tenant,\nand the subscription's owning tenant is resolved downstream.\n\n## Changes\n- Add `MsalAccountSelector` (UPN + tenant tie-break, exactly-one\nselection).\n- Use it in `MsalAuthService.AcquireTokenSilentlyAsync`; force re-auth\nwhen no account matches.\n- `BuildApp`: fall back to `organizations` authority when tenant is\nunset.\n- Add `MsalAccountSelectorTests` covering stale-account, guest-tenant,\nmulti-home, and no-match cases.\n\n## Validation\n- `dotnet build src/Core/Core.Authentication/Core.Authentication.csproj`\nsucceeds (0 warnings/errors).\n- `dotnet test tests/Cli.Tests` (MsalAccountSelector filter): 6/6 pass.\n\n**History Notes**\n\n[Login] `az login`: Select the MSAL cache account matching the active\nprofile during silent token acquisition to avoid signing requests with a\nstale or unrelated account\n\n---\n\n---------\n\nCo-authored-by: Ming Xu <xumi@microsoft.com>",
          "timestamp": "2026-07-17T12:23:52+10:00",
          "tree_id": "90d2371e7721685035860095258b31d0b705c8e7",
          "url": "https://github.com/Azure/azclips/commit/4d4c98593228f89a45946fcc5908c5ca224542ee"
        },
        "date": 1784255747617,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 230029882.33,
            "unit": "ns",
            "range": "± 4636544.87876481"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 228724232.97,
            "unit": "ns",
            "range": "± 4987520.938455882"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 230217530.31,
            "unit": "ns",
            "range": "± 4200426.769449783"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "30e33e5e75e7e3d2d8e0bf3fc473c9c41ad989bf",
          "message": "[Compute] Fix empty vm list and disk list output by writing the ARM list envelope value (#463)\n\n**Related command**\n`az vm list`, `az disk list`, `Get-AzVM`\n\n**Description**\n`az vm list` / `Get-AzVM` (and the disk equivalents) returned no results\neven when the resource group contained resources. ARM list endpoints\nreturn an envelope `{ \"value\": [...], \"nextLink\": \"...\" }`, but\n`ListVMProcessor`/`ListDiskProcessor` were typed as\n`ComputeProcessorBase<VirtualMachine>` / `<Disk>`, so the base pipeline\ndeserialized the envelope into a single resource object and silently\ndropped the `value` array.\n\nFix (kept intentionally small):\n1. Introduce `VirtualMachineListResult` / `DiskListResult` envelope\nmodels.\n2. Add a small `SelectOutput` hook on the generic Compute base (default\nreturns the body as-is); the list processors override it to write\n`body.Value` — a flat array. PowerShell enumerates it into individual\nobjects (`enumerateOutput`), and the CLI serializes it as a JSON array.\n\nOut of scope (deliberate): `nextLink` pagination is not followed yet — a\nresource group beyond ARM's page size will return only the first page.\nThis is acceptable for private preview and will be designed/implemented\nseparately.\n\n**Testing Guide**\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~ListVMProcessorTests|FullyQualifiedName~ListDiskProcessorTests\"`\n- Manual: `az vm list --subscription-id <sub> --resource-group <rg>` and\n`Get-AzVM -SubscriptionId <sub> -ResourceGroupName <rg>` against an RG\nwith VMs.\n\n**Live validation (real Azure)**\n\nRan the built CLI (extensions installed via `az extension add --path\n<nupkg>`) against a live subscription:\n\n- Empty RG returns `[]` (not an error) for both `az vm list` and `az\ndisk list`.\n- After creating a managed disk, `az disk list` returns it:\n```\n[ { \"id\": \".../disks/listfix-disk1\", \"name\": \"listfix-disk1\", \"location\": \"australiaeast\" } ]\n```\n- After creating a VM, `az vm list` returns it (flat JSON array with the\nVM object).\n\nTest resources were deleted afterward.\n\n**History Notes**\n\n[Compute] `az vm list`: Fix empty output by writing the ARM list\nenvelope value\n[Compute] `az disk list`: Fix empty output by writing the ARM list\nenvelope value\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-17T12:24:55+10:00",
          "tree_id": "c90cc53bac3c8dfcbab159519ba96a1dbe695723",
          "url": "https://github.com/Azure/azclips/commit/30e33e5e75e7e3d2d8e0bf3fc473c9c41ad989bf"
        },
        "date": 1784256017169,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 215451547.1,
            "unit": "ns",
            "range": "± 3454833.3159783073"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 215930180.9,
            "unit": "ns",
            "range": "± 2194660.694945903"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 213796271.21,
            "unit": "ns",
            "range": "± 2674610.8148138938"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "maddisondas@microsoft.com",
            "name": "MaddyMicrosoft",
            "username": "MaddyMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "11ea2c8d60c6d75004a9463c0b5ce389770350fa",
          "message": "[Login] Correct parameter error messages to use Client type param (#530)\n\nThe shared LoginProcessor backs both `az login` and `Connect-AzAccount`,\nbut its runtime error/warning messages hardcoded CLI-style flags\n(`--subscription-id`, `--tenant`), which are parameter-binding errors on\nthe PowerShell surface.\n\nResolve ClientType from DI and format every processor-emitted parameter\nreference per surface (`--kebab-case` on CLI, `-PascalCase` on\nPowerShell). All 17 sites across ValidateArguments,\nEnsureCrossTenantAccessible, ResolveSubscriptionTenantAsync, and\nResolveDefaultSubscriptionAsync now use a shared `Flag()` helper. Also\nrenders the sign-in guidance command surface-correctly\n(`Connect-AzAccount` vs `az login`) and fixes a latent `--tenant` typo\n(the actual CLI flag is `--tenant-id`).\n\nAdds LoginProcessorValidationTests with 12 theory cases covering all\nsites x both surfaces. Adds Moq to Cmdlets.Tests (matching Cli.Tests\nversion).\n\nFixes #468.\n\n\n**Related command**\n`az login` / `Connect-AzAccount`\n\n**Description**\nThe `LoginProcessor` is a single shared class that backs the CLI (`az\nlogin`) and PowerShell (`Connect-AzAccount`) surfaces. Its runtime error\nand warning messages hardcoded CLI-only wording, so a PowerShell user\nwho typed a bare `Connect-AzAccount` saw:\n\n```\nConnect-AzAccount: A subscription id is required. Provide it with --subscription-id.\n```\n`--subscription-id` is a CLI flag; on PowerShell it fails to bind as\n`PositionalParameterNotFound`. The correct parameter on that surface is\n`-SubscriptionId`.\n\nRoot cause: message strings baked one surface's dash notation into the\nshared processor. This change resolves `ClientType` from the DI\ncontainer inside the processor and introduces a private static\n`FormatParameterName` helper plus a `Flag()` local function per method.\nEvery parameter reference in a processor-emitted message is now rendered\nwith the caller's surface form.\n\nSites updated (17 total in\n`src/Extensions/Login/Processors/LoginProcessor.cs`):\n\n- `ValidateArguments` — 12 messages (mutual-exclusivity, MI selector\nrules, subscription-required, tenant-with-identity,\nidentity-with-devicecode, etc.)\n- `EnsureCrossTenantAccessible` — 2 throws (tenant probe threw / probe\nreturned false)\n- `ResolveSubscriptionTenantAsync` — 1 throw (ARM lookup failed)\n- `ResolveDefaultSubscriptionAsync` — 2 `logger.Warn` sites (no subs\nfound / listing failed)\n\nNo behavior change beyond message text. \n\n**Testing Guide**\nAutomated:\n\n```bash\n# 12 new theory cases covering all sites × both surfaces\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -c Debug \\\n  --filter \"FullyQualifiedName\\~LoginProcessorValidationTests\"\n\n# Full Cmdlets.Tests suite (53 tests)\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -c Debug\n\n# Cli.Tests login/profile subset (regression check)\ndotnet test tests/Cli.Tests/Cli.Tests.csproj -c Debug \\\n  --filter \"FullyQualifiedName\\~ProfileCliTests|FullyQualifiedName\\~ManagedIdentityIdCodecTests|FullyQualifiedName\\~ProfileSubscriptionResolutionTests\"\n```\n\nManual (from a fresh `pwsh -NoProfile`, after `./build/build.ps1\n-Configuration Debug`):\n\n```powershell\n$env:Path = \"$PWD\\artifacts\\Debug\\Core.Cli;$env:Path\"\nImport-Module .\\artifacts\\Debug\\PS\\Login\\Login.psd1 -Force\n\n# CLI — expect '--subscription-id' etc.\naz login\naz login --identity --tenant-id 11111111-1111-1111-1111-111111111111\naz login --identity --client-id aaa --object-id bbb\naz login --identity --use-device-code\naz login --subscription-id 00000000-0000-0000-0000-000000000000 --client-id aaa\n\n# PowerShell — expect '-SubscriptionId' etc.\nConnect-AzAccount\nConnect-AzAccount -Identity -TenantId 11111111-1111-1111-1111-111111111111\nConnect-AzAccount -Identity -ClientId aaa -ObjectId bbb\nConnect-AzAccount -Identity -UseDeviceCode\nConnect-AzAccount -SubscriptionId 00000000-0000-0000-0000-000000000000 -ClientId aaa\n```\n\n`--profile` / `-Profile` validation checks are covered by the unit tests\nonly — they require a pre-existing profile to reach the processor, which\nneeds a real Azure sign-in.\n\n\n**History Notes**\n\n[Login] `az login` / `Connect-AzAccount`: Show surface-correct parameter\nnames in validation and post-sign-in error messages\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-17T13:02:10+10:00",
          "tree_id": "c1a587214c10f3241bdb6cff47ffdf40d4e15e77",
          "url": "https://github.com/Azure/azclips/commit/11ea2c8d60c6d75004a9463c0b5ce389770350fa"
        },
        "date": 1784258040955,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 230607808.65,
            "unit": "ns",
            "range": "± 6662414.901334262"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 228684430.74,
            "unit": "ns",
            "range": "± 4770993.948921063"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 229339157.43,
            "unit": "ns",
            "range": "± 5824538.7302069375"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3d933f5df0af5cdf49db2fd2a611afaab5ba599e",
          "message": "{DevOps} Trigger stable release 0.0.8 (#551)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.8 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.8.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-17T13:29:53+10:00",
          "tree_id": "22a3b313a2f49f967fc7f8aa270a2a407a72c018",
          "url": "https://github.com/Azure/azclips/commit/3d933f5df0af5cdf49db2fd2a611afaab5ba599e"
        },
        "date": 1784259654401,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 229381756.38,
            "unit": "ns",
            "range": "± 4147161.0396673093"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 229381234.36,
            "unit": "ns",
            "range": "± 4473515.419546557"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 229714324.56,
            "unit": "ns",
            "range": "± 4413985.7122697625"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "128b98e0df6b5438009ac6be1c1f1e9b65c5d1e2",
          "message": "{Telemetry} Remove unnecessary configs for telemetry (#527)\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\nRemoves dangling configs for `telemetry.appInsightsInstrumentationKey`\nand `telemetry.zipkinEndpoint`\nWe do not want to allow users to change the instrumentation key and\nzipkin itself has been deprecated other options are being evaluated.\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\nRun `<path>/az config list --all`\n<img width=\"1325\" height=\"805\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/c762b081-ca50-478f-9109-c03e71b113fb\"\n/>\n\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-17T13:55:36+10:00",
          "tree_id": "04e886f8984355847d9826223d2b00083f80b219",
          "url": "https://github.com/Azure/azclips/commit/128b98e0df6b5438009ac6be1c1f1e9b65c5d1e2"
        },
        "date": 1784261266350,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 228616024.13,
            "unit": "ns",
            "range": "± 5108593.469481291"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 230366982.75,
            "unit": "ns",
            "range": "± 4784873.536593583"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 229076980.12,
            "unit": "ns",
            "range": "± 4082496.5215595597"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "ef08058baa79d7ff79c3d238de3003e3f622b7a2",
          "message": "{Autocomplete} Fix multi layered commands not completing correctly (#557)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n`az deployment`\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\nFor any command that has multiple sub commands autocompletion was not\nable to resolve the completion tree correctly as the keys are flattened.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\nsource the sh scripts\n`. .\\src\\CLI\\Completions\\az.completions.ps1` or `source\n.\\src\\CLI\\Completions\\az.completions.bash`\nattempt to auto complete any commands with multiple sub commands groups\n`az deployment` or `az agent`\n\n<img width=\"1348\" height=\"158\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/88b32176-853c-4ccb-967e-cf4b896c336a\"\n/>\n\n\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-18T13:21:35+10:00",
          "tree_id": "167310b52a90cd89df6cba088630b3cc50c13f4c",
          "url": "https://github.com/Azure/azclips/commit/ef08058baa79d7ff79c3d238de3003e3f622b7a2"
        },
        "date": 1784345509218,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 223481953.06,
            "unit": "ns",
            "range": "± 6277739.648203632"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 222072086.62,
            "unit": "ns",
            "range": "± 4278608.353710654"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 222736170.67,
            "unit": "ns",
            "range": "± 5200776.292921747"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "93666114eef2966b8bd8c5f7d3d75716682d7190",
          "message": "[Resources] Make SubscriptionId optional for resource group create (#481) (#555)\n\n**Related command**\n`az group create` / `New-AzResourceGroup`\n\n**Description**\n`SubscriptionId` was declared as a required parameter in the PS and CLI\ncommand metadata for `New-AzResourceGroup` / `az group create`, forcing\nusers to pass it explicitly. `ResolveSubscriptionId` already falls back\nto the active profile's subscription, so this marks the parameter\noptional to match the `group show` and `group list` commands and\ndefaults to the current subscription when omitted.\n\nThe CLI help doc for `group create` is also updated to note the\nparameter is optional.\n\nFixes #481\n\n**Testing Guide**\n- `dotnet build src/Extensions/Resources/Extensions.Resources.csproj`\n- Run `az group create --name myrg --location eastus` without\n`--subscription-id` and confirm the group is created in the current\nsubscription.\n\n**History Notes**\n\n[Resources] `az group create`: Make SubscriptionId an optional parameter\nthat defaults to the current subscription\n[Resources] `New-AzResourceGroup`: Make SubscriptionId an optional\nparameter that defaults to the current subscription\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-18T19:45:24+10:00",
          "tree_id": "fba663f299bcd6bcf2926a105907c4fe0034bc00",
          "url": "https://github.com/Azure/azclips/commit/93666114eef2966b8bd8c5f7d3d75716682d7190"
        },
        "date": 1784368510388,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 212582094.93,
            "unit": "ns",
            "range": "± 2025899.7021916427"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 213109918.53,
            "unit": "ns",
            "range": "± 2584536.138492071"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 212744896.9,
            "unit": "ns",
            "range": "± 3248168.4723780695"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "fdf548a6617964f89054d12fc82fc52fd4f1f605",
          "message": "{Telemetry} Use consistent LocalAppData path for trace files (#523)\n\nFixing https://github.com/Azure/azclips/issues/511\nTelemetry trace logs were being written to\n{LocalAppData}/Azclips/traces/ while auth profiles are stored in\n{LocalAppData}/.azclips/. Changed\nFileTelemetrySender.GetTraceDirectory() to use\nHomePaths.GetDefaultLocalAppDataRoot() so all local app data files live\nunder {LocalAppData}/.azclips/ consistently.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-20T15:38:08+10:00",
          "tree_id": "388ff228806364a7e78e2ca8357d18f70d952457",
          "url": "https://github.com/Azure/azclips/commit/fdf548a6617964f89054d12fc82fc52fd4f1f605"
        },
        "date": 1784526590355,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 217611637.97,
            "unit": "ns",
            "range": "± 4282606.151991826"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 218209214.79,
            "unit": "ns",
            "range": "± 3226759.1924705156"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 218671638.21,
            "unit": "ns",
            "range": "± 4657018.855460509"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a94c7be9f81727963950cd7edbf9f3b04e516df1",
          "message": "{Telemetry} Revert consistent LocalAppData path for trace files (#569)\n\n**Related command**\nN/A\n\n**Description**\nReverts merged PR #523 (commit\n`fdf548a6617964f89054d12fc82fc52fd4f1f605`) to undo the LocalAppData\ntrace-file path change in `FileTelemetrySender`.\n\n**Testing Guide**\nN/A\n\n**History Notes**\n{Telemetry} Revert LocalAppData trace-file path change introduced by\n#523\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-21T09:08:56+10:00",
          "tree_id": "fba663f299bcd6bcf2926a105907c4fe0034bc00",
          "url": "https://github.com/Azure/azclips/commit/a94c7be9f81727963950cd7edbf9f3b04e516df1"
        },
        "date": 1784589564884,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 218810793.89,
            "unit": "ns",
            "range": "± 2619247.340898834"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 219257840.42,
            "unit": "ns",
            "range": "± 5300513.43195563"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 217293629.13,
            "unit": "ns",
            "range": "± 2274478.2749775345"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6832555fb59bbf86d378dd4d2dea3a67ddcced96",
          "message": "{DevOps} Trigger stable release 0.1.0 (#572)\n\nUpdates src/CLI/Core.Cli.csproj to 0.1.0 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.1.0.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-21T09:29:27+10:00",
          "tree_id": "3322ed8cc69089216c3644b2bea163b5298c2e83",
          "url": "https://github.com/Azure/azclips/commit/6832555fb59bbf86d378dd4d2dea3a67ddcced96"
        },
        "date": 1784590764757,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 213934102.67,
            "unit": "ns",
            "range": "± 1912278.2494964935"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 214397594.65,
            "unit": "ns",
            "range": "± 2132879.51445386"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 213534860.58,
            "unit": "ns",
            "range": "± 2121748.9778888472"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "40ea71669329eb70850bea98de8315c9e4d9dd08",
          "message": "{chore} merge dev recent commits to main (#582)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-22T13:53:15+10:00",
          "tree_id": "babec21741ab3a1feef4ae8f984fb06321e3e8d1",
          "url": "https://github.com/Azure/azclips/commit/40ea71669329eb70850bea98de8315c9e4d9dd08"
        },
        "date": 1784693075984,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 224955771.17,
            "unit": "ns",
            "range": "± 3428945.6986707365"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 226502980.02,
            "unit": "ns",
            "range": "± 4585014.099096692"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 228413911.67,
            "unit": "ns",
            "range": "± 6168782.513052963"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "14b0f3ea718e0d129002befbba84e23fa1c17715",
          "message": "{Telemetry} Telemetry consistent LocalAppData (#586)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\nTelemetry trace logs were being written to\n{LocalAppData}/Azclips/traces/ while auth profiles are stored in\n{LocalAppData}/.azclips/. Changed\nFileTelemetrySender.GetTraceDirectory() to use\nHomePaths.GetDefaultLocalAppDataRoot() so all local app data files live\nunder {LocalAppData}/.azclips/ consistently.\n\n(cherry picked from commit\nhttps://github.com/Azure/azclips/commit/28348e09106275764d15ff9af4c0a8372b8e1b5f)\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\nCo-authored-by: Nori Zhang <norizhang@microsoft.com>",
          "timestamp": "2026-07-22T04:11:55Z",
          "tree_id": "23f8de13cf0873924080d39cec2039615fa12dd2",
          "url": "https://github.com/Azure/azclips/commit/14b0f3ea718e0d129002befbba84e23fa1c17715"
        },
        "date": 1784694246056,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 213473366.91,
            "unit": "ns",
            "range": "± 2230350.7285213373"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 214788738.93,
            "unit": "ns",
            "range": "± 2206393.6736710523"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 212665216.49,
            "unit": "ns",
            "range": "± 2425977.3453126876"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b03a05ed76159e4c045fb381c10b445ec2e0dd31",
          "message": "{Core} Update default log-level to `WARN`, adjust call sites, and add debug logging for extension loading (#588)\n\n> **Supersedes [#575](https://github.com/Azure/azclips/pull/575)** —\nthat PR was auto-closed when the `dev` branch was deleted. Full prior\nreview discussion (7 Copilot comments + follow-ups, all addressed) lives\non #575.\n\n---\n\nFixes [Azure/CLIPS#493](https://github.com/Azure/CLIPS/issues/493) —\nLogging: Examine default log-level, call site level usage, and\n`WriteLine` vs `Error.WriteLine`.\n\n---\n\n## 1. Summary\n\nThree intertwined changes to CLI logging, all shipping here:\n\n1. **Stream routing fix** — align with POSIX/GNU: stdout = command data,\nstderr = diagnostics. Today\n[`BuildStreams`](src/CLI/Program.cs#L203-L253) sends\nwarning/info/verbose/debug to stdout, polluting `--output json`\npipelines. Every peer CLI (Azure CLI Python, Az PowerShell, AWS CLI,\ngit, kubectl, docker, terraform) puts diagnostics on stderr.\n2. **Default log level → `warn`** (was `error`). Matches Azure CLI\nPython and Az PowerShell defaults; surfaces warnings that are currently\nsilently swallowed (e.g. [`LoginProcessor.cs` L419,\nL428](src/Extensions/Login/Processors/LoginProcessor.cs#L419-L428)).\n3. **Extension-load debug tracing** — per-extension load rows +\naggregate summary table on\n[`ExtensionLoader`](src/CLI/AssemblyLoading/ExtensionLoader.cs),\nmodelled on [`azure-cli/src/azure-cli-core/azure/cli/core/__init__.py`\nL232-L235,\nL680-L688](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232).\nAll at `Debug`; emitted only under `--debug` or\n`core.logging.level=debug`.\n\n---\n\n## 2. Decision Registry\n\nSuccinct \"why\" for each committed decision.\n\n| # | Decision | Rationale |\n| ------- |\n--------------------------------------------------------------------------------------------\n|\n-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n|\n| **D1** | Default log level: `warn` (was `error`) | Azure-family peer\nsymmetry — Azure CLI Python defaults to `Warning`; Az PowerShell\neffective baseline is `Warning + Error`. `info`-as-default rejected (no\ncited peer default; would need call-site retriage). Shared config also\nflips PS cmdlets; `-WarningAction Ignore` is the escape hatch. |\n| **D2** | Route Warning/Info/Verbose/Debug to **stderr** | Industry\nstandard. POSIX defines stderr as the diagnostic stream; [GNU Coding\nStandards\n§4.4](https://www.gnu.org/prep/standards/html_node/Errors.html) codifies\nit; every peer CLI examined follows it. Current stdout routing is a bug.\n|\n| **D3** | Collapse `ILoggingService.Log(...)` → `Info(...)` in\n`ConsoleLoggingService` | Zero production call sites;\n[`OutputStreamAdapter.Log`](src/Core/Core.Runtime/ScopedLoggingService.cs#L66-L69)\nalready delegated to `Info`. `Log` stays on the interface as a permanent\nalias — no deprecation planned. |\n| **D4** | Add `ShouldEmitColor()` guard with standard precedence |\nPrecedence chain matches ecosystem standard: `NO_COLOR` → `FORCE_COLOR`\n→ `TERM=dumb` → redirection check → color. Applied at all eight color\nsites; broader audit rolled into the [`ConsoleSink`\nfollow-up](https://github.com/Azure/CLIPS/issues/543). |\n| **D5** | Extension-load tracing: per-load rows + aggregate table, all\nat `Debug` | Copies the [azure-cli\n`__init__.py`](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232)\nL232-L235, L680-L688 pattern: format-string constants + per-load Debug\nrow + `Total (count)` summary. Uniform `Debug` level — user gets\neverything or nothing under `--debug`. |\n| **D6** | Log prefix: per-class `[nameof(TypeName)]` | Matches existing\ncodebase convention — e.g.\n[`MsalAuthService`](src/Core/Core.Authentication/MsalAuthService.cs#L36).\n|\n| **D7** | Fix TUI's stderr classifier as part of Tier B | [`TUI\nProcessService.RunCliAsync`](TUI/services/ProcessService.cs) mapped all\nstderr → `HistoryItemType.Error`; post-D2 every warning would render as\nan error in the TUI. `ClassifyLine` extended to recognize the AzClips\nprefix set. |\n| **D8** | Don't demote `Info` call sites in this PR | Under `warn`,\n`Info` sites stay hidden by default so noise doesn't manifest. No\n`info`-default move planned; revisit only if that ever changes. |\n| **D9** | New subprocess-based integration test required |\n[`ExecuteCli`\nfixture](tests/Cli.Tests/Configuration/ConfigCliTests.cs#L362)\nconstructs its own `OutputStreams` and bypasses `Program.Main` /\n`BuildStreams` — cannot test D2 routing. |\n| **D10** | Harmonize diagnostic prefix to `LEVEL: message` (uppercase\nword, single colon, single space) | Three surfaces were emitting three\ndifferent prefix styles. Chose `LEVEL:` over `[LEVEL]`: matches Azure\nCLI Python's output, the Python `logging` default formatter, and the PS\nhost's own rendering of `cmdlet.WriteWarning(...)`. Consolidated\npre-production while no downstream parser has locked in a style. |\n\n---\n\n## 3. Implementation summary\n\nAll 18 planned items landed on this branch; one (a chain-order Debug\nfrom `CLIAssemblyLoadingProvider`) was dropped mid-flight as unreachable\nthrough the level-gated pipeline. Change groups map to the decisions\nabove:\n\n- **Stream routing (D2/D3/D4/D7):**\n[`BuildStreams`](src/CLI/Program.cs),\n[`ConsoleLoggingService`](src/Core/Core.Runtime/ConsoleLoggingService.cs),\nand\n[`ConsoleOutputAdapter`](src/Core/Core.Runtime/ConsoleOutputAdapter.cs)\nall now write warn/info/verbose/debug to `Console.Error`; new\n[`ColorPolicy.ShouldEmitColor`](src/Core/Core.Runtime/ColorPolicy.cs)\nguards color at all eight sites (stderr uses\n`Console.IsErrorRedirected`, stdout uses `Console.IsOutputRedirected`);\nTUI [`ClassifyLine`](TUI/services/ProcessService.cs) recognizes the\nAzClips prefix set.\n- **Default flip (D1):**\n[`CoreOptions.LoggingOptions.Level`](src/Core/Core.Abstractions/CoreOptions.cs),\nthe [config schema\nmirror](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs), and\nthe two invalid-value fallbacks ([CLI](src/CLI/CliLoggingPolicy.cs),\n[PS](src/PS/Common/CmdletOutputStreamsFactory.cs)) all move `error` →\n`warn`. Tests updated in lockstep\n([`ConfigCliTests`](tests/Cli.Tests/Configuration/ConfigCliTests.cs),\n[`CliLoggingPolicyTests`](tests/Cli.Tests/Configuration/CliLoggingPolicyTests.cs),\n[`CmdletOutputStreamsFactoryTests`](tests/Cmdlets.Tests/CmdletOutputStreamsFactoryTests.cs)).\n- **Extension-load debug tracing (D5/D6):** new\n[`ExtensionLoadTracker`](src/CLI/AssemblyLoading/ExtensionLoadTracker.cs)\naccumulates per-load rows and emits an aggregated table on shutdown;\nboth loader implementations in\n[`ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs)\ninstrumented with entry/probe/hit/miss `[ClassName]`-prefixed Debug\nlines. Summary routes through `streams.WriteDebug` (level-gated) so the\ntable stays silent under `warn`.\n- **Tests (D9):**\n[`StreamRoutingIntegrationTests`](tests/Cli.Tests/StreamRoutingIntegrationTests.cs)\n(4 subprocess tests: `WriteError`→stderr; `warn` keeps stderr silent;\n`--debug` puts `DEBUG:` on stderr with stdout clean; `--debug` emits\nper-loader + `ExtensionLoadTracker` lines) and\n[`TuiClassifyLineTests`](tests/Cli.Tests/TuiClassifyLineTests.cs) (7\nunit tests covering every prefix + fallthrough + ANSI stripping).\n`ClassifyLine` bumped to `internal static` with\n`InternalsVisibleTo=\"Cli.Tests\"` for the unit tests.\n- **Docs:**\n[`docs/azure-cli/docs-conceptual/config.md`](docs/azure-cli/docs-conceptual/config.md)\ngained a **Stream routing** subsection and refreshed sample outputs;\n[`eng-docs/spec/config-implementation.md`](eng-docs/spec/config-implementation.md)\n§10 \"CLI Runtime Logging Policy\" expanded to cover the default flip,\nstream contract, `LEVEL:` prefix, color gating, and the `EmitSummary`\nstreams-vs-logger invariant.\n\nPer-item commit SHAs and file lists live in the branch's git log — see\nthe **Files changed** / **Commits** tabs for the granular view.\n\n---\n\n## 4. Behavior audit under the new `warn` default\n\nCodebase scan for `.Warn(...)` / `WriteWarning(...)` producers to\nenumerate what changes from \"silent under `error`\" to \"visible under\n`warn`\". Result: **two user-facing warning sites, both in `az login`.**\n\n### Newly visible warnings\n\n1. **`az login` — no subscriptions on the identity**\n\n[`LoginProcessor.cs:419`](src/Extensions/Login/Processors/LoginProcessor.cs#L419)\n— `logger.Warn(\"No subscriptions found for the logged-in identity.\nContinuing with tenant-level access; pass '--subscription' to target a\nsubscription.\")`. Fires when subscription auto-discovery completes but\nreturns zero entries after a successful login. Actionable: the warning\ntells the user why no default subscription was picked and how to specify\none.\n\n2. **`az login` — subscription auto-discovery failure**\n\n[`LoginProcessor.cs:428`](src/Extensions/Login/Processors/LoginProcessor.cs#L428)\n— `logger.Warn($\"Failed to auto-discover subscriptions: {ex.Message}.\nContinuing without a default subscription; pass '--subscription' to set\none.\")`. Fires when subscription auto-discovery throws (e.g. ARM\ntransient failure, revoked permissions). Actionable: surfaces the\nunderlying exception message and the workaround.\n\nBoth are on the intentional side of D1's PS-side note: under the new\ndefault the PS host applies `$WarningPreference = Continue` and shows\nthem; callers who want the old silence use `-WarningAction Ignore`.\n\n---\n\n## 5. Validation\n\n- `dotnet build azclips.slnx`\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`\n- Manual: `az login --output json 2> /dev/null | jq .` and `az extension\nlist 2> /dev/null | jq .` produce clean JSON.\n- Manual (Windows): `Console.ForegroundColor` behavior on stderr, both\npiped and non-piped; `NO_COLOR=1 az foo` suppresses color.\n- Manual: `AZCLIPS_LOG_LEVEL=debug az <cmd-that-loads-extension>` shows\nthe new tracker summary and per-extension rows.\n- Manual: `az --debug config get --key core.logging.level` — clean\nstdout, debug trace on stderr, still parseable when piped.\n- Manual: `az config get --key core.logging.level` with no prior setting\nprints `core.logging.level = warn [default]` (previously `error`).\n- Manual: an operation that emits `Warn` (e.g. login with no\ndiscoverable subscriptions — [`LoginProcessor.cs`\nL419](src/Extensions/Login/Processors/LoginProcessor.cs#L419)) surfaces\nthe warning on stderr under the new default; previously silently\nswallowed.\n- Manual (PS): import a module (e.g. `Import-Module\nsrc/PS/Login/Login.psd1`) and invoke a cmdlet that triggers\n`logger.Warn(...)` — confirm the warning surfaces in the PS host\n(default `$WarningPreference = Continue`). Then re-run with\n`-WarningAction Ignore` and confirm it's suppressed. Both should hold\nwith no prior `core.logging.level` config set.\n\n---\n\n## Follow-ups (not in this PR)\n\nTracked under epic [Azure/CLIPS#542 \"AzClips\nLogging\"](https://github.com/Azure/CLIPS/issues/542):\n\n- [Azure/CLIPS#543](https://github.com/Azure/CLIPS/issues/543) —\nConsolidate console-writing primitives into a single `ConsoleSink`.\n- [Azure/CLIPS#544](https://github.com/Azure/CLIPS/issues/544) — Add a\n`Warn` value to `HistoryItemType` in the TUI.\n- [Azure/CLIPS#545](https://github.com/Azure/CLIPS/issues/545) — Bring\nstray raw `Console.*` call sites under `ILoggingService`.\n\n---------\n\nCo-authored-by: JaysonTaiMicrosoft <jaysontai@microsoft.com>\nCo-authored-by: Nori Zhang <norizhang@microsoft.com>",
          "timestamp": "2026-07-22T14:47:20+10:00",
          "tree_id": "10b4dc80bf3ba0cd7418c8ff105982581fc550d6",
          "url": "https://github.com/Azure/azclips/commit/b03a05ed76159e4c045fb381c10b445ec2e0dd31"
        },
        "date": 1784696302420,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 216761928.11,
            "unit": "ns",
            "range": "± 2516470.2751066196"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 216012525.45,
            "unit": "ns",
            "range": "± 2751476.099554423"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 213578761.72,
            "unit": "ns",
            "range": "± 2345222.611440035"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9ace2ec11b7d211c8c5c7c1f07baef54f4e0ed68",
          "message": "[Extension] `az extension list`: Include bundled extensions and expose on-disk `Location` field (#590)\n\n**Related command**\n\n- `az extension list`\n- `az extension show`\n\n**Description**\n\nCurrently `az extension list` only surfaces extensions installed via `az\nextension add` under `~/.azclips/extensions/`; extensions bundled with\nthe `az` install are silently filtered out, and no row exposes the\non-disk DLL path.\n\nThis PR:\n\n- Removes the Core-tier `PackageId` filter in\n`DiscoveryBackedExtensionRegistry` so all discoverable extensions (Core\n+ Bundled + Installed) surface via `az extension list` / `az extension\nshow`.\n- Adds an absolute `Location` DLL path per row, plumbed from the loaders\nthrough `InstalledExtensionRef` → `DiscoveredExtensionInfo` →\n`ExtensionRegistryEntry`. `Location` is runtime-only\n(`[JsonIgnore(Always)]`) and never persisted to `registry.json`.\n\nFixes https://github.com/Azure/CLIPS/issues/478\n\n**Key design decisions**\n\n- **Scope**: list all three tiers (Core + Bundled + Installed) with an\nabsolute-path `Location` field. No `Tier` column — path prefix conveys\norigin.\n- **List only what's discoverable on disk**: do not synthesize rows from\n`BuiltInExtensions.Map` for Core verbs with no DLL present.\n- **Winner-take-all preserved**: `list` still shows one row per\n`PackageId` via `ExtensionCandidateSelector` — matches dispatch exactly.\nBroader \"show shadowed candidates\" visibility is coupled to the\nparallel-version-install work and deferred.\n- **`Location` is runtime-only**: never persisted; no `registry.json`\nschema bump.\n- **Private preview**: breaking changes to `Core.Abstractions.*` records\nand `IExtensionLoader` tuple are accepted; no back-compat shims.\n\n**Testing Guide**\n\n- Automated: `dotnet build azclips.slnx` clean; `Cli.Tests` 505 pass;\n`Core.Extensions.Tests` 79/79; `Core.Engine.Tests` 2/2. New tests cover\n`Location` propagation through `PivotByVerb` and end-to-end from both\nloaders.\n- Manual: full sweep on both local Debug build and CI\n`Distribution_Windows_Full.zip` — `list` / `show` / `add` / `remove` /\n`pin` / `update` / dispatch smoke (`vm`, `disk`, `login`, `config`,\n`agent`, `extension`) all pass, no CLIPS#522-shape \"could not be found\nby any extension loader\" errors, `registry.json` never gains a\n`location` key.\n\n**History Notes**\n\n[Extension] `az extension list`: Include bundled extensions and expose\non-disk `Location` field\n[Extension] `az extension show`: Expose on-disk `Location` field\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-23T15:03:18+10:00",
          "tree_id": "cf472a4a12253b6a246c49d9925ecf2eefc4b76d",
          "url": "https://github.com/Azure/azclips/commit/9ace2ec11b7d211c8c5c7c1f07baef54f4e0ed68"
        },
        "date": 1784783687432,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 224133786.82,
            "unit": "ns",
            "range": "± 3412839.7530754516"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 227035903.46,
            "unit": "ns",
            "range": "± 5673068.632680782"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 225317749.22,
            "unit": "ns",
            "range": "± 5128528.469089641"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "maddisondas@microsoft.com",
            "name": "MaddyMicrosoft",
            "username": "MaddyMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b5d3e2863162ea3775b90bca7860a434ba700779",
          "message": "[Core] Fix argument-name routing and user-visible error labels (#594)\n\n**Related command**\n\nAll commands (argument parsing layer). PS side affects any cmdlet where\nthe parameter name differs from the processor property (e.g. `-ObjectId`\nbound to a `Group` property). CLI side affects every command that emits\na parameter-related error or verbose log.\n\n**Description**\n\nTwo independent argument-parser bugs surfaced while onboarding a new\ncmdlet. Fixing both in one PR because they share the same test-project\nsetup and both trace back to the parsers passing the wrong string\ndownstream.\n\n**Fix 1 — `PSArgumentParsingProvider` routing (functional bug)**\n\n`PSArgumentParsingProvider.ParseArguments` was inserting each argument\ninto the output dictionary under the raw incoming key (the PS parameter\nname from `BoundParameters`). Downstream,\n`ArgumentParsingService.SetProperty` calls\n`processor.GetType().GetProperty(propertyName)` on that key, so whenever\nthe PS parameter name differed from the underlying processor property\nname the reflection lookup returned `null` and the whole call failed\nwith `The property 'X' does not exist on processor '...'`. The CLI\nprovider has always translated the incoming key to `metadata.Name`\nbefore writing it out — the PS provider was the only one skipping that\nstep.\n\nFixed by reading `metadata.Name` off the resolved `ParameterMetadata`\nand using it as the output-dict key. Unknown keys (no metadata) still\npass through unchanged so we don't mask legitimate \"unknown parameter\"\nerrors elsewhere.\n\n**Fix 2 — `CLIArgumentParsingProvider` error labels (UX bug)**\n\n`CLIArgumentParsingProvider` was building `ArgumentException` messages\nand verbose log lines from `parameterMetadata.Name`, which is the\nprocessor property name — not what the user typed. A user who ran `az\nfoo -g mygroup` and hit a validation failure would see an error naming\n`Group` (or whatever internal property) rather than `-g` or `--group`.\nSame story for aliases: users who typed the short form saw the canonical\nform in the error.\n\nFixed by capturing `originalInput = kvp.Key` at the top of the loop,\nbefore the alias-resolution branch reassigns `key`, and threading\n`originalInput` through `ParseArgument`, `ConvertToCollection`,\n`ConvertScalar`, and `PropertyParser.ConvertToType`. The `propertyName`\nparameter on those helpers is used exclusively for user-facing text —\nnever for lookups or control flow — so passing the literal input string\nis safe throughout. Errors and verbose lines now name exactly what the\nuser typed.\n\nNeither change alters public API. No behavioural change beyond the\nerror/log text on the CLI side and unblocking previously-broken cmdlets\non the PS side.\n\n**Testing Guide**\n\n```bash\n# From repo root\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj\ndotnet test tests/Cli.Tests/Cli.Tests.csproj\n```\n\n- `tests/Cmdlets.Tests/PSArgumentParsingProviderTests.cs` (new) — 4\ntests covering the PS-name-to-processor-property translation,\nunknown-key passthrough, and array parameter handling.\n- `tests/Cli.Tests/ArgumentParsing/CliArgumentValidationTests.cs` — 3\nnew tests covering missing scalar value, scalar conversion failure, and\nshort-alias-in-error-message. Existing\n`ValidateArguments_MissingRequired_ReportsCliFlagName` unchanged.\n\nAlso verified by temporarily reverting each fix and confirming the new\ntests fail as expected before re-applying.\n\nTwo pre-existing failures on clean `main`\n(`CompatibilityGuardTests.CheckAssembly_StampedFirstPartyExtension_IsIncompatibleAt0_9_0`\nand\n`ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`)\nare unrelated to this change and reproduce without any of these edits\napplied.\n\n**History Notes**\n\n[PowerShell] Argument parser: Route cmdlet parameters to their processor\nproperty name so parameters whose PS name differs from the backing\nproperty no longer fail with a missing-property error\n[CLI] Argument parser: Use the flag or alias the user typed in error\nmessages and verbose logs instead of the internal processor property\nname\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed. — n/a, no doc-visible behaviour changed",
          "timestamp": "2026-07-23T17:49:50+10:00",
          "tree_id": "98002ae6b3833fe7453539dad5e978403e2439cb",
          "url": "https://github.com/Azure/azclips/commit/b5d3e2863162ea3775b90bca7860a434ba700779"
        },
        "date": 1784794092634,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 230532488.01,
            "unit": "ns",
            "range": "± 3813731.743600103"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 233426498.69,
            "unit": "ns",
            "range": "± 4429386.654336434"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 230416257.43,
            "unit": "ns",
            "range": "± 4174852.5347172846"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "088027bf6672da1880260d1fe26bb147644485d0",
          "message": "[Telemetry] Honor telemetry.enabled changes mid-session (#592)\n\n**Related command**\n\n`Set-AzConfig -Key telemetry.enabled`, `az config set --key\ntelemetry.enabled`, plus every cmdlet / `az` command that emits\ntelemetry.\n\n**Description**\n\nFixes #470.\n\n`PSTelemetryProvider` (and its CLI/TUI siblings) read\n`TelemetryOptions.Enabled` in the constructor and cached the decision by\neither building the OpenTelemetry pipeline or short-circuiting. Because\nthe PS DI container lives from `Import-Module` to `Remove-Module`, that\ndecision was frozen for the whole session — so `Set-AzConfig\ntelemetry.enabled false` had no effect on subsequent cmdlets until the\nmodule was reloaded. The mirror direction (start disabled → enable\nmid-session) was equally broken.\n\nThe check now lives in `TelemetryService.StartActivity` /\n`StartRootActivity`, which read `TelemetryOptions.Enabled` via\n`IConfigurationService` on every call. The three provider ctors always\nbuild the pipeline; the runtime gate is the single source of truth. Both\ntoggle directions now take effect on the very next invocation without a\nmodule reload.\n\nNothing else can leak: the existing `DefaultActivityNameFilterProcessor`\nallowlist already drops any span (e.g. HTTP-client instrumentation)\nwhose OperationName isn't produced by our own `ITelemetryService.Start*`\nmethods, so gating those methods is a complete fix.\n\nPer-call cost is one\n`IConfigurationService.GetConfiguration<TelemetryOptions>()` call\n(warm-cache ~5–15 µs), well under 1 % of any cmdlet that touches the\nnetwork.\n\n**Scope**\n\n| Host | Container lifetime | Observable before | After fix |\n| --- | --- | --- | --- |\n| PowerShell | `Import-Module` → `Remove-Module` (hours+) | **Yes** —\nthis issue | Live toggle both directions |\n| TUI | Interactive REPL (long) | Latent (same code shape) | Live toggle\nboth directions |\n| CLI | One process per `az` invocation | No — new process = fresh\nconfig read | Parity change (see manual repro) |\n| Agent | Medium-lived, inherits CLI provider | Latent | Parity change |\n\n**Testing Guide**\n\nAutomated:\n\n```powershell\ndotnet build azclips.slnx -c Debug\ndotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj  # 119 pass, incl. 6 new gate tests\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj                # 85 pass\ndotnet test tests/Cli.Tests/Cli.Tests.csproj                        # 508 pass (1 pre-existing E2E failure requires packaged az binary; identical on main)\n```\n\nNew coverage in\n`tests/Core.Telemetry.Tests/TelemetryServiceEnabledGateTests.cs`:\n\n- `StartActivity` / `StartRootActivity` return `null` when\n`Enabled=false`, non-null when `true`.\n- Toggle `true → false` mid-life takes effect on the next call (Teresa's\nscenario).\n- Toggle `false → true` mid-life takes effect on the next call (mirror\nscenario).\n\nManual repro (PS, with a temporary\n`Console.Error.WriteLine(\"[TELEMETRY-PROBE] recorded {name}\")` in\n`TelemetryService.StartActivity` for observability):\n\nBefore the fix, Teresa's scenario:\n\n```text\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = true [global config]\n\nPS> Set-AzConfig -Key telemetry.enabled -Value false\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded SetConfigCommand.ProcessAsync\n\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root      # ← BUG: probe still fires\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = false [global config]\n```\n\nAfter the fix, same sequence:\n\n```text\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = true [global config]\n\nPS> Set-AzConfig -Key telemetry.enabled -Value false\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded SetConfigCommand.ProcessAsync\n\nPS> Get-AzConfig -Key telemetry.enabled\ntelemetry.enabled = false [global config]                # ← FIXED: no probe lines\n```\n\nMirror direction (fresh session, config already `false`):\n\n```text\nPS> Get-AzConfig -Key telemetry.enabled                   # no probe (gate short-circuits)\ntelemetry.enabled = false [global config]\nPS> Set-AzConfig -Key telemetry.enabled -Value true       # no probe (Set ran while disabled)\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root       # ← FIXED: mirror direction also live\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = true [global config]\n```\n\nCLI verification (three consecutive `az` invocations with the same\nprobe) confirms the CLI change is parity-only, not load-bearing: after\n`az config set --key telemetry.enabled --value false`, the very next\n`az` invocation is a fresh process and its `CLITelemetryProvider` ctor\nreads the new value at startup — no probe output. The bug never had time\nto manifest in a single-shot CLI process.\n\n**History Notes**\n\n[Telemetry] Honor `telemetry.enabled` config changes without requiring a\nPowerShell module reload\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-27T11:30:36+10:00",
          "tree_id": "d38e04cd6803ab981031a781aa817bc6051bb224",
          "url": "https://github.com/Azure/azclips/commit/088027bf6672da1880260d1fe26bb147644485d0"
        },
        "date": 1785116468675,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 216644877.59,
            "unit": "ns",
            "range": "± 8321438.602434339"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 215772232.59,
            "unit": "ns",
            "range": "± 2258124.3603956993"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 213883920.92,
            "unit": "ns",
            "range": "± 3290766.2855660035"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "fbb38eb060fef1ac573e322efa914be6791b3ba0",
          "message": "{Benchmark} Compute extension benchmarks (#589)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\nAdd benchmarks for compute extension covering CRUD scenarios\nBenchmark filtering based on file changes\nParallelize benchmark runs between extensions\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>\nCopilot-Session: 57a95271-c097-4e9e-a319-e63a70ff8c25",
          "timestamp": "2026-07-27T13:23:26+10:00",
          "tree_id": "e7bb471d2db93f88ac364aada41fa4de09c5810b",
          "url": "https://github.com/Azure/azclips/commit/fbb38eb060fef1ac573e322efa914be6791b3ba0"
        },
        "date": 1785123403660,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 214172190.29,
            "unit": "ns",
            "range": "± 2020590.042477248"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 214462214.67,
            "unit": "ns",
            "range": "± 2457860.9579846566"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 214033208.15,
            "unit": "ns",
            "range": "± 2687700.7141111493"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "aryasadeghi@microsoft.com",
            "name": "aryasadeghi1",
            "username": "aryasadeghi1"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6506ee39d8d5b51f42ab9ef6c6f98a69457bd77b",
          "message": "{Tests} Add scenario test HTTP record/playback foundation (#562)\n\nRecordPlaybackInterceptor fills the tracing-step slot `Engine.cs`\nreserves for record/playback. Record mode captures each request/response\ninto a `SessionRecord` (Authorization is never stored); playback serves\nrecorded responses with no network and fails unmatched requests with a\ndiagnostic listing the remaining entries.\n\n`ScenarioHost` runs commands in-process through the real dispatcher and\nargument binders for both surfaces with stubbed auth.\n\nPOC: The parity tests show group create and `New-AzResourceGroup` emit\nidentical HTTP, so a single recording covers both CLI and PowerShell,\nincluding record-on-CLI then replay-on-PowerShell.\n\nPart of the scenario testing framework work (Addresses #470).\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-27T14:02:11+10:00",
          "tree_id": "8cafcf51d4b077c6d1d19e3179f14c83be373ea7",
          "url": "https://github.com/Azure/azclips/commit/6506ee39d8d5b51f42ab9ef6c6f98a69457bd77b"
        },
        "date": 1785125788888,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 226298662.73,
            "unit": "ns",
            "range": "± 6834991.008081872"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 224912857.87,
            "unit": "ns",
            "range": "± 4863118.156069902"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 223071992.31,
            "unit": "ns",
            "range": "± 4898633.431079543"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "22f669f323c85efd6764ee0d885277da58be1535",
          "message": "{Core} Command not found error message adjustments (#554)\n\n**Related command**\n\n`az agent`, `az agent tool`, `az agent tool resource`\n\n**Description**\n\nTyping an Agent command group that has no runnable leaf produced the raw\ninternal error and then handed off to the AI agent:\n\n```\n$ az agent\n[ERROR] Command 'agent' not found for client type 'CLI'.\n  ⠋ Analyzing error root cause using AI ...\n```\n\ninstead of the standard, fast `CommandNotFound` that other groups (`az\ndeployment`, `az vm`, `az group`) already return:\n\n```\n$ az agent\nerror: [CommandNotFound] 'az agent' is not a recognized command. Run 'az agent --help' to see available commands.\n```\n\n**Cause.** Built-in verbs always resolve in `ExtensionDispatcher`, so\nthey skip the verb-only fallback that #525 added for installed\nextensions. The dispatcher then dispatched the non-runnable group to the\nextension, whose `ExtensionIndex` threw a generic `KeyNotFoundException`\nthat the orchestrator routes to the AI agent.\n\n**Fix.** A Core-level change to the dispatcher, mirroring #525. Add\n`LeafIndex.IsCommandGroup(verb, path)` — true when a longer leaf exists\nunder `(verb, path)` but the exact leaf does not — and gate dispatch on\nit, throwing `CommandNotFoundException` (`code=CommandNotFound`, exit 4)\nfor command groups. This now covers the built-in path that #525 could\nnot reach. A runnable root verb like `az login` (whose only leaf is its\nroot) is unaffected. No Agent extension code is touched; `az agent` is\nthe observable symptom because it is the built-in group exercised here.\n\n**Scope of the behavior change.** This is a general Core fix, not\nAgent-specific. The gate fires for any resolved built-in verb whose\ninvocation lands on a command group with no runnable root leaf — today\nthat is `agent`, `config`, `extension`, and `profile` (each seeded with\nchild leaves, none exposing a runnable root command). All four now\nreturn the standard `CommandNotFound` instead of falling through to the\nAI agent, which is the intended consistency fix (mirrors #525 for the\nbuilt-in path). `az agent` is simply the group reported in the bug.\nRunnable root verbs like `az login` / `az logout` (whose only leaf is\nthe root) are unaffected. Config-owned command *behavior* is untouched —\nonly the not-found routing is unified.\n\n**Testing Guide**\n\nBuilt from `azclips.slnx` and run against\n`artifacts/Release/Core.Cli/az.exe`:\n\n| Command | Result |\n| --- | --- |\n| `az agent` | `[CommandNotFound] 'az agent' ... Run 'az agent --help'`\n|\n| `az agent tool` | `[CommandNotFound] 'az agent tool' ...` |\n| `az agent tool resource` | `[CommandNotFound] 'az agent tool resource'\n...` |\n| `az agent tool env` (valid leaf) | executes normally, returns JSON |\n| `az agent tool resource find --name test --top 1` (valid leaf) |\nexecutes normally |\n| `az login --subscription-id ...` | resolves to the command (not a\ngroup); no regression |\n| `az logout` | executes normally |\n\nAutomated:\n\n- `dotnet test tests/Core.Extensions.Tests` — `LeafIndexTests` +6\n(`IsCommandGroup`)\n- `dotnet test tests/Cli.Tests --filter ExtensionDispatcher` — +5 (group\ngate, valid leaf, runnable root)\n\n**Known follow-up (out of scope):** a typo on a full leaf, e.g. `az\nagent tool resource findxxx`, still returns the old `client type 'CLI'`\nerror. That is a leaf-typo path, distinct from the command-group case\nfixed here, and applies to all extensions.",
          "timestamp": "2026-07-28T11:05:30+10:00",
          "tree_id": "343f9a5546daeca221cf7caf9fc40cb757e8eeaf",
          "url": "https://github.com/Azure/azclips/commit/22f669f323c85efd6764ee0d885277da58be1535"
        },
        "date": 1785201531065,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 230352676.21,
            "unit": "ns",
            "range": "± 5114822.282354839"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 234684579.3,
            "unit": "ns",
            "range": "± 5434616.208843264"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 229408557.52,
            "unit": "ns",
            "range": "± 5170796.237126581"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "662ed09b05d145e320f50aff2c1cd6a2928a0da9",
          "message": "{Autocompletion} rename $matches to $completionResultMatches in completion script (#616)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\nThis PR fixes https://github.com/Azure/azclips/issues/613 by renaming\nthe $matches variable to a non built in variable name. No remaining\nPSScriptAnalyzer warnings in this script.\n\nCo-author [bjompen](https://github.com/bjompen)\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-28T18:36:22+10:00",
          "tree_id": "9983161b00ee15eb64492a6c079f1cc57a9efa5e",
          "url": "https://github.com/Azure/azclips/commit/662ed09b05d145e320f50aff2c1cd6a2928a0da9"
        },
        "date": 1785228639635,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 215859354.81,
            "unit": "ns",
            "range": "± 2916786.4364503375"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 216932703.82,
            "unit": "ns",
            "range": "± 2792581.2525433116"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 215905592.67,
            "unit": "ns",
            "range": "± 2497138.287067722"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "cd8b8efc3cdbfc3fbc6eb9005453c6b077a890ba",
          "message": "{DevOps} Remove dev branch references from CI workflows (#625)\n\n**Related command**\n\nN/A\n\n**Original PR**\n\n- #563 (https://github.com/Azure/azclips/pull/563)\n\n**Description**\n\nRemoves CI branch targeting that still referenced `dev` after the branch\nretirement.\n\nChanges in this PR:\n- Removed `dev` from GitHub Actions PR triggers in `build.yml`,\n`build-and-test.yml`, and `benchmark.yml`.\n- Removed `dev` from Azure Pipelines trigger include list in\n`azclips-beta-build.yml`.\n- Deleted `.github/workflows/backport-pr.yml`, which was designed around\nmain/dev backport directions.\n\n**Testing Guide**\n\n- Ran `git diff --check` on the resulting patch.\n\n**History Notes**\n\n{DevOps} Remove dev branch references from CI workflows\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\nCopilot-Session: 234beeee-b64b-41b9-b3fd-cdeea2bf0416",
          "timestamp": "2026-07-30T13:43:50+10:00",
          "tree_id": "bf643e5f502a411aaaafa892fefe608d27948e3e",
          "url": "https://github.com/Azure/azclips/commit/cd8b8efc3cdbfc3fbc6eb9005453c6b077a890ba"
        },
        "date": 1785383879620,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 227415748.64,
            "unit": "ns",
            "range": "± 6097149.379696544"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 226745607.27,
            "unit": "ns",
            "range": "± 2976001.7165421075"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 225408402.66,
            "unit": "ns",
            "range": "± 2976260.6825739476"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "aryasadeghi@microsoft.com",
            "name": "aryasadeghi1",
            "username": "aryasadeghi1"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "dd1f7edf63f96ceefd4443c86bda5eef522bdc49",
          "message": "{Test} Add recording sanitisers and playback normalisation (#622)\n\nRecordings are scrubbed before they reach disk: subscription and tenant\nids become a fixed placeholder, bearer tokens are redacted, and the\nAuthorization and api-key headers are dropped. The same rewrites run on\noutgoing requests during playback so sanitised recordings still match,\nincluding under a different subscription. The list is central and\nextensible per scenario.\n\nAlso makes the interceptor safe for orchestrators that fan out requests\nin parallel, with tests driving record and playback from many threads.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-30T15:14:44+10:00",
          "tree_id": "d7b7512572a774a8e3e93ed2dd260856a1b9bf19",
          "url": "https://github.com/Azure/azclips/commit/dd1f7edf63f96ceefd4443c86bda5eef522bdc49"
        },
        "date": 1785389283005,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 220366674.84,
            "unit": "ns",
            "range": "± 3524855.9443974635"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 220791426.73,
            "unit": "ns",
            "range": "± 2971390.9395868746"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 219673900.46,
            "unit": "ns",
            "range": "± 3293910.6632997044"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9cf355ae687974088e427ec06e499786e26a8cf3",
          "message": "{Benchmark} Skip extension loading for builtin extensions (#626)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n- Skip attempting to staging builtin extensions during benchmarking and\nuse binary built-in binaries only.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-30T16:14:44+10:00",
          "tree_id": "3a29c8d5149b8a84f6ee8998e1493cd75740cb17",
          "url": "https://github.com/Azure/azclips/commit/9cf355ae687974088e427ec06e499786e26a8cf3"
        },
        "date": 1785392903208,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 215749552.86,
            "unit": "ns",
            "range": "± 6215473.560205236"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 217023915.8,
            "unit": "ns",
            "range": "± 8457329.608768899"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 217991162.03,
            "unit": "ns",
            "range": "± 26036320.757515136"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9c369859c0da432878ff9a979590b48b2d429fbc",
          "message": "{Help} Add help tools to solution file (#629)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Fixes IDE not building tools project\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-31T08:41:01+10:00",
          "tree_id": "dde5f716e8dab937e4be6b131f9bd480ffe0bb94",
          "url": "https://github.com/Azure/azclips/commit/9c369859c0da432878ff9a979590b48b2d429fbc"
        },
        "date": 1785452106524,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 220911077.57,
            "unit": "ns",
            "range": "± 4052733.5377099984"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 221912757.84,
            "unit": "ns",
            "range": "± 6667357.16194949"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 219615597.9,
            "unit": "ns",
            "range": "± 2839809.425538784"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a0869b9c09226d742455a2982ae58dcca4614d48",
          "message": "{Core} Adjust raw console call sites to use output streams (#638)\n\n**Related command**\n\nExtension discovery / help path — affects the diagnostic surface any\nconsumer of `IEngine.OpenDiscoverySession` sees on extension-load\nfailure.\n\n**Description**\n\nFixes [Azure/CLIPS#545](https://github.com/Azure/CLIPS/issues/545) —\nfollow-up from PR #575 (issue #493). Was §6 bullet 3 of that PR's\nplanning doc.\n\nPR #575 harmonized diagnostic prefixes to `LEVEL: message`, routed\nwarn/info/verbose/debug to stderr, and level-gated everything through\n[`core.logging.level`](docs/azure-cli/docs-conceptual/config.md)\n(default: `warn`). A handful of `Console.*` sites were left unaddressed\nand still write raw to stdout/stderr regardless of `core.logging.level`\nor the caller's `OutputStreams` binding.\n\nChanges:\n\n- **[`DiscoverySession.cs`](src/Core/Core.Engine/DiscoverySession.cs)**\n— three `Console.Error.WriteLine` catch sites (L87, L116, L141) now\nroute through the session's `outputStreamAdapter.Warn(...)`. Prefix\nfollows PR #575 D6 (`[{nameof(DiscoverySession)}]`, matches\n[`MsalAuthService`](src/Core/Core.Authentication/MsalAuthService.cs#L52)).\nSeverity is `Warn` because the session degrades gracefully: it\naccumulates successfully-loaded extensions and returns them even when\none fails — the textbook definition of a warning. Matches\n[azure-cli-python\nprecedent](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py)\n(extension-load failures at `WARNING`). Under the new `warn` default\nthey surface; under `error` they stay silent.\n- **[`FileExporter.cs`](src/Core/Core.Telemetry/FileExporter.cs#L93)** —\nstays raw, comment added. `Core.Telemetry` sits below `Core.Runtime` in\nthe layer stack; routing through `ILoggingService` would invert the\ndependency and risk recursion if any future logger path opens a\ntelemetry activity. Matches OpenTelemetry `SelfLog` / Serilog `SelfLog`\nconvention.\n-\n**[`CompletionGenerator.cs`](src/CLI/Completions/CompletionGenerator.cs)**\n— stays raw, class-level `<remarks>` added covering all five\n`Console.WriteLine` sites. Output is shell-completion protocol payload\nconsumed by tab-completion scripts, not diagnostic logging; must not be\nprefixed, level-gated, or stream-routed.\n\n**Behavior shift worth noting**\n\nThree of the four `DiscoverySession` call sites deliberately bind every\ndiagnostic stream to a no-op sink for silent internal probing:\n\n| Caller | `WriteWarning` binding | Post-change surface |\n| --- | --- | --- |\n|\n[`CompletionGenerator.cs`](src/CLI/Completions/CompletionGenerator.cs#L395)\n| no-op | silent |\n|\n[`DiscoveryBackedExtensionRegistry.cs`](src/Extensions/Extension/DiscoveryBackedExtensionRegistry.cs#L127)\n| no-op | silent |\n|\n[`CliExtensionServiceRegistration.cs`](src/CLI/CliExtensionServiceRegistration.cs#L128)\n| no-op | silent |\n|\n[`RuntimeHelpDispatcher.cs`](src/CLI/Help/RuntimeHelpDispatcher.cs#L263)\n| `_streams.WriteWarning` | visible on stderr |\n\nBefore this PR, the raw `Console.Error.WriteLine` fired unconditionally\nand leaked past every caller's \"stay quiet\" contract. After this PR,\nextension-load failures at the three no-op call sites become silent, and\nonly the help dispatcher's discovery path surfaces the line. This is the\nintended outcome of CLIPS#545 — routing through `ILoggingService` puts\ndiagnostic control back in each caller's hands.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx` — clean.\n- `dotnet test tests/Core.Engine.Tests/Core.Engine.Tests.csproj` — 2/2\npass.\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` — 516/524 pass, 7\nskipped, 1 pre-existing E2E failure\n(`ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`\n— CLI-binary path resolution on Windows) confirmed unrelated by stashing\nmy changes and reproducing on clean tree.\n- **Manual**: corrupted `Microsoft.Azure.Azclips.Extensions.Compute.dll`\nin-place (4 null bytes, keeping the filename so `BuiltinExtensionLoader`\nstill enumerates it) and ran `az vm --help`. Result: `WARNING:\n[DiscoverySession] Failed to load extension: Bad IL format.` on stderr,\nfollowed by `ExtensionNotFound` from dispatch. Other extensions loaded\nfine; graceful-degradation preserved.\n\n**History Notes**\n\n{Core} Route DiscoverySession extension-load failures through\nILoggingService.Warn\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-03T14:21:59+10:00",
          "tree_id": "85c674846d0a2cf3dbc46142c1233f2cd81b2720",
          "url": "https://github.com/Azure/azclips/commit/a0869b9c09226d742455a2982ae58dcca4614d48"
        },
        "date": 1785731744347,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 217440729.02,
            "unit": "ns",
            "range": "± 2145886.2955571054"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 220652294.91,
            "unit": "ns",
            "range": "± 3158410.777217626"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 217418765.56,
            "unit": "ns",
            "range": "± 3272360.843727114"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a1ab50be4b53ef1cde61530d9f25e2892a3179c4",
          "message": "{Telemetry} Add Jaeger for local telemetry monitoring (#605)\n\n- Add Jaeger for local telemetry monitoring\n- Debug build emits to local by default \n- Release build emits to remote by default\n- `AZCLIPS_TELEMETRY_TARGET` env variable overrides which telemetry sink\nto emit to regardless of build\n\n<img width=\"3836\" height=\"1867\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/8887a671-c6b9-4d68-8c6c-28b75c2ea35d\"\n/>\n<img width=\"3838\" height=\"1859\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/3f573fb1-5644-4996-a882-9e154ce15de3\"\n/>\n<img width=\"3836\" height=\"1873\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/e8897778-b5bf-4f1f-a997-f9e834e3e235\"\n/>\n<img width=\"3833\" height=\"1858\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/d69a3b27-0762-4a3f-9ad9-eee109735810\"\n/>\n\n---------\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>\nCopilot-Session: f8b1e1b9-860e-4341-9317-fddc46f8bbc9",
          "timestamp": "2026-08-04T10:02:13+10:00",
          "tree_id": "7a1442a0cc650dd708d1324844b0cb774a0617c9",
          "url": "https://github.com/Azure/azclips/commit/a1ab50be4b53ef1cde61530d9f25e2892a3179c4"
        },
        "date": 1785802527409,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 216713069.04,
            "unit": "ns",
            "range": "± 4590482.623115411"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 217251712.52,
            "unit": "ns",
            "range": "± 3435614.895561458"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 215525183.03,
            "unit": "ns",
            "range": "± 2064669.8826849323"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "776cba576b3b747e16f4d5bca469cff641a6d6c7",
          "message": "{Extensions} Add process caching for builtin extension loader (#642)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Add caching for builtin extension loader while loading managed\nassembly.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCopilot-Session: 32164222-c594-431a-b78d-7a1bc7bce947",
          "timestamp": "2026-08-04T00:25:23Z",
          "tree_id": "e24837e3d7bceaa789a265a623532ec90e8a15e3",
          "url": "https://github.com/Azure/azclips/commit/776cba576b3b747e16f4d5bca469cff641a6d6c7"
        },
        "date": 1785804012633,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 218354058.34,
            "unit": "ns",
            "range": "± 2027167.1831995798"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 219225060.03,
            "unit": "ns",
            "range": "± 2669343.465524902"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 217483423.88,
            "unit": "ns",
            "range": "± 2535260.9165956867"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d32e45c6b500af660d79a50958575434021be431",
          "message": "{Core} Centralize console diagnostic rendering into `ConsoleSink` (#639)\n\n**Related command**\n\nConsole diagnostic rendering — internal.\n\n**Description**\n\nFixes [Azure/CLIPS#543](https://github.com/Azure/CLIPS/issues/543)\n(follow-up to PR #575).\n\nThree surfaces (`ConsoleLoggingService`, `ConsoleOutputAdapter`,\n`BuildStreams` in `Program.cs`) each independently picked a color,\nstream, and redirection guard. Refactor: introduce `ConsoleSink` as the\nsingle owner, delegate all three surfaces to it. Byte-preserving for\nexisting diagnostic paths; one intentional visual delta on the\nstructured-error path (see below).\n\n- **New** [`ConsoleSink.cs`](src/Core/Core.Runtime/ConsoleSink.cs) —\nlow-level primitives (`EmitStderr` / `EmitStdout`) + high-level\ndiagnostic methods (`Error` / `Warn` / `Info` / `Verbose` / `Debug`)\nwith canonical `LEVEL:` prefix.\n- All three surfaces collapse to one-line sink calls. `ColorPolicy` →\n`internal`.\n- `RecordTelemetry` **preserved verbatim** inside\n`ConsoleLoggingService` — not moved into the sink. Wiring it uniformly\nwould silently change telemetry semantics; keep for a separate design\ndiscussion.\n\n**One intentional visual delta**\n\n`Program.EmitStructuredError` previously wrote raw `error: [code]\nmessage` (lowercase, no color) directly to `Console.Error`. Routed\nthrough `ConsoleSink.Error`, so it now renders as `ERROR: [code]\nmessage` (red on non-redirected stderr, `NO_COLOR` / `FORCE_COLOR` /\nredirection guard respected via `ColorPolicy`).\n\nDocumentation is updated in the same PR to match — six pre-existing\nreferences to `error:` / `Error:` in\n[`eng-docs/spec/extension-management.md`](eng-docs/spec/extension-management.md),\n[`eng-docs/howto/extension-management.md`](eng-docs/howto/extension-management.md),\n[`wiki/onboarding-guide.md`](wiki/onboarding-guide.md), and\n[`wiki/zero-to-hero-guide.md`](wiki/zero-to-hero-guide.md) all move to\n`ERROR:`. Also resolves a pre-existing internal drift in\n`extension-management.md` (`error:` vs `Error:` on different lines).\nJSON output (`--output json`) is **unchanged** — that's the public\npayload shape, not a diagnostic prefix.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx` — clean.\n- New: `ConsoleSinkTests` — 9/9 pass.\n- Safety net (byte-preserving for diagnostic paths):\n`StreamRoutingIntegrationTests` 4/4, `TuiClassifyLineTests` 11/11,\n`CliLoggingPolicyTests` 11/11.\n- Full `Cli.Tests`: 525/533 pass (1 pre-existing unrelated failure).\n- Manual: reproduced CLIPS#545 corrupt-DLL test — `WARNING:` line\nbyte-identical to pre-refactor; `az vmss --help` now shows `ERROR:` red\n(previously `error:` white).\n\n**History Notes**\n\n{Core} Centralize console diagnostic rendering into `ConsoleSink`\n\n---\n\n- [x] Title prefix / release note conventions.\n- [x] Tests added for behavior changes.\n- [x] Docs updated where relevant.",
          "timestamp": "2026-08-04T11:41:35+10:00",
          "tree_id": "ec4397a0239b1aa19bc6a34a74a2a795f8f8c3b5",
          "url": "https://github.com/Azure/azclips/commit/d32e45c6b500af660d79a50958575434021be431"
        },
        "date": 1785808543395,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 221219958.86,
            "unit": "ns",
            "range": "± 3341552.980105005"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 221638829.82,
            "unit": "ns",
            "range": "± 4747411.770591105"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 219571142.97,
            "unit": "ns",
            "range": "± 2833930.3238042295"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "msarfraz@microsoft.com",
            "name": "mansoor sarfraz",
            "username": "msarfraz"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b2280c10f017e705318be61f21c4a8e3f397af66",
          "message": "{core} version error fixed for unrecognized arguments (#648)\n\nError message updated for unrecognized arguments\n<img width=\"743\" height=\"148\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/966ff2f0-1aa0-4b0a-8e44-8967cb21eb64\"\n/>\n\nCo-authored-by: Mansoor Sarfraz <msarfraz+microsoft@microsoft.com>",
          "timestamp": "2026-08-04T14:22:57+10:00",
          "tree_id": "c35be5a5fea0d0109e8acf2200139d7d929a3ffa",
          "url": "https://github.com/Azure/azclips/commit/b2280c10f017e705318be61f21c4a8e3f397af66"
        },
        "date": 1785818216633,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 221448703.86,
            "unit": "ns",
            "range": "± 4801968.725007094"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 221876071.84,
            "unit": "ns",
            "range": "± 4564946.987535187"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 220629400.69,
            "unit": "ns",
            "range": "± 4258361.796458292"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7f017417140445116c6eeee33dd1903ca15e33c6",
          "message": "[CLI] Add `core.output.color` config option (auto/always/never) (#649)\n\n**Related command**\n\n`az config set core.output.color`, `az config get core.output.color`,\n`az config list --all`.\n\n**Description**\n\nFixes [Azure/CLIPS#604](https://github.com/Azure/CLIPS/issues/604)\n(follow-up to PR #639 / CLIPS#543, child of CLIPS#542).\n\nAdds `core.output.color` — three-state persistent color preference for\nCLI diagnostic rendering, matching `git config color.ui` / `cargo\nterm.color` shape (`auto | always | never`).\n\n- **New config key** in\n[`ConfigurationSchema.cs`](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs)\nwith `AllowedValues=[auto,always,never]`, default `auto`.\n- **`ColorMode` enum + `Parse` + `Initialize`** in\n[`ColorPolicy.cs`](src/Core/Core.Runtime/ColorPolicy.cs). `Program.Main`\nresolves the mode once from config after DI init and calls\n`ColorPolicy.Initialize(mode)` before `BuildStreams`. No per-emission\nconfig lookup.\n- **Precedence** (top to bottom): `NO_COLOR` env → `FORCE_COLOR` env →\n`core.output.color = always` → `core.output.color = never` → `TERM=dumb`\n→ target-stream redirected → color on. Env sits above config because env\nis per-invocation override; config is persistent policy. Standard\nconvention (git / cargo / npm).\n- **`auto` (default)** preserves pre-config behavior byte-for-byte.\nUsers who never touch the setting see no change.\n- **CLI-scoped**. PowerShell coloring is controlled by the PS host\n(`$PSStyle` / `$Host.PrivateData`) and is not affected by this setting;\nseparate CLIPS issue if wanted later.\n- **Docs updated**: user-facing table in\n[`docs/azure-cli/docs-conceptual/config.md`](docs/azure-cli/docs-conceptual/config.md)\nand design notes in\n[`eng-docs/spec/config-implementation.md`](eng-docs/spec/config-implementation.md)\n§10.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx` — clean.\n- New: `ColorPolicyTests` — 16/16 pass (9 `Parse` cases + 7\nprecedence-matrix cases).\n- Safety net: `ConsoleSinkTests` 9/9, `StreamRoutingIntegrationTests`\n4/4, `TuiClassifyLineTests` 11/11, `CliLoggingPolicyTests` 11/11 — 51/51\ncombined.\n- Full `Cli.Tests`: 551/559 pass (1 pre-existing unrelated\n`ManagedIdentityE2ETests` failure).\n- Manual (CLI):\n- `az config set core.output.color=never; az vm --help` → no color even\non a live terminal.\n- `az config set core.output.color=always; az vm --help 2> log.txt` →\ncolor present in `log.txt` (redirection bypassed).\n- `NO_COLOR=1 az vm --help` with `core.output.color=always` → no color\n(env wins).\n- `az config unset core.output.color; az vm --help` → colors return to\nauto-detected behavior.\n\n**History Notes**\n\n[CLI] `az config set core.output.color`: Add `auto` | `always` | `never`\nfor persistent color preference\n\n---\n\n- [x] Title prefix / release note conventions.\n- [x] Tests added for behavior changes.\n- [x] Docs updated where relevant.",
          "timestamp": "2026-08-04T14:47:20+10:00",
          "tree_id": "13a6e4d1236a17508b5245c23a748c0747774074",
          "url": "https://github.com/Azure/azclips/commit/7f017417140445116c6eeee33dd1903ca15e33c6"
        },
        "date": 1785819669567,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 219850841.8,
            "unit": "ns",
            "range": "± 2910919.308309298"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 219679048.16,
            "unit": "ns",
            "range": "± 2823078.3572948487"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 219178903.04,
            "unit": "ns",
            "range": "± 3136765.2895860947"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "0b437ec242e2e393a78e5980e2e7f54fd169e33c",
          "message": "{CLI} Add global params handler (#651)\n\nFixes [Azure/CLIPS#519](https://github.com/Azure/CLIPS/issues/519). \n\nConsolidates six ad-hoc global-flag parsing sites into a single\n`GlobalArgParser`, and removes control-flow-via-exceptions from the CLI\ncold path.\n\n## What ships\n\n- **New**: `GlobalArgParser.Parse(ReadOnlySpan<string>) -> GlobalArgs`\n(`src/CLI/Parser/`). One pass, classifies `--debug`, `--output[=]`,\n`--no-cache`, the `--help` family, and the `--version` family into a\nrecord.\n- **Deleted**: `ParseDebugFlag`, `ParseOutputFlag`, `ParseNoCacheFlag`\n(Program.cs); `DispatchHelpException`, `DispatchVersionException`,\n`RuntimeExtensionHelpException`.\n- **`Program.Main`**: three `catch` blocks -> two `if` branches\n(`globalArgs.Version`, `globalArgs.Help`). `AZCLIPS_NO_EXTENSION_CACHE`\nenv-var mutation moved to the call site.\n- **`ExtensionDispatcher.Parse`**: help/version classification helpers\ndeleted (~90 lines). Single-purpose: extension routing.\n- **`RuntimeHelpDispatcher.RenderExtensionHelp`**: signature\n`(RuntimeExtensionHelpException)` -> `(IReadOnlyList<string> path)`.\nRenderer no longer depends on parser types.\n- **Tests**: 46 new `GlobalArgParserTests` covering the full precedence\nmatrix; ~13 obsolete `ExtensionDispatcherTests` removed (behavior\ncovered upstream).\n- **Docs**: 7 wiki files refreshed; line-number citations replaced with\nsymbol-name refs.\n\n## Design decisions\n\n- **`GlobalArgParser` is `internal static`**, not a DI singleton. Zero\ndeps, no host-swappable interface -- same shape as `IntrinsicVerbs`,\n`PrintProbe`, `WelcomeBanner`, `ColorPolicy`. Convert only if a real\ndependency shows up.\n- **`HelpRequest` carries only `Path`** (no `Verb` / `IsCommandHelp`).\nVerb = `Path[0]`; the group-vs-command hint (`Path.Count > 1`) is\nderived inside the renderer. Keeps `Core.Cli.Help` decoupled from\n`Core.Cli.Parser`.\n- **`OutputFormat` is a strict enum.** Unknown values (`--output yaml`)\nraise `UsageError` (exit 2) at parse time instead of silently falling\nback to plain. **Behavior change** -- arguably a bug fix.\n- **`--upload-telemetry` and `--print-probe` / `--meow` stay outside the\nparser.** Both are pre-DI process-shape short-circuits, not user-facing\nglobals. Their `if`-blocks at the top of `Program.Main` are kept with a\ncomment explaining why.\n- **Position-insensitive flag matching preserved.** `az group --debug\ncreate` still works. Switching to git-style leading-only is a policy\ndecision for a follow-up.\n- **`--no-cache` env-var mutation lives at the call site**, not in the\nparser. Parser stays pure; the env var is the real mechanism and the\nflag is just sugar for it.\n\n## Validation\n\n- Full solution build: 0 warnings, 0 errors.\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`: all real tests pass (1\npre-existing E2E failure unrelated).\n- End-to-end smoke covers: bare `az`, `az --help`, `az --version`, `az\nversion`, `az extension --help`, `az help extension list`, `az\n--output=badvalue ...`, `az --version foo`.",
          "timestamp": "2026-08-05T14:26:36+10:00",
          "tree_id": "e0bfb372d32393fb7a1589913707c57a10049aeb",
          "url": "https://github.com/Azure/azclips/commit/0b437ec242e2e393a78e5980e2e7f54fd169e33c"
        },
        "date": 1785904913889,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Config",
            "value": 228480012.43,
            "unit": "ns",
            "range": "± 6138588.908433162"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Login",
            "value": 229399890.93,
            "unit": "ns",
            "range": "± 4833469.020391801"
          },
          {
            "name": "Benchmark.Startup.CliWarmStartupBenchmarks.Extension",
            "value": 227866041.4,
            "unit": "ns",
            "range": "± 2641988.938253271"
          }
        ]
      }
    ],
    "Azclips CLI Startup (cold)": [
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "962b6bc594aeb8b1a6e7e2cc27b47235f3119c72",
          "message": "[Benchmark] Benchmark start up time (#401)\n\n## What\nMeasures how long `az` and `pwsh` take to start up (process-create →\ncommand ready),\nseparately from command execution. Adds warm/cold benchmarks, richer\nreporting, and\nCI dashboards to track startup regressions.\n\nLoop for context: [Measuring start up\ntime](https://microsoft.sharepoint.com/:fl:/g/contentstorage/CSP_2e24e7fe-1ac4-440f-bfce-2f4be2ac6e50/IQDPWhjRrPT5QpMalKvnTZ5hAWWJkEXTX3Pw5_zDdkRmRt8?e=OCVdLa&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF8yZTI0ZTdmZS0xYWM0LTQ0MGYtYmZjZS0yZjRiZTJhYzZlNTAmZD1iJTIxX3Vja0xzUWFEMFNfemk5TDRxeHVVQzFBYVZFbklmeE11dWIyS1dMa0RwWkJNRjBzMThzbVJZMTQwM01YclJWUSZmPTAxWTZETVdLNlBMSU1ORExIVTdGQkpHR1VVVlBUVTNIVEImYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lcg%3D%3D)\n\n## How it works\nA new `ProcessorAcquired` engine event fires once the command is\nresolved but before\nit runs. When `AZCLIPS_BENCH_RESOLVE_ONLY=true`, the engine exits right\nthere — timing\nstartup only, with no network or auth side effects. The flag defaults\noff, so\nproduction behavior is unchanged.\n\n## Changes\n- **Engine seam**: `ProcessorAcquired` event +\n`BenchmarkOptions.ResolveOnly`, wired\nvia config and the `AZCLIPS_BENCH_RESOLVE_ONLY` env var.\n`Environment.Exit(0)` only\n  when the flag is set.\n- **Harness** (`tests/Benchmark/Startup/`): spawns real `az`/`pwsh` per\niteration for\n  the `config`, `login`, and `extension` built-ins.\n  - Warm — page cache hot.\n- Cold — page cache evicted first (Linux `drop_caches`; Windows\nstandby-list purge,\nneeds an elevated terminal). Cold is skipped with a reason when eviction\nisn't\n    available, never faked as cold.\n- **Reporting**: adds `Mode` (warm/cold) + `Extension` dimensions to\ncharts and\n  App Insights; warm/cold shown in separate sections.\n- **CI**: new `--product STARTUP` step publishing four dashboards — warm\n(tracked,\n  115% alert) and cold (informational).\n- **Tests**: new `Core.Engine.Tests` verifying `ProcessorAcquired` fires\nonce, before\n  execution, in both CLI and PowerShell paths.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-10T15:16:33+10:00",
          "tree_id": "345b6594b7ab67f144d89b8775255a2ab69203c0",
          "url": "https://github.com/Azure/azclips/commit/962b6bc594aeb8b1a6e7e2cc27b47235f3119c72"
        },
        "date": 1783661230209,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 271600909.18,
            "unit": "ns",
            "range": "± 5744381.517650603"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 272747649.98,
            "unit": "ns",
            "range": "± 6567962.59794929"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 325584688.17,
            "unit": "ns",
            "range": "± 91672288.7632416"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "711696cf8d7ac741290034c73f46b6844a53171e",
          "message": "feat(agent): prettier interactive tool-event rendering via Spectre.Console (#435)\n\n## What\n\nRender the **interactive** agent's tool events through\n**Spectre.Console** so long commands, args, and output wrap and indent\nto the terminal — instead of being crammed onto one line or hard-cut\nmid-token.\n\nBefore, in interactive mode:\n- `▶ powershell({...huge JSON...})` — the entire args JSON on one line\n- `✓ powershell 10025ms · <output>` — output's first line crammed after\nthe `·`\n- `⚠ approve args:` — long commands sliced by byte budget, cutting\nwords/tokens in half\n- long tool output truncated to the **last 20 lines** (dropping the\nhead)\n\n## Changes\n\n- **Tool call/complete rendering** (`ConsoleAgentRenderer`): tool name\non its own line; args printed as indented `key: value` pairs (parsed\nfrom args JSON); output indented and word-wrapped to terminal width.\n- **`ConsoleApprovalPrompt.WriteArgs`**: wrap long commands at\nwhitespace instead of slicing by byte budget, so flags / quoted values\nstay intact.\n- **`SanitizeToolResult`**: drop the 4000-char / last-20-lines\ntruncation. It is **display-only** (never fed to the model — that path\nuses the SDK's raw tool result) and the terminal has scrollback, so the\nold cap only risked dropping the **head** of a long result. ANSI /\noverwrite-run / adjacent-dedupe cleanup is kept.\n- **New config `agent.interactive.verbose`** (bool, default `false`):\nwhen on, the interactive session also shows non-shell tool details\n(`get_command` / `find_*` args and output); off keeps the session\nuncluttered. Documented in `docs/howto/config.md`.\n\n## Scope / non-goals\n\n- **Plan rendering (`RenderPlan`) is unchanged** — it's a flat list that\nneeds no layout engine.\n- **one-shot mode is unaffected** — it prints no tool events (`quiet`),\nso verbose is interactive-only by design (hence the `agent.interactive.`\nnamespace).\n\n## Dependency\n\nAdds `Spectre.Console` 0.57.2 (MIT). Only the core rendering package (+\nits `Spectre.Console.Ansi`); **not** `Spectre.Console.Cli` (not\nAOT-safe) or `.ImageSharp`. Self-contained publish verified clean (no\ntrim/AOT warnings under `TreatWarningsAsErrors`); ~1.1 MB added DLLs.\n\n## Testing\n\n- `Core.Agent.Tests`: 168 passed\n- `Cli.Tests` (excl. `ManagedIdentityE2E`): 405 passed\n- Manually verified in a real terminal: long output shows in full (no\ntruncation), args/output wrap and indent, `verbose` on/off toggles\nnon-shell tool detail.",
          "timestamp": "2026-07-10T16:53:52+10:00",
          "tree_id": "a9d139fbe5e6a60420fa23a024cb4efff1d44545",
          "url": "https://github.com/Azure/azclips/commit/711696cf8d7ac741290034c73f46b6844a53171e"
        },
        "date": 1783667051747,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 314433915.73,
            "unit": "ns",
            "range": "± 78626746.6601768"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 273753259.08,
            "unit": "ns",
            "range": "± 5926461.41900187"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 272013275.92,
            "unit": "ns",
            "range": "± 3915546.548482384"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "11f4c58369c804f845ef62eb8f3975f4b32f0d24",
          "message": "fix(packaging): agent DLL casing + executable bit on Linux/macOS (#436)\n\n## What\n\nLinux/macOS-only packaging bugs that stopped the agent from launching.\nInvisible on Windows (NTFS is case-insensitive), so they only surfaced\nwhen a teammate tested on Linux.\n\n## Root cause\n\nThe agent's `Azclips.Agent.Tools.dll` is a **project reference** that is\n**agent-only** (unlike the other `Core.*` project refs, it has no\nbyte-identical copy in the CLI publish root). Two different loaders\nresolve it from two different flat directories:\n\n- **agent subprocess** (`az <failing-cmd>` fallback): hostfxr, app base\n= CLI publish root. Every other project ref already has a copy there;\n`Azclips.Agent.Tools` did not → the subprocess crashed with `assembly\nnot found in azclips-agent.deps.json`. (The `deps/` probing path does\n**not** help project refs — it only carries NuGet packages.)\n- **`az agent tool ...`**: the CLI's custom extension\n`AssemblyLoadContext`, which only looks flat at the extension top level.\n\n`stage_agent` had also blindly lowercased **every** dep folder name,\nwhich additionally broke the case-sensitive `deps/` layout for project\nrefs on Linux/macOS.\n\n## Fix\n\nIn `stage_agent.sh` / `stage_agent.ps1` (kept in sync):\n\n1. **Casing**: derive each dep's `deps/` subfolder from\n`libraries[].path` when present (NuGet, already lowercased), else the\n**original-case** library id (project refs). No more blanket `.lower()`.\n2. **Placement**: for each agent-only project ref (one the CLI root\nlacks), copy it into **both** the CLI publish root (for the agent\nsubprocess's app-base probe) **and** the extension top level (for the\nCLI extension loader), then still move the original under `deps/`. The\nstep-6 cleanup is exempted for these flat copies so it doesn't delete\nthem as `deps/`-redundant.\n\n**Note on executable bit**: an earlier revision added a `chmod +x` step\nto the tarball scripts. It was removed — verified against the shipped\nv0.0.4 release (built on native Linux/macOS CI agents) that `az` /\n`azclips-agent` / `copilot` / `.so` / `.dylib` already ship `0755`;\n`dotnet publish` sets the apphost executable on Linux/macOS and `tar`\npreserves it. The missing `+x` only appeared in local Windows/NTFS\ncross-compiles, which is never the release path.\n\n## Files\n\n- `devops/scripts/packaging/common/stage_agent.sh` / `stage_agent.ps1` —\ncasing + dual-placement of agent-only project refs (kept in sync)\n- `Agent/tests/Core.Agent.Tests/AgentToolsLiveTests.cs` — new\nSkip-by-default live smoke test (see Testing)\n\n## Testing\n\n- Rebuilt the linux-x64 and win-x64 full packages; confirmed\n`Azclips.Agent.Tools.dll` lands in all three needed spots (CLI root,\nextension top level, `deps/Azclips.Agent.Tools/1.0.0/`) while NuGet\npackages stay lowercase under `deps/`.\n- **Windows and WSL Ubuntu 24.04**, both load paths verified after\ninstall: `az agent tool env` (CLI extension loader) returns the env\nJSON, and `az profile show` (one-shot agent subprocess) reaches its\nnormal AI-help output — previously both crashed on the missing DLL.\n- Added `AgentToolsLiveTests`: a `Skip`-by-default live smoke test that,\nagainst an installed package, exercises **both** load paths — the seven\n`agent tool` commands and a one-shot `profile show` — so a \"fixed one\nloader, broke the other\" regression is caught by one test after install.\nIt snapshots/restores `agent.enabled` + `agent.sessionStyle` so it\nleaves the installed config unchanged.",
          "timestamp": "2026-07-13T10:06:38+10:00",
          "tree_id": "7b22b0e19616a26f5338367dacd25148180e9e0d",
          "url": "https://github.com/Azure/azclips/commit/11f4c58369c804f845ef62eb8f3975f4b32f0d24"
        },
        "date": 1783901811223,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 287363020.18,
            "unit": "ns",
            "range": "± 4700903.460294757"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 288931896.2,
            "unit": "ns",
            "range": "± 5544608.248764662"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 289247383.54,
            "unit": "ns",
            "range": "± 5124480.361602166"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "xumingk1@gmail.com",
            "name": "xuming-ms",
            "username": "xuming-ms"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "da6a74ad8be4ef88d67a284a335f15371241d538",
          "message": "Tenant resolution logic for auth flow (#437)\n\n## Summary\nResolves the tenant that actually owns the requested subscription during\n`login --subscription-id` (without `--tenant`), instead of persisting\nthe signed-in home tenant.\n\nAlso use the temp directory for MI e2e test instead of the developer's\nreal profile storage\n\n## Behavior\n- On a fresh interactive/device-code login with `--subscription-id` and\nno `--tenant`, the owning tenant is resolved from the subscription (via\nthe ARM 401 challenge) and persisted to the profile.\n- If the owning tenant cannot be determined, the login fails **without**\nsaving a profile (avoids persisting an unverified tenant).\n- For a cross-tenant subscription, the owning tenant's access is\nverified with a read-only silent-token probe **before** saving. If\ninteractive sign-in is required (e.g. MFA / conditional access) or the\nprobe fails, the login errors and the profile is **not** saved.\n\n## Tests\n- New `SubscriptionServiceTenantResolutionTests` covering the ARM\nchallenge parsing paths.\n\n---------\n\nCo-authored-by: Ming Xu <xumi@microsoft.com>",
          "timestamp": "2026-07-13T10:43:49+10:00",
          "tree_id": "ee181fbeba9410a5730810edab5d579e18477555",
          "url": "https://github.com/Azure/azclips/commit/da6a74ad8be4ef88d67a284a335f15371241d538"
        },
        "date": 1783904093690,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 290322130.53,
            "unit": "ns",
            "range": "± 4908572.5101958895"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 290736956.35,
            "unit": "ns",
            "range": "± 5599775.977344366"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 441075145.11,
            "unit": "ns",
            "range": "± 190738162.29886958"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8ebd7f0968a39d333b19bc9d4cf74588c72c31b5",
          "message": "Trigger stable release 0.0.5 (#441)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.5 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.5.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-13T10:47:53+10:00",
          "tree_id": "120893bf33fcd12bcd32e164f0d9dbfc1a42fb91",
          "url": "https://github.com/Azure/azclips/commit/8ebd7f0968a39d333b19bc9d4cf74588c72c31b5"
        },
        "date": 1783904310133,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 379454806.82,
            "unit": "ns",
            "range": "± 101969584.83697681"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 278698058.88,
            "unit": "ns",
            "range": "± 43041545.56392908"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 270732108.33,
            "unit": "ns",
            "range": "± 4424540.336078188"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "msarfraz@microsoft.com",
            "name": "mansoor sarfraz",
            "username": "msarfraz"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d93ef16042c9bbafaff05e7058c1022f26c4ac9d",
          "message": "{Core} extensions Dir added in version output (#421)\n\n**Description**\n\n- extensions Dir added in version output\n- version intrinsic verb handling improved for future use\n\n---------\n\nCo-authored-by: Mansoor Sarfraz <msarfraz+microsoft@microsoft.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-13T14:23:45+10:00",
          "tree_id": "1db298bd21f5edc5d784030ace14e3839c450cfe",
          "url": "https://github.com/Azure/azclips/commit/d93ef16042c9bbafaff05e7058c1022f26c4ac9d"
        },
        "date": 1783917247188,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 319962507.78,
            "unit": "ns",
            "range": "± 84242098.40346773"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 271872901.83,
            "unit": "ns",
            "range": "± 3156447.1192531446"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 271165252.92,
            "unit": "ns",
            "range": "± 4533165.64973166"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3d7c72ee31fdde25b8b15c2f3442d292ab8262b3",
          "message": "Rename Compute/Resources PowerShell cmdlets to Az-prefixed Azure Powe… (#446)\n\nAlign extension PowerShell command names with Azure PowerShell.\n\n## Renamed cmdlets\n- **Compute:** `Get-VM`/`New-VM`/`Remove-VM` →\n`Get-AzVM`/`New-AzVM`/`Remove-AzVM`\n- **Resources:** `New-ResourceGroup`/`Get-ResourceGroup` →\n`New-AzResourceGroup`/`Get-AzResourceGroup`;\n`New-ResourceGroupDeployment`/`New-ResourceGroupDeploymentWhatIf` →\nAz-prefixed; `Invoke-AzclipsRestMethod` → `Invoke-AzRestMethod`\n\n## Compute network cmdlets — removed, not renamed\n`New-VirtualNetwork` / `New-NetworkInterface` /\n`New-NetworkSecurityGroup` are **not** part of the Compute PowerShell\nsurface. Their dispatch was never wired up (the `s_psTable` keys were\ncommented out), so they were removed rather than renamed/exported:\n- Deleted the cmdlet classes, their PS `CommandMetadata`/commented\ndispatch entries, and their help files\n- Dropped them from `Compute.psd1` `CmdletsToExport`\n\nThey will live in a future **Network** extension (mirroring\n`Az.Network`). Networking is still created internally by the `New-AzVM`\norchestrator, so the underlying network **processors** are retained.\n\n## Scope of edits\nCmdlet `[Cmdlet]` nouns + invocation strings, `ExtensionIndex`\nInvocationNames + dispatch-table keys, `.psd1` `CmdletsToExport`,\n`exports/*.ps1` proxy functions (renamed), `help/*.md` (renamed +\ncontent), tests, benchmarks, example scripts, and docs.",
          "timestamp": "2026-07-13T15:44:30+10:00",
          "tree_id": "e1db67c70e25c7ae47c2ed7d30ca7468ddfdd8f8",
          "url": "https://github.com/Azure/azclips/commit/3d7c72ee31fdde25b8b15c2f3442d292ab8262b3"
        },
        "date": 1783922100859,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 274761443.64,
            "unit": "ns",
            "range": "± 5732210.16200573"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 274984158.87,
            "unit": "ns",
            "range": "± 4542642.837241023"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 435516853.52,
            "unit": "ns",
            "range": "± 187047808.5809441"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "tritorto@gmail.com",
            "name": "Teresa Ritorto",
            "username": "teresaritorto"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "0520c13a51f790f98853c2931495b2d1eab24b79",
          "message": "Feature/telemetry tags (#438)\n\nApplied several fixes to telemetry tagging\n- Included missing instalationId in allowlist\n- Re-added TenantId, SubscriptionId and authType (removed from\nAuthContextService as part of the profile management work).",
          "timestamp": "2026-07-13T16:05:19+10:00",
          "tree_id": "e555a57ba0545160db476ad6c58d8f6f4cb233ff",
          "url": "https://github.com/Azure/azclips/commit/0520c13a51f790f98853c2931495b2d1eab24b79"
        },
        "date": 1783923408771,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 285164255.04,
            "unit": "ns",
            "range": "± 45863128.64264793"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 276881483.66,
            "unit": "ns",
            "range": "± 18211151.34161638"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 272674264.15,
            "unit": "ns",
            "range": "± 7557986.226263482"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "68524fbf95541d9e3f81aed3d92e433102613a80",
          "message": "{Help} Handle `help` for nested sub groups (#450)\n\nFixes [Azure/CLIPS#504](https://github.com/Azure/CLIPS/issues/504): `az\ndeployment group --help` (and any intermediate sub-group) rendered a\nblank summary because only verb-root and leaf files had a home in the\ncorpus.\n\n**Structural fix, not a bandage.** CLI help markdown moves from flat\nkebab (`deployment/group-create.md`) to nested directories\n(`deployment/group/create.md` + `deployment/group/index.md`). The\nfilesystem now mirrors the command tree 1:1, with `index.md` at every\ngroup node. Intermediate-group summary falls out of the same `index.md`\nrule that already covers verb roots. `HelpCompletenessValidator`\nrequires `index.md` for every prefix of every routed command key, so the\nblank-summary regression cannot come back silently.\n\nTouches:\n\n- Runtime: `MarkdownCommandHelpProvider.ResolveHelpFilePath` (nested\nprobing: leaf `<path>.md` first, then `<path>/index.md`),\n`ExtensionIndexHelpAdapter` (drop hard-coded empty summaries; source\nfrom markdown at every level).\n- Tool: `HelpCommandKey` (path-style filenames),\n`HelpCompletenessValidator` (require intermediate `index.md`),\n`Scaffolder` (emit nested stubs).\n- Corpus: 9 leaves renamed via `git mv` (blame preserved); 3 new\nintermediate `index.md` files (`deployment/group/`, `agent/tool/`,\n`agent/tool/resource/`).\n- Docs: spec + how-to + wiki updated to describe the nested authoring\nmodel.\n- Tests added for path resolution, adapter summary population,\npath-style filenames, and nested tree completeness.\n\nVerification: `dotnet build azclips.slnx` clean; `build.ps1\n-GenerateHelpArtifacts` reports no issues (11 manifests); `Cli.Tests`\n407/407 non-E2E passed; `Azclips.Help.Tool.Tests` 174/174 passed;\nlive-checked `az deployment group --help` and `az agent tool resource\n--help`.",
          "timestamp": "2026-07-14T16:13:02+10:00",
          "tree_id": "e54cebed21ba091864451225d3ee36b2c18e422f",
          "url": "https://github.com/Azure/azclips/commit/68524fbf95541d9e3f81aed3d92e433102613a80"
        },
        "date": 1784010456284,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 271021452.78,
            "unit": "ns",
            "range": "± 5263293.47887028"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 271436792.77,
            "unit": "ns",
            "range": "± 4753289.837362849"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 349716478.35,
            "unit": "ns",
            "range": "± 95517144.95230147"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e9c54603958428a4c363e75d6bb52df1bfce90fc",
          "message": "[CLI] `az vm list`: Report missing-parameter errors using CLI flag names instead of internal property names (#461)\n\n**Related command**\n`az vm list` (and any CLI command with required parameters)\n\n**Description**\nWhen a required parameter was missing on a CLI command, the validation\nerror reported the C# property name (e.g. `SubscriptionId`) instead of\nthe actual CLI flag. For example, `az vm list` produced `Required\nparameter 'SubscriptionId' is missing.` rather than the flag the user is\nexpected to type.\n\nThis fix makes the missing-required-parameter error use the parameter's\nCLI invocation name with a `--` prefix, matching the suggestion style\nalready used by `az login`:\n\n```\nRequired parameter '--subscription-id' is missing.\n```\n\n**Testing Guide**\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~CliArgumentValidationTests\"`\n- Manual: run `az vm list` with no arguments and confirm the error lists\n`--subscription-id` / `--resource-group`.\n\n**History Notes**\n\n[CLI] `az vm list`: Report CLI flag names instead of internal property\nnames in missing-required-parameter errors\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-15T15:01:25+10:00",
          "tree_id": "c9bd97a11589b4f624063aa8e0d78433aa686b51",
          "url": "https://github.com/Azure/azclips/commit/e9c54603958428a4c363e75d6bb52df1bfce90fc"
        },
        "date": 1784092349251,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 300643251.35,
            "unit": "ns",
            "range": "± 4334452.225130534"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 303967911.74,
            "unit": "ns",
            "range": "± 4455696.0516759595"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 302716689.06,
            "unit": "ns",
            "range": "± 4874958.267213794"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "ed9842fea2ee85cc793dd258bdfbf35c9b7b1ec0",
          "message": "{DevOps} Trigger stable release 0.0.6 (#521)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.6 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.6.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-16T11:56:56+10:00",
          "tree_id": "f58364c5d024853577b19d853b1e166c584d3b6d",
          "url": "https://github.com/Azure/azclips/commit/ed9842fea2ee85cc793dd258bdfbf35c9b7b1ec0"
        },
        "date": 1784167777918,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 315126649.33,
            "unit": "ns",
            "range": "± 379290243.0649418"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 385626036.47,
            "unit": "ns",
            "range": "± 101794128.9106362"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 267760745.58,
            "unit": "ns",
            "range": "± 3588913.576752473"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "22533913f1469da9676d255e2b09a43f47fc7c62",
          "message": "{Config} Include CompletionOptions in config default entries. (#522)\n\nso az config get --key completion.enabled' returns the default value\n\nfix https://github.com/Azure/azclips/issues/519\n\n<img width=\"1423\" height=\"743\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/eaa0e93e-f8aa-441e-9990-a3e3b3e3f676\"\n/>\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-16T02:15:05Z",
          "tree_id": "ff4183cbd79551f74cffa348851080af099b3077",
          "url": "https://github.com/Azure/azclips/commit/22533913f1469da9676d255e2b09a43f47fc7c62"
        },
        "date": 1784168865716,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 268407910.06,
            "unit": "ns",
            "range": "± 3791698.182697672"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 268794031.62,
            "unit": "ns",
            "range": "± 4252872.515974093"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 267984404.35,
            "unit": "ns",
            "range": "± 3898029.4279601197"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3dfa70d256110f91b8ffba456e818ccc78815f94",
          "message": "{Core} Command not found error message adjustments (#525)\n\nCloses Azure/CLIPS#511.\n\nCloses https://github.com/Azure/azclips/issues/471\n\nTypos inside an installed extension group produced a misleading \"not\ninstalled\" error:\n\n```\n$ az vm --list\nerror: [ExtensionNotFound] Verb 'vm' is provided by extension '...Compute',\nwhich is not installed. Install it with: az extension add --name Compute\n```\n\n…even when Compute was already installed and `az vm --help` worked.\n\n**Cause.** `ExtensionDispatcher`'s verb-only fallback consulted\n`ExtensionHintTable` (a static build-time verb→package map) on any leaf\nmiss, ignoring whether the extension was already installed.\n\n**Fix.** Gate the hint-table throw on `LeafIndex.HasVerb(verb)`.\nLeafIndex is discovery-backed, so a hit means the extension IS installed\n— surface a new `CommandNotFoundException` (`code=CommandNotFound`, exit\n4) pointing at `az <verb> --help` instead.\n\nAfter:\n\n```\n$ az vm --list\nerror: [CommandNotFound] 'az vm' is not a recognized command. Run 'az vm --help' to see available commands.\n\n$ az group foobar\nerror: [CommandNotFound] 'az group foobar' is not a recognized command. Run 'az group --help' to see available commands.\n\n$ az vm list      # valid leaf still reaches the extension\n[ERROR] Failed to parse arguments for command 'vm list': ... 'SubscriptionId' is missing ...\n```\n\n`ExtensionNotFound` still fires when the verb is genuinely unknown\n(regression-tested).\n\n**Changes**\n- `LeafIndex.HasVerb` (O(1))\n- New `CommandNotFoundException`\n- Dispatcher gate + tests (`LeafIndexTests` +4,\n`ExtensionDispatcherTests` +3)\n- Docs: error-code contract in spec + Troubleshooting/exit-code table in\nhowto\n\n`CommandNotFound` is a new documented `code` (contract-relevant); exit\ncode stays `4` for scripting continuity.\n\n_Re-opened directly on the upstream repo (was #462 from fork) so CI can\nrun the fork-restricted steps._",
          "timestamp": "2026-07-16T13:30:59+10:00",
          "tree_id": "2b2bdc9d3165734e5c8e0f0e15083c47f7eac2fe",
          "url": "https://github.com/Azure/azclips/commit/3dfa70d256110f91b8ffba456e818ccc78815f94"
        },
        "date": 1784173269598,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 270979552.25,
            "unit": "ns",
            "range": "± 5532269.553022921"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 276753297.84,
            "unit": "ns",
            "range": "± 33187543.91006804"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 286992048.79,
            "unit": "ns",
            "range": "± 57812074.3169432"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "5b207522ac08f2467f68e8117643da72b070d7f9",
          "message": "{Agent} Render plan review values from the command; fix one-shot exit code (#464)\n\n**Related command**\n\nAgent / error-recovery flows — any command that triggers the agent\nfallback (e.g. a failing `az vm create`). Internal agent behavior only.\n\n**Description**\n\n- **Plan review rendering (root fix).** The \"Values to review\" block\nused to render a model-supplied `{name, value}` verbatim, which\nmis-shaped switch flags (a model listing `--active` as `{name:active,\nvalue:true}` showed `active true`, reading as if the user had to type\n`true`). Now the model lists only the flag in `defaults`, and the host\nreads the real token straight from the step command — so review rows\nalways match what runs: `--active` for a switch, `--location eastus2`\nfor a valued flag, `--admin-password <admin-password>` for a\nplaceholder. Placeholders render their flag too. `PlanSanitizer` (the\nearlier post-hoc patch) is removed since the malformed output can no\nlonger be produced.\n- **One-shot exit code.** A one-shot agent fallback only *proposes* a\nfix — it never runs anything, so the original command still failed. It\nnow returns a non-zero exit code (was masking failure with the agent\nsubprocess's own 0), so `&&`, CI, and `$?` see the failure. Interactive\nis unchanged.\n<img width=\"1078\" height=\"534\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/cf784e35-842f-4423-ac56-da94bfdad0b0\"\n/>\n- **Branding.** Dropped the unsettled \"Azclips\" name from LLM-facing\nprompts, the runtime reminder, and tool descriptions; kept the \"not the\nclassic azure-cli\" anti-hallucination boundary. Internal identifiers\n(env vars, paths, assembly names) untouched.\n- **Cleanup.** Removed the unused `tui.md` prompt (TUI not planned) and\ndropped `tui` from the entrypoint whitelist.",
          "timestamp": "2026-07-16T17:16:51+10:00",
          "tree_id": "a6c7fcbc3d86d19ca14270de8c774f00950efc98",
          "url": "https://github.com/Azure/azclips/commit/5b207522ac08f2467f68e8117643da72b070d7f9"
        },
        "date": 1784187211664,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 297766923.47,
            "unit": "ns",
            "range": "± 11772210.62223489"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 296972820.7,
            "unit": "ns",
            "range": "± 4334986.955890085"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 295669229.5,
            "unit": "ns",
            "range": "± 4664733.420693781"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "95b8ea4a3362282de8779c39f633b9332dafb033",
          "message": "{Help} Default `az` command returns top-level help and include welcome banner (#535)\n\nFixes https://github.com/Azure/azclips/issues/482\nFixes https://github.com/Azure/CLIPS/issues/503\n\n## What\n\nBare `az` (no arguments) now prints top-level help instead of erroring,\nwith a small ASCII welcome banner above it.\n\n**Before**\n```\n$ az\n[ERROR] Arguments cannot be empty. (Parameter 'args')\n```\n\n**After**\n```\n$ az\n\n     _        ___ _    ___ ___  ___ \n    /_\\   ___/ __| |  |_ _| _ \\/ __|\n   / _ \\ |_ / (__| |__ | ||  _/\\__ \\\n  /_/ \\_\\/__|\\___|____|___|_|  |___/\n\n  Welcome to AzCLIPS - Azure CLI, PowerShell & automation, in one host.\n  Run `az --help` for command groups, or `az <verb> --help` for a specific verb.\n\nGroups:\n    agent  : ...\n    config : ...\n    ...\n```\n\n## Changes\n\n- `ExtensionDispatcher.Parse`: empty args throw `DispatchHelpException`\n(same signal as `az --help`) instead of `ArgumentException`. Also covers\n`az --debug`, `az --output json`, etc., where global-flag stripping\nleaves nothing behind.\n- `Program.Main`: captures `isBareInvocation` before flag stripping so\nthe banner fires only on truly bare `az`. `az --help`, `az -h`, and `az\nhelp` stay grep-friendly.\n- New `WelcomeBanner` (figlet-small 'AzCLIPS') printed above root help\non bare invocation.\n- New hidden `PrintProbe` diagnostic for eyeballing terminal wrap /\nencoding / column alignment during triage (cold-path, no DI, not listed\nin `--help`).\n- Wiki updates so the enumerated bare-help shapes include bare `az`.\n\n## Verified\n\n- `dotnet build azclips.slnx` clean.\n- `Cli.Tests` all pass, including a regression test for the new\nempty-args dispatch.\n- Manual smoke: `az`, `az --help`, `az --debug`, `az version` all behave\ncorrectly.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-16T17:31:27+10:00",
          "tree_id": "a53de16c728d32c9fd1339f6060b5ff78c74fbb7",
          "url": "https://github.com/Azure/azclips/commit/95b8ea4a3362282de8779c39f633b9332dafb033"
        },
        "date": 1784187725928,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 282202478.72,
            "unit": "ns",
            "range": "± 4122423.340493751"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 283993331.37,
            "unit": "ns",
            "range": "± 7039572.340517508"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 281206950.8,
            "unit": "ns",
            "range": "± 4542069.725593056"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b28c14debd913c390d2bf76791ad3edf479afab4",
          "message": "{DevOps} Trigger stable release 0.0.7 (#538)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.7 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.7.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-16T21:37:55+10:00",
          "tree_id": "cf47d606296f0224af5d156555888fa01f8d4e9d",
          "url": "https://github.com/Azure/azclips/commit/b28c14debd913c390d2bf76791ad3edf479afab4"
        },
        "date": 1784202732895,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 291359133.72,
            "unit": "ns",
            "range": "± 4929784.807186398"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 292209278.8,
            "unit": "ns",
            "range": "± 5019850.504813182"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 291347409.91,
            "unit": "ns",
            "range": "± 3977711.1880208054"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "xumingk1@gmail.com",
            "name": "xuming-ms",
            "username": "xuming-ms"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "4d4c98593228f89a45946fcc5908c5ca224542ee",
          "message": "[Auth] Select the active profile's account from the shared MSAL token cache (#537)\n\n## Summary\nThe MSAL token cache is shared across every identity the user has ever\nsigned in with. The previous code took `accounts.FirstOrDefault()`,\nwhich could pick a stale/unrelated account (e.g. a personal account for\na work profile), producing 403 AuthorizationFailed on ARM.\n\nThis change introduces `MsalAccountSelector` to choose the cached\naccount that matches the active profile, mirroring Azure PowerShell\n(`SharedTokenCacheCredential` with `EnableGuestTenantAuthentication =\ntrue`):\n- match by username (UPN) first,\n- tie-break by tenant only when the UPN is ambiguous,\n- require exactly one match, otherwise return null so the caller\nre-authenticates instead of signing requests with the wrong identity.\n\nAlso, `BuildApp` now uses the multi-tenant `organizations` authority\nwhen no tenant is known (initial login) instead of a hardcoded tenant,\nand the subscription's owning tenant is resolved downstream.\n\n## Changes\n- Add `MsalAccountSelector` (UPN + tenant tie-break, exactly-one\nselection).\n- Use it in `MsalAuthService.AcquireTokenSilentlyAsync`; force re-auth\nwhen no account matches.\n- `BuildApp`: fall back to `organizations` authority when tenant is\nunset.\n- Add `MsalAccountSelectorTests` covering stale-account, guest-tenant,\nmulti-home, and no-match cases.\n\n## Validation\n- `dotnet build src/Core/Core.Authentication/Core.Authentication.csproj`\nsucceeds (0 warnings/errors).\n- `dotnet test tests/Cli.Tests` (MsalAccountSelector filter): 6/6 pass.\n\n**History Notes**\n\n[Login] `az login`: Select the MSAL cache account matching the active\nprofile during silent token acquisition to avoid signing requests with a\nstale or unrelated account\n\n---\n\n---------\n\nCo-authored-by: Ming Xu <xumi@microsoft.com>",
          "timestamp": "2026-07-17T12:23:52+10:00",
          "tree_id": "90d2371e7721685035860095258b31d0b705c8e7",
          "url": "https://github.com/Azure/azclips/commit/4d4c98593228f89a45946fcc5908c5ca224542ee"
        },
        "date": 1784255751395,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 288781150.25,
            "unit": "ns",
            "range": "± 5367835.691650978"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 288117070.8,
            "unit": "ns",
            "range": "± 5104682.802596767"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 390046412.43,
            "unit": "ns",
            "range": "± 141050166.99552396"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "30e33e5e75e7e3d2d8e0bf3fc473c9c41ad989bf",
          "message": "[Compute] Fix empty vm list and disk list output by writing the ARM list envelope value (#463)\n\n**Related command**\n`az vm list`, `az disk list`, `Get-AzVM`\n\n**Description**\n`az vm list` / `Get-AzVM` (and the disk equivalents) returned no results\neven when the resource group contained resources. ARM list endpoints\nreturn an envelope `{ \"value\": [...], \"nextLink\": \"...\" }`, but\n`ListVMProcessor`/`ListDiskProcessor` were typed as\n`ComputeProcessorBase<VirtualMachine>` / `<Disk>`, so the base pipeline\ndeserialized the envelope into a single resource object and silently\ndropped the `value` array.\n\nFix (kept intentionally small):\n1. Introduce `VirtualMachineListResult` / `DiskListResult` envelope\nmodels.\n2. Add a small `SelectOutput` hook on the generic Compute base (default\nreturns the body as-is); the list processors override it to write\n`body.Value` — a flat array. PowerShell enumerates it into individual\nobjects (`enumerateOutput`), and the CLI serializes it as a JSON array.\n\nOut of scope (deliberate): `nextLink` pagination is not followed yet — a\nresource group beyond ARM's page size will return only the first page.\nThis is acceptable for private preview and will be designed/implemented\nseparately.\n\n**Testing Guide**\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~ListVMProcessorTests|FullyQualifiedName~ListDiskProcessorTests\"`\n- Manual: `az vm list --subscription-id <sub> --resource-group <rg>` and\n`Get-AzVM -SubscriptionId <sub> -ResourceGroupName <rg>` against an RG\nwith VMs.\n\n**Live validation (real Azure)**\n\nRan the built CLI (extensions installed via `az extension add --path\n<nupkg>`) against a live subscription:\n\n- Empty RG returns `[]` (not an error) for both `az vm list` and `az\ndisk list`.\n- After creating a managed disk, `az disk list` returns it:\n```\n[ { \"id\": \".../disks/listfix-disk1\", \"name\": \"listfix-disk1\", \"location\": \"australiaeast\" } ]\n```\n- After creating a VM, `az vm list` returns it (flat JSON array with the\nVM object).\n\nTest resources were deleted afterward.\n\n**History Notes**\n\n[Compute] `az vm list`: Fix empty output by writing the ARM list\nenvelope value\n[Compute] `az disk list`: Fix empty output by writing the ARM list\nenvelope value\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-17T12:24:55+10:00",
          "tree_id": "c90cc53bac3c8dfcbab159519ba96a1dbe695723",
          "url": "https://github.com/Azure/azclips/commit/30e33e5e75e7e3d2d8e0bf3fc473c9c41ad989bf"
        },
        "date": 1784256020643,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 273398286.29,
            "unit": "ns",
            "range": "± 4108629.0120186466"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 272636766.02,
            "unit": "ns",
            "range": "± 3492466.94854527"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 270711240.46,
            "unit": "ns",
            "range": "± 3842004.7975203753"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "maddisondas@microsoft.com",
            "name": "MaddyMicrosoft",
            "username": "MaddyMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "11ea2c8d60c6d75004a9463c0b5ce389770350fa",
          "message": "[Login] Correct parameter error messages to use Client type param (#530)\n\nThe shared LoginProcessor backs both `az login` and `Connect-AzAccount`,\nbut its runtime error/warning messages hardcoded CLI-style flags\n(`--subscription-id`, `--tenant`), which are parameter-binding errors on\nthe PowerShell surface.\n\nResolve ClientType from DI and format every processor-emitted parameter\nreference per surface (`--kebab-case` on CLI, `-PascalCase` on\nPowerShell). All 17 sites across ValidateArguments,\nEnsureCrossTenantAccessible, ResolveSubscriptionTenantAsync, and\nResolveDefaultSubscriptionAsync now use a shared `Flag()` helper. Also\nrenders the sign-in guidance command surface-correctly\n(`Connect-AzAccount` vs `az login`) and fixes a latent `--tenant` typo\n(the actual CLI flag is `--tenant-id`).\n\nAdds LoginProcessorValidationTests with 12 theory cases covering all\nsites x both surfaces. Adds Moq to Cmdlets.Tests (matching Cli.Tests\nversion).\n\nFixes #468.\n\n\n**Related command**\n`az login` / `Connect-AzAccount`\n\n**Description**\nThe `LoginProcessor` is a single shared class that backs the CLI (`az\nlogin`) and PowerShell (`Connect-AzAccount`) surfaces. Its runtime error\nand warning messages hardcoded CLI-only wording, so a PowerShell user\nwho typed a bare `Connect-AzAccount` saw:\n\n```\nConnect-AzAccount: A subscription id is required. Provide it with --subscription-id.\n```\n`--subscription-id` is a CLI flag; on PowerShell it fails to bind as\n`PositionalParameterNotFound`. The correct parameter on that surface is\n`-SubscriptionId`.\n\nRoot cause: message strings baked one surface's dash notation into the\nshared processor. This change resolves `ClientType` from the DI\ncontainer inside the processor and introduces a private static\n`FormatParameterName` helper plus a `Flag()` local function per method.\nEvery parameter reference in a processor-emitted message is now rendered\nwith the caller's surface form.\n\nSites updated (17 total in\n`src/Extensions/Login/Processors/LoginProcessor.cs`):\n\n- `ValidateArguments` — 12 messages (mutual-exclusivity, MI selector\nrules, subscription-required, tenant-with-identity,\nidentity-with-devicecode, etc.)\n- `EnsureCrossTenantAccessible` — 2 throws (tenant probe threw / probe\nreturned false)\n- `ResolveSubscriptionTenantAsync` — 1 throw (ARM lookup failed)\n- `ResolveDefaultSubscriptionAsync` — 2 `logger.Warn` sites (no subs\nfound / listing failed)\n\nNo behavior change beyond message text. \n\n**Testing Guide**\nAutomated:\n\n```bash\n# 12 new theory cases covering all sites × both surfaces\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -c Debug \\\n  --filter \"FullyQualifiedName\\~LoginProcessorValidationTests\"\n\n# Full Cmdlets.Tests suite (53 tests)\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -c Debug\n\n# Cli.Tests login/profile subset (regression check)\ndotnet test tests/Cli.Tests/Cli.Tests.csproj -c Debug \\\n  --filter \"FullyQualifiedName\\~ProfileCliTests|FullyQualifiedName\\~ManagedIdentityIdCodecTests|FullyQualifiedName\\~ProfileSubscriptionResolutionTests\"\n```\n\nManual (from a fresh `pwsh -NoProfile`, after `./build/build.ps1\n-Configuration Debug`):\n\n```powershell\n$env:Path = \"$PWD\\artifacts\\Debug\\Core.Cli;$env:Path\"\nImport-Module .\\artifacts\\Debug\\PS\\Login\\Login.psd1 -Force\n\n# CLI — expect '--subscription-id' etc.\naz login\naz login --identity --tenant-id 11111111-1111-1111-1111-111111111111\naz login --identity --client-id aaa --object-id bbb\naz login --identity --use-device-code\naz login --subscription-id 00000000-0000-0000-0000-000000000000 --client-id aaa\n\n# PowerShell — expect '-SubscriptionId' etc.\nConnect-AzAccount\nConnect-AzAccount -Identity -TenantId 11111111-1111-1111-1111-111111111111\nConnect-AzAccount -Identity -ClientId aaa -ObjectId bbb\nConnect-AzAccount -Identity -UseDeviceCode\nConnect-AzAccount -SubscriptionId 00000000-0000-0000-0000-000000000000 -ClientId aaa\n```\n\n`--profile` / `-Profile` validation checks are covered by the unit tests\nonly — they require a pre-existing profile to reach the processor, which\nneeds a real Azure sign-in.\n\n\n**History Notes**\n\n[Login] `az login` / `Connect-AzAccount`: Show surface-correct parameter\nnames in validation and post-sign-in error messages\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-17T13:02:10+10:00",
          "tree_id": "c1a587214c10f3241bdb6cff47ffdf40d4e15e77",
          "url": "https://github.com/Azure/azclips/commit/11ea2c8d60c6d75004a9463c0b5ce389770350fa"
        },
        "date": 1784258045122,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 290202819.43,
            "unit": "ns",
            "range": "± 6071575.201678159"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 290055658.8,
            "unit": "ns",
            "range": "± 6201392.3456535665"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 290911084.55,
            "unit": "ns",
            "range": "± 6839659.054570541"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3d933f5df0af5cdf49db2fd2a611afaab5ba599e",
          "message": "{DevOps} Trigger stable release 0.0.8 (#551)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.8 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.8.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-17T13:29:53+10:00",
          "tree_id": "22a3b313a2f49f967fc7f8aa270a2a407a72c018",
          "url": "https://github.com/Azure/azclips/commit/3d933f5df0af5cdf49db2fd2a611afaab5ba599e"
        },
        "date": 1784259657976,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 393246185.49,
            "unit": "ns",
            "range": "± 137695910.98497632"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 322696622.32,
            "unit": "ns",
            "range": "± 87679886.47895984"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 288829252.37,
            "unit": "ns",
            "range": "± 4823959.224972218"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "128b98e0df6b5438009ac6be1c1f1e9b65c5d1e2",
          "message": "{Telemetry} Remove unnecessary configs for telemetry (#527)\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\nRemoves dangling configs for `telemetry.appInsightsInstrumentationKey`\nand `telemetry.zipkinEndpoint`\nWe do not want to allow users to change the instrumentation key and\nzipkin itself has been deprecated other options are being evaluated.\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\nRun `<path>/az config list --all`\n<img width=\"1325\" height=\"805\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/c762b081-ca50-478f-9109-c03e71b113fb\"\n/>\n\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-17T13:55:36+10:00",
          "tree_id": "04e886f8984355847d9826223d2b00083f80b219",
          "url": "https://github.com/Azure/azclips/commit/128b98e0df6b5438009ac6be1c1f1e9b65c5d1e2"
        },
        "date": 1784261270103,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 289318178.53,
            "unit": "ns",
            "range": "± 7524740.810713036"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 288954177.42,
            "unit": "ns",
            "range": "± 5024669.128341855"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 290340099.06,
            "unit": "ns",
            "range": "± 6720614.34694459"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "ef08058baa79d7ff79c3d238de3003e3f622b7a2",
          "message": "{Autocomplete} Fix multi layered commands not completing correctly (#557)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n`az deployment`\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\nFor any command that has multiple sub commands autocompletion was not\nable to resolve the completion tree correctly as the keys are flattened.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\nsource the sh scripts\n`. .\\src\\CLI\\Completions\\az.completions.ps1` or `source\n.\\src\\CLI\\Completions\\az.completions.bash`\nattempt to auto complete any commands with multiple sub commands groups\n`az deployment` or `az agent`\n\n<img width=\"1348\" height=\"158\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/88b32176-853c-4ccb-967e-cf4b896c336a\"\n/>\n\n\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-18T13:21:35+10:00",
          "tree_id": "167310b52a90cd89df6cba088630b3cc50c13f4c",
          "url": "https://github.com/Azure/azclips/commit/ef08058baa79d7ff79c3d238de3003e3f622b7a2"
        },
        "date": 1784345512876,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 282414095.55,
            "unit": "ns",
            "range": "± 5002869.240252176"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 282622097.92,
            "unit": "ns",
            "range": "± 5185214.5121325925"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 280851541.89,
            "unit": "ns",
            "range": "± 4610819.7071546605"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yangan@microsoft.com",
            "name": "YangAn-microsoft",
            "username": "YangAn-microsoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "93666114eef2966b8bd8c5f7d3d75716682d7190",
          "message": "[Resources] Make SubscriptionId optional for resource group create (#481) (#555)\n\n**Related command**\n`az group create` / `New-AzResourceGroup`\n\n**Description**\n`SubscriptionId` was declared as a required parameter in the PS and CLI\ncommand metadata for `New-AzResourceGroup` / `az group create`, forcing\nusers to pass it explicitly. `ResolveSubscriptionId` already falls back\nto the active profile's subscription, so this marks the parameter\noptional to match the `group show` and `group list` commands and\ndefaults to the current subscription when omitted.\n\nThe CLI help doc for `group create` is also updated to note the\nparameter is optional.\n\nFixes #481\n\n**Testing Guide**\n- `dotnet build src/Extensions/Resources/Extensions.Resources.csproj`\n- Run `az group create --name myrg --location eastus` without\n`--subscription-id` and confirm the group is created in the current\nsubscription.\n\n**History Notes**\n\n[Resources] `az group create`: Make SubscriptionId an optional parameter\nthat defaults to the current subscription\n[Resources] `New-AzResourceGroup`: Make SubscriptionId an optional\nparameter that defaults to the current subscription\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-18T19:45:24+10:00",
          "tree_id": "fba663f299bcd6bcf2926a105907c4fe0034bc00",
          "url": "https://github.com/Azure/azclips/commit/93666114eef2966b8bd8c5f7d3d75716682d7190"
        },
        "date": 1784368513984,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 271677634.34,
            "unit": "ns",
            "range": "± 4198777.220623338"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 272566357.55,
            "unit": "ns",
            "range": "± 4336699.409388464"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 271068203.96,
            "unit": "ns",
            "range": "± 2856338.853933602"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "fdf548a6617964f89054d12fc82fc52fd4f1f605",
          "message": "{Telemetry} Use consistent LocalAppData path for trace files (#523)\n\nFixing https://github.com/Azure/azclips/issues/511\nTelemetry trace logs were being written to\n{LocalAppData}/Azclips/traces/ while auth profiles are stored in\n{LocalAppData}/.azclips/. Changed\nFileTelemetrySender.GetTraceDirectory() to use\nHomePaths.GetDefaultLocalAppDataRoot() so all local app data files live\nunder {LocalAppData}/.azclips/ consistently.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-20T15:38:08+10:00",
          "tree_id": "388ff228806364a7e78e2ca8357d18f70d952457",
          "url": "https://github.com/Azure/azclips/commit/fdf548a6617964f89054d12fc82fc52fd4f1f605"
        },
        "date": 1784526594149,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 278946527,
            "unit": "ns",
            "range": "± 14723789.462016318"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 349362452.34,
            "unit": "ns",
            "range": "± 139768880.18519208"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 343978056.44,
            "unit": "ns",
            "range": "± 152661172.00970548"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a94c7be9f81727963950cd7edbf9f3b04e516df1",
          "message": "{Telemetry} Revert consistent LocalAppData path for trace files (#569)\n\n**Related command**\nN/A\n\n**Description**\nReverts merged PR #523 (commit\n`fdf548a6617964f89054d12fc82fc52fd4f1f605`) to undo the LocalAppData\ntrace-file path change in `FileTelemetrySender`.\n\n**Testing Guide**\nN/A\n\n**History Notes**\n{Telemetry} Revert LocalAppData trace-file path change introduced by\n#523\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-21T09:08:56+10:00",
          "tree_id": "fba663f299bcd6bcf2926a105907c4fe0034bc00",
          "url": "https://github.com/Azure/azclips/commit/a94c7be9f81727963950cd7edbf9f3b04e516df1"
        },
        "date": 1784589568291,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 423779562.4,
            "unit": "ns",
            "range": "± 222918127.63520497"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 280799191.5,
            "unit": "ns",
            "range": "± 35714253.06861686"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 278104880.56,
            "unit": "ns",
            "range": "± 4005469.918839617"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6832555fb59bbf86d378dd4d2dea3a67ddcced96",
          "message": "{DevOps} Trigger stable release 0.1.0 (#572)\n\nUpdates src/CLI/Core.Cli.csproj to 0.1.0 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.1.0.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-21T09:29:27+10:00",
          "tree_id": "3322ed8cc69089216c3644b2bea163b5298c2e83",
          "url": "https://github.com/Azure/azclips/commit/6832555fb59bbf86d378dd4d2dea3a67ddcced96"
        },
        "date": 1784590768819,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 272116420.68,
            "unit": "ns",
            "range": "± 4917150.9451785255"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 271091559.03,
            "unit": "ns",
            "range": "± 3447024.200787587"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 272218068.13,
            "unit": "ns",
            "range": "± 3767706.3006210513"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "40ea71669329eb70850bea98de8315c9e4d9dd08",
          "message": "{chore} merge dev recent commits to main (#582)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-22T13:53:15+10:00",
          "tree_id": "babec21741ab3a1feef4ae8f984fb06321e3e8d1",
          "url": "https://github.com/Azure/azclips/commit/40ea71669329eb70850bea98de8315c9e4d9dd08"
        },
        "date": 1784693080272,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 285499863.71,
            "unit": "ns",
            "range": "± 10095301.804200353"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 286072025.61,
            "unit": "ns",
            "range": "± 4711630.100598734"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 285239215.06,
            "unit": "ns",
            "range": "± 5409274.708079745"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "14b0f3ea718e0d129002befbba84e23fa1c17715",
          "message": "{Telemetry} Telemetry consistent LocalAppData (#586)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\nTelemetry trace logs were being written to\n{LocalAppData}/Azclips/traces/ while auth profiles are stored in\n{LocalAppData}/.azclips/. Changed\nFileTelemetrySender.GetTraceDirectory() to use\nHomePaths.GetDefaultLocalAppDataRoot() so all local app data files live\nunder {LocalAppData}/.azclips/ consistently.\n\n(cherry picked from commit\nhttps://github.com/Azure/azclips/commit/28348e09106275764d15ff9af4c0a8372b8e1b5f)\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\nCo-authored-by: Nori Zhang <norizhang@microsoft.com>",
          "timestamp": "2026-07-22T04:11:55Z",
          "tree_id": "23f8de13cf0873924080d39cec2039615fa12dd2",
          "url": "https://github.com/Azure/azclips/commit/14b0f3ea718e0d129002befbba84e23fa1c17715"
        },
        "date": 1784694250254,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 268143321.3,
            "unit": "ns",
            "range": "± 2314225.1920907884"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 272025875.26,
            "unit": "ns",
            "range": "± 4984048.349470955"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 334080531.36,
            "unit": "ns",
            "range": "± 86744390.42354359"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b03a05ed76159e4c045fb381c10b445ec2e0dd31",
          "message": "{Core} Update default log-level to `WARN`, adjust call sites, and add debug logging for extension loading (#588)\n\n> **Supersedes [#575](https://github.com/Azure/azclips/pull/575)** —\nthat PR was auto-closed when the `dev` branch was deleted. Full prior\nreview discussion (7 Copilot comments + follow-ups, all addressed) lives\non #575.\n\n---\n\nFixes [Azure/CLIPS#493](https://github.com/Azure/CLIPS/issues/493) —\nLogging: Examine default log-level, call site level usage, and\n`WriteLine` vs `Error.WriteLine`.\n\n---\n\n## 1. Summary\n\nThree intertwined changes to CLI logging, all shipping here:\n\n1. **Stream routing fix** — align with POSIX/GNU: stdout = command data,\nstderr = diagnostics. Today\n[`BuildStreams`](src/CLI/Program.cs#L203-L253) sends\nwarning/info/verbose/debug to stdout, polluting `--output json`\npipelines. Every peer CLI (Azure CLI Python, Az PowerShell, AWS CLI,\ngit, kubectl, docker, terraform) puts diagnostics on stderr.\n2. **Default log level → `warn`** (was `error`). Matches Azure CLI\nPython and Az PowerShell defaults; surfaces warnings that are currently\nsilently swallowed (e.g. [`LoginProcessor.cs` L419,\nL428](src/Extensions/Login/Processors/LoginProcessor.cs#L419-L428)).\n3. **Extension-load debug tracing** — per-extension load rows +\naggregate summary table on\n[`ExtensionLoader`](src/CLI/AssemblyLoading/ExtensionLoader.cs),\nmodelled on [`azure-cli/src/azure-cli-core/azure/cli/core/__init__.py`\nL232-L235,\nL680-L688](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232).\nAll at `Debug`; emitted only under `--debug` or\n`core.logging.level=debug`.\n\n---\n\n## 2. Decision Registry\n\nSuccinct \"why\" for each committed decision.\n\n| # | Decision | Rationale |\n| ------- |\n--------------------------------------------------------------------------------------------\n|\n-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------\n|\n| **D1** | Default log level: `warn` (was `error`) | Azure-family peer\nsymmetry — Azure CLI Python defaults to `Warning`; Az PowerShell\neffective baseline is `Warning + Error`. `info`-as-default rejected (no\ncited peer default; would need call-site retriage). Shared config also\nflips PS cmdlets; `-WarningAction Ignore` is the escape hatch. |\n| **D2** | Route Warning/Info/Verbose/Debug to **stderr** | Industry\nstandard. POSIX defines stderr as the diagnostic stream; [GNU Coding\nStandards\n§4.4](https://www.gnu.org/prep/standards/html_node/Errors.html) codifies\nit; every peer CLI examined follows it. Current stdout routing is a bug.\n|\n| **D3** | Collapse `ILoggingService.Log(...)` → `Info(...)` in\n`ConsoleLoggingService` | Zero production call sites;\n[`OutputStreamAdapter.Log`](src/Core/Core.Runtime/ScopedLoggingService.cs#L66-L69)\nalready delegated to `Info`. `Log` stays on the interface as a permanent\nalias — no deprecation planned. |\n| **D4** | Add `ShouldEmitColor()` guard with standard precedence |\nPrecedence chain matches ecosystem standard: `NO_COLOR` → `FORCE_COLOR`\n→ `TERM=dumb` → redirection check → color. Applied at all eight color\nsites; broader audit rolled into the [`ConsoleSink`\nfollow-up](https://github.com/Azure/CLIPS/issues/543). |\n| **D5** | Extension-load tracing: per-load rows + aggregate table, all\nat `Debug` | Copies the [azure-cli\n`__init__.py`](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py#L232)\nL232-L235, L680-L688 pattern: format-string constants + per-load Debug\nrow + `Total (count)` summary. Uniform `Debug` level — user gets\neverything or nothing under `--debug`. |\n| **D6** | Log prefix: per-class `[nameof(TypeName)]` | Matches existing\ncodebase convention — e.g.\n[`MsalAuthService`](src/Core/Core.Authentication/MsalAuthService.cs#L36).\n|\n| **D7** | Fix TUI's stderr classifier as part of Tier B | [`TUI\nProcessService.RunCliAsync`](TUI/services/ProcessService.cs) mapped all\nstderr → `HistoryItemType.Error`; post-D2 every warning would render as\nan error in the TUI. `ClassifyLine` extended to recognize the AzClips\nprefix set. |\n| **D8** | Don't demote `Info` call sites in this PR | Under `warn`,\n`Info` sites stay hidden by default so noise doesn't manifest. No\n`info`-default move planned; revisit only if that ever changes. |\n| **D9** | New subprocess-based integration test required |\n[`ExecuteCli`\nfixture](tests/Cli.Tests/Configuration/ConfigCliTests.cs#L362)\nconstructs its own `OutputStreams` and bypasses `Program.Main` /\n`BuildStreams` — cannot test D2 routing. |\n| **D10** | Harmonize diagnostic prefix to `LEVEL: message` (uppercase\nword, single colon, single space) | Three surfaces were emitting three\ndifferent prefix styles. Chose `LEVEL:` over `[LEVEL]`: matches Azure\nCLI Python's output, the Python `logging` default formatter, and the PS\nhost's own rendering of `cmdlet.WriteWarning(...)`. Consolidated\npre-production while no downstream parser has locked in a style. |\n\n---\n\n## 3. Implementation summary\n\nAll 18 planned items landed on this branch; one (a chain-order Debug\nfrom `CLIAssemblyLoadingProvider`) was dropped mid-flight as unreachable\nthrough the level-gated pipeline. Change groups map to the decisions\nabove:\n\n- **Stream routing (D2/D3/D4/D7):**\n[`BuildStreams`](src/CLI/Program.cs),\n[`ConsoleLoggingService`](src/Core/Core.Runtime/ConsoleLoggingService.cs),\nand\n[`ConsoleOutputAdapter`](src/Core/Core.Runtime/ConsoleOutputAdapter.cs)\nall now write warn/info/verbose/debug to `Console.Error`; new\n[`ColorPolicy.ShouldEmitColor`](src/Core/Core.Runtime/ColorPolicy.cs)\nguards color at all eight sites (stderr uses\n`Console.IsErrorRedirected`, stdout uses `Console.IsOutputRedirected`);\nTUI [`ClassifyLine`](TUI/services/ProcessService.cs) recognizes the\nAzClips prefix set.\n- **Default flip (D1):**\n[`CoreOptions.LoggingOptions.Level`](src/Core/Core.Abstractions/CoreOptions.cs),\nthe [config schema\nmirror](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs), and\nthe two invalid-value fallbacks ([CLI](src/CLI/CliLoggingPolicy.cs),\n[PS](src/PS/Common/CmdletOutputStreamsFactory.cs)) all move `error` →\n`warn`. Tests updated in lockstep\n([`ConfigCliTests`](tests/Cli.Tests/Configuration/ConfigCliTests.cs),\n[`CliLoggingPolicyTests`](tests/Cli.Tests/Configuration/CliLoggingPolicyTests.cs),\n[`CmdletOutputStreamsFactoryTests`](tests/Cmdlets.Tests/CmdletOutputStreamsFactoryTests.cs)).\n- **Extension-load debug tracing (D5/D6):** new\n[`ExtensionLoadTracker`](src/CLI/AssemblyLoading/ExtensionLoadTracker.cs)\naccumulates per-load rows and emits an aggregated table on shutdown;\nboth loader implementations in\n[`ExtensionLoader.cs`](src/CLI/AssemblyLoading/ExtensionLoader.cs)\ninstrumented with entry/probe/hit/miss `[ClassName]`-prefixed Debug\nlines. Summary routes through `streams.WriteDebug` (level-gated) so the\ntable stays silent under `warn`.\n- **Tests (D9):**\n[`StreamRoutingIntegrationTests`](tests/Cli.Tests/StreamRoutingIntegrationTests.cs)\n(4 subprocess tests: `WriteError`→stderr; `warn` keeps stderr silent;\n`--debug` puts `DEBUG:` on stderr with stdout clean; `--debug` emits\nper-loader + `ExtensionLoadTracker` lines) and\n[`TuiClassifyLineTests`](tests/Cli.Tests/TuiClassifyLineTests.cs) (7\nunit tests covering every prefix + fallthrough + ANSI stripping).\n`ClassifyLine` bumped to `internal static` with\n`InternalsVisibleTo=\"Cli.Tests\"` for the unit tests.\n- **Docs:**\n[`docs/azure-cli/docs-conceptual/config.md`](docs/azure-cli/docs-conceptual/config.md)\ngained a **Stream routing** subsection and refreshed sample outputs;\n[`eng-docs/spec/config-implementation.md`](eng-docs/spec/config-implementation.md)\n§10 \"CLI Runtime Logging Policy\" expanded to cover the default flip,\nstream contract, `LEVEL:` prefix, color gating, and the `EmitSummary`\nstreams-vs-logger invariant.\n\nPer-item commit SHAs and file lists live in the branch's git log — see\nthe **Files changed** / **Commits** tabs for the granular view.\n\n---\n\n## 4. Behavior audit under the new `warn` default\n\nCodebase scan for `.Warn(...)` / `WriteWarning(...)` producers to\nenumerate what changes from \"silent under `error`\" to \"visible under\n`warn`\". Result: **two user-facing warning sites, both in `az login`.**\n\n### Newly visible warnings\n\n1. **`az login` — no subscriptions on the identity**\n\n[`LoginProcessor.cs:419`](src/Extensions/Login/Processors/LoginProcessor.cs#L419)\n— `logger.Warn(\"No subscriptions found for the logged-in identity.\nContinuing with tenant-level access; pass '--subscription' to target a\nsubscription.\")`. Fires when subscription auto-discovery completes but\nreturns zero entries after a successful login. Actionable: the warning\ntells the user why no default subscription was picked and how to specify\none.\n\n2. **`az login` — subscription auto-discovery failure**\n\n[`LoginProcessor.cs:428`](src/Extensions/Login/Processors/LoginProcessor.cs#L428)\n— `logger.Warn($\"Failed to auto-discover subscriptions: {ex.Message}.\nContinuing without a default subscription; pass '--subscription' to set\none.\")`. Fires when subscription auto-discovery throws (e.g. ARM\ntransient failure, revoked permissions). Actionable: surfaces the\nunderlying exception message and the workaround.\n\nBoth are on the intentional side of D1's PS-side note: under the new\ndefault the PS host applies `$WarningPreference = Continue` and shows\nthem; callers who want the old silence use `-WarningAction Ignore`.\n\n---\n\n## 5. Validation\n\n- `dotnet build azclips.slnx`\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj`\n- Manual: `az login --output json 2> /dev/null | jq .` and `az extension\nlist 2> /dev/null | jq .` produce clean JSON.\n- Manual (Windows): `Console.ForegroundColor` behavior on stderr, both\npiped and non-piped; `NO_COLOR=1 az foo` suppresses color.\n- Manual: `AZCLIPS_LOG_LEVEL=debug az <cmd-that-loads-extension>` shows\nthe new tracker summary and per-extension rows.\n- Manual: `az --debug config get --key core.logging.level` — clean\nstdout, debug trace on stderr, still parseable when piped.\n- Manual: `az config get --key core.logging.level` with no prior setting\nprints `core.logging.level = warn [default]` (previously `error`).\n- Manual: an operation that emits `Warn` (e.g. login with no\ndiscoverable subscriptions — [`LoginProcessor.cs`\nL419](src/Extensions/Login/Processors/LoginProcessor.cs#L419)) surfaces\nthe warning on stderr under the new default; previously silently\nswallowed.\n- Manual (PS): import a module (e.g. `Import-Module\nsrc/PS/Login/Login.psd1`) and invoke a cmdlet that triggers\n`logger.Warn(...)` — confirm the warning surfaces in the PS host\n(default `$WarningPreference = Continue`). Then re-run with\n`-WarningAction Ignore` and confirm it's suppressed. Both should hold\nwith no prior `core.logging.level` config set.\n\n---\n\n## Follow-ups (not in this PR)\n\nTracked under epic [Azure/CLIPS#542 \"AzClips\nLogging\"](https://github.com/Azure/CLIPS/issues/542):\n\n- [Azure/CLIPS#543](https://github.com/Azure/CLIPS/issues/543) —\nConsolidate console-writing primitives into a single `ConsoleSink`.\n- [Azure/CLIPS#544](https://github.com/Azure/CLIPS/issues/544) — Add a\n`Warn` value to `HistoryItemType` in the TUI.\n- [Azure/CLIPS#545](https://github.com/Azure/CLIPS/issues/545) — Bring\nstray raw `Console.*` call sites under `ILoggingService`.\n\n---------\n\nCo-authored-by: JaysonTaiMicrosoft <jaysontai@microsoft.com>\nCo-authored-by: Nori Zhang <norizhang@microsoft.com>",
          "timestamp": "2026-07-22T14:47:20+10:00",
          "tree_id": "10b4dc80bf3ba0cd7418c8ff105982581fc550d6",
          "url": "https://github.com/Azure/azclips/commit/b03a05ed76159e4c045fb381c10b445ec2e0dd31"
        },
        "date": 1784696306419,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 394144140.55,
            "unit": "ns",
            "range": "± 104821054.83917807"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 274130647.96,
            "unit": "ns",
            "range": "± 2585314.458063936"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 270671261.3,
            "unit": "ns",
            "range": "± 2795259.728791373"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9ace2ec11b7d211c8c5c7c1f07baef54f4e0ed68",
          "message": "[Extension] `az extension list`: Include bundled extensions and expose on-disk `Location` field (#590)\n\n**Related command**\n\n- `az extension list`\n- `az extension show`\n\n**Description**\n\nCurrently `az extension list` only surfaces extensions installed via `az\nextension add` under `~/.azclips/extensions/`; extensions bundled with\nthe `az` install are silently filtered out, and no row exposes the\non-disk DLL path.\n\nThis PR:\n\n- Removes the Core-tier `PackageId` filter in\n`DiscoveryBackedExtensionRegistry` so all discoverable extensions (Core\n+ Bundled + Installed) surface via `az extension list` / `az extension\nshow`.\n- Adds an absolute `Location` DLL path per row, plumbed from the loaders\nthrough `InstalledExtensionRef` → `DiscoveredExtensionInfo` →\n`ExtensionRegistryEntry`. `Location` is runtime-only\n(`[JsonIgnore(Always)]`) and never persisted to `registry.json`.\n\nFixes https://github.com/Azure/CLIPS/issues/478\n\n**Key design decisions**\n\n- **Scope**: list all three tiers (Core + Bundled + Installed) with an\nabsolute-path `Location` field. No `Tier` column — path prefix conveys\norigin.\n- **List only what's discoverable on disk**: do not synthesize rows from\n`BuiltInExtensions.Map` for Core verbs with no DLL present.\n- **Winner-take-all preserved**: `list` still shows one row per\n`PackageId` via `ExtensionCandidateSelector` — matches dispatch exactly.\nBroader \"show shadowed candidates\" visibility is coupled to the\nparallel-version-install work and deferred.\n- **`Location` is runtime-only**: never persisted; no `registry.json`\nschema bump.\n- **Private preview**: breaking changes to `Core.Abstractions.*` records\nand `IExtensionLoader` tuple are accepted; no back-compat shims.\n\n**Testing Guide**\n\n- Automated: `dotnet build azclips.slnx` clean; `Cli.Tests` 505 pass;\n`Core.Extensions.Tests` 79/79; `Core.Engine.Tests` 2/2. New tests cover\n`Location` propagation through `PivotByVerb` and end-to-end from both\nloaders.\n- Manual: full sweep on both local Debug build and CI\n`Distribution_Windows_Full.zip` — `list` / `show` / `add` / `remove` /\n`pin` / `update` / dispatch smoke (`vm`, `disk`, `login`, `config`,\n`agent`, `extension`) all pass, no CLIPS#522-shape \"could not be found\nby any extension loader\" errors, `registry.json` never gains a\n`location` key.\n\n**History Notes**\n\n[Extension] `az extension list`: Include bundled extensions and expose\non-disk `Location` field\n[Extension] `az extension show`: Expose on-disk `Location` field\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-23T15:03:18+10:00",
          "tree_id": "cf472a4a12253b6a246c49d9925ecf2eefc4b76d",
          "url": "https://github.com/Azure/azclips/commit/9ace2ec11b7d211c8c5c7c1f07baef54f4e0ed68"
        },
        "date": 1784783691255,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 284570228.63,
            "unit": "ns",
            "range": "± 6571319.204165976"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 286338332.53,
            "unit": "ns",
            "range": "± 5648553.940225832"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 284084566.93,
            "unit": "ns",
            "range": "± 5104207.401247991"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "maddisondas@microsoft.com",
            "name": "MaddyMicrosoft",
            "username": "MaddyMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b5d3e2863162ea3775b90bca7860a434ba700779",
          "message": "[Core] Fix argument-name routing and user-visible error labels (#594)\n\n**Related command**\n\nAll commands (argument parsing layer). PS side affects any cmdlet where\nthe parameter name differs from the processor property (e.g. `-ObjectId`\nbound to a `Group` property). CLI side affects every command that emits\na parameter-related error or verbose log.\n\n**Description**\n\nTwo independent argument-parser bugs surfaced while onboarding a new\ncmdlet. Fixing both in one PR because they share the same test-project\nsetup and both trace back to the parsers passing the wrong string\ndownstream.\n\n**Fix 1 — `PSArgumentParsingProvider` routing (functional bug)**\n\n`PSArgumentParsingProvider.ParseArguments` was inserting each argument\ninto the output dictionary under the raw incoming key (the PS parameter\nname from `BoundParameters`). Downstream,\n`ArgumentParsingService.SetProperty` calls\n`processor.GetType().GetProperty(propertyName)` on that key, so whenever\nthe PS parameter name differed from the underlying processor property\nname the reflection lookup returned `null` and the whole call failed\nwith `The property 'X' does not exist on processor '...'`. The CLI\nprovider has always translated the incoming key to `metadata.Name`\nbefore writing it out — the PS provider was the only one skipping that\nstep.\n\nFixed by reading `metadata.Name` off the resolved `ParameterMetadata`\nand using it as the output-dict key. Unknown keys (no metadata) still\npass through unchanged so we don't mask legitimate \"unknown parameter\"\nerrors elsewhere.\n\n**Fix 2 — `CLIArgumentParsingProvider` error labels (UX bug)**\n\n`CLIArgumentParsingProvider` was building `ArgumentException` messages\nand verbose log lines from `parameterMetadata.Name`, which is the\nprocessor property name — not what the user typed. A user who ran `az\nfoo -g mygroup` and hit a validation failure would see an error naming\n`Group` (or whatever internal property) rather than `-g` or `--group`.\nSame story for aliases: users who typed the short form saw the canonical\nform in the error.\n\nFixed by capturing `originalInput = kvp.Key` at the top of the loop,\nbefore the alias-resolution branch reassigns `key`, and threading\n`originalInput` through `ParseArgument`, `ConvertToCollection`,\n`ConvertScalar`, and `PropertyParser.ConvertToType`. The `propertyName`\nparameter on those helpers is used exclusively for user-facing text —\nnever for lookups or control flow — so passing the literal input string\nis safe throughout. Errors and verbose lines now name exactly what the\nuser typed.\n\nNeither change alters public API. No behavioural change beyond the\nerror/log text on the CLI side and unblocking previously-broken cmdlets\non the PS side.\n\n**Testing Guide**\n\n```bash\n# From repo root\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj\ndotnet test tests/Cli.Tests/Cli.Tests.csproj\n```\n\n- `tests/Cmdlets.Tests/PSArgumentParsingProviderTests.cs` (new) — 4\ntests covering the PS-name-to-processor-property translation,\nunknown-key passthrough, and array parameter handling.\n- `tests/Cli.Tests/ArgumentParsing/CliArgumentValidationTests.cs` — 3\nnew tests covering missing scalar value, scalar conversion failure, and\nshort-alias-in-error-message. Existing\n`ValidateArguments_MissingRequired_ReportsCliFlagName` unchanged.\n\nAlso verified by temporarily reverting each fix and confirming the new\ntests fail as expected before re-applying.\n\nTwo pre-existing failures on clean `main`\n(`CompatibilityGuardTests.CheckAssembly_StampedFirstPartyExtension_IsIncompatibleAt0_9_0`\nand\n`ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`)\nare unrelated to this change and reproduce without any of these edits\napplied.\n\n**History Notes**\n\n[PowerShell] Argument parser: Route cmdlet parameters to their processor\nproperty name so parameters whose PS name differs from the backing\nproperty no longer fail with a missing-property error\n[CLI] Argument parser: Use the flag or alias the user typed in error\nmessages and verbose logs instead of the internal processor property\nname\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed. — n/a, no doc-visible behaviour changed",
          "timestamp": "2026-07-23T17:49:50+10:00",
          "tree_id": "98002ae6b3833fe7453539dad5e978403e2439cb",
          "url": "https://github.com/Azure/azclips/commit/b5d3e2863162ea3775b90bca7860a434ba700779"
        },
        "date": 1784794096629,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 288128019.67,
            "unit": "ns",
            "range": "± 4360684.6955514"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 290524506.68,
            "unit": "ns",
            "range": "± 5037424.078640888"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 287125609.02,
            "unit": "ns",
            "range": "± 5577160.872978663"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "088027bf6672da1880260d1fe26bb147644485d0",
          "message": "[Telemetry] Honor telemetry.enabled changes mid-session (#592)\n\n**Related command**\n\n`Set-AzConfig -Key telemetry.enabled`, `az config set --key\ntelemetry.enabled`, plus every cmdlet / `az` command that emits\ntelemetry.\n\n**Description**\n\nFixes #470.\n\n`PSTelemetryProvider` (and its CLI/TUI siblings) read\n`TelemetryOptions.Enabled` in the constructor and cached the decision by\neither building the OpenTelemetry pipeline or short-circuiting. Because\nthe PS DI container lives from `Import-Module` to `Remove-Module`, that\ndecision was frozen for the whole session — so `Set-AzConfig\ntelemetry.enabled false` had no effect on subsequent cmdlets until the\nmodule was reloaded. The mirror direction (start disabled → enable\nmid-session) was equally broken.\n\nThe check now lives in `TelemetryService.StartActivity` /\n`StartRootActivity`, which read `TelemetryOptions.Enabled` via\n`IConfigurationService` on every call. The three provider ctors always\nbuild the pipeline; the runtime gate is the single source of truth. Both\ntoggle directions now take effect on the very next invocation without a\nmodule reload.\n\nNothing else can leak: the existing `DefaultActivityNameFilterProcessor`\nallowlist already drops any span (e.g. HTTP-client instrumentation)\nwhose OperationName isn't produced by our own `ITelemetryService.Start*`\nmethods, so gating those methods is a complete fix.\n\nPer-call cost is one\n`IConfigurationService.GetConfiguration<TelemetryOptions>()` call\n(warm-cache ~5–15 µs), well under 1 % of any cmdlet that touches the\nnetwork.\n\n**Scope**\n\n| Host | Container lifetime | Observable before | After fix |\n| --- | --- | --- | --- |\n| PowerShell | `Import-Module` → `Remove-Module` (hours+) | **Yes** —\nthis issue | Live toggle both directions |\n| TUI | Interactive REPL (long) | Latent (same code shape) | Live toggle\nboth directions |\n| CLI | One process per `az` invocation | No — new process = fresh\nconfig read | Parity change (see manual repro) |\n| Agent | Medium-lived, inherits CLI provider | Latent | Parity change |\n\n**Testing Guide**\n\nAutomated:\n\n```powershell\ndotnet build azclips.slnx -c Debug\ndotnet test tests/Core.Telemetry.Tests/Core.Telemetry.Tests.csproj  # 119 pass, incl. 6 new gate tests\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj                # 85 pass\ndotnet test tests/Cli.Tests/Cli.Tests.csproj                        # 508 pass (1 pre-existing E2E failure requires packaged az binary; identical on main)\n```\n\nNew coverage in\n`tests/Core.Telemetry.Tests/TelemetryServiceEnabledGateTests.cs`:\n\n- `StartActivity` / `StartRootActivity` return `null` when\n`Enabled=false`, non-null when `true`.\n- Toggle `true → false` mid-life takes effect on the next call (Teresa's\nscenario).\n- Toggle `false → true` mid-life takes effect on the next call (mirror\nscenario).\n\nManual repro (PS, with a temporary\n`Console.Error.WriteLine(\"[TELEMETRY-PROBE] recorded {name}\")` in\n`TelemetryService.StartActivity` for observability):\n\nBefore the fix, Teresa's scenario:\n\n```text\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = true [global config]\n\nPS> Set-AzConfig -Key telemetry.enabled -Value false\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded SetConfigCommand.ProcessAsync\n\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root      # ← BUG: probe still fires\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = false [global config]\n```\n\nAfter the fix, same sequence:\n\n```text\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = true [global config]\n\nPS> Set-AzConfig -Key telemetry.enabled -Value false\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded SetConfigCommand.ProcessAsync\n\nPS> Get-AzConfig -Key telemetry.enabled\ntelemetry.enabled = false [global config]                # ← FIXED: no probe lines\n```\n\nMirror direction (fresh session, config already `false`):\n\n```text\nPS> Get-AzConfig -Key telemetry.enabled                   # no probe (gate short-circuits)\ntelemetry.enabled = false [global config]\nPS> Set-AzConfig -Key telemetry.enabled -Value true       # no probe (Set ran while disabled)\nPS> Get-AzConfig -Key telemetry.enabled\n[TELEMETRY-PROBE] recorded PowerShell.Activity.Root       # ← FIXED: mirror direction also live\n[TELEMETRY-PROBE] recorded SessionCreated\n[TELEMETRY-PROBE] recorded GetConfigCommand.ProcessAsync\ntelemetry.enabled = true [global config]\n```\n\nCLI verification (three consecutive `az` invocations with the same\nprobe) confirms the CLI change is parity-only, not load-bearing: after\n`az config set --key telemetry.enabled --value false`, the very next\n`az` invocation is a fresh process and its `CLITelemetryProvider` ctor\nreads the new value at startup — no probe output. The bug never had time\nto manifest in a single-shot CLI process.\n\n**History Notes**\n\n[Telemetry] Honor `telemetry.enabled` config changes without requiring a\nPowerShell module reload\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-27T11:30:36+10:00",
          "tree_id": "d38e04cd6803ab981031a781aa817bc6051bb224",
          "url": "https://github.com/Azure/azclips/commit/088027bf6672da1880260d1fe26bb147644485d0"
        },
        "date": 1785116472382,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 272289281.99,
            "unit": "ns",
            "range": "± 5093408.946344538"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 271828139.38,
            "unit": "ns",
            "range": "± 3378268.804801693"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 271994438.83,
            "unit": "ns",
            "range": "± 4853659.239306676"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "fbb38eb060fef1ac573e322efa914be6791b3ba0",
          "message": "{Benchmark} Compute extension benchmarks (#589)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\nAdd benchmarks for compute extension covering CRUD scenarios\nBenchmark filtering based on file changes\nParallelize benchmark runs between extensions\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>\nCopilot-Session: 57a95271-c097-4e9e-a319-e63a70ff8c25",
          "timestamp": "2026-07-27T13:23:26+10:00",
          "tree_id": "e7bb471d2db93f88ac364aada41fa4de09c5810b",
          "url": "https://github.com/Azure/azclips/commit/fbb38eb060fef1ac573e322efa914be6791b3ba0"
        },
        "date": 1785123409580,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 332477622.53,
            "unit": "ns",
            "range": "± 87633320.27421059"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 327868331.55,
            "unit": "ns",
            "range": "± 86291422.23498186"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 272520015.59,
            "unit": "ns",
            "range": "± 3745333.507302072"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "aryasadeghi@microsoft.com",
            "name": "aryasadeghi1",
            "username": "aryasadeghi1"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6506ee39d8d5b51f42ab9ef6c6f98a69457bd77b",
          "message": "{Tests} Add scenario test HTTP record/playback foundation (#562)\n\nRecordPlaybackInterceptor fills the tracing-step slot `Engine.cs`\nreserves for record/playback. Record mode captures each request/response\ninto a `SessionRecord` (Authorization is never stored); playback serves\nrecorded responses with no network and fails unmatched requests with a\ndiagnostic listing the remaining entries.\n\n`ScenarioHost` runs commands in-process through the real dispatcher and\nargument binders for both surfaces with stubbed auth.\n\nPOC: The parity tests show group create and `New-AzResourceGroup` emit\nidentical HTTP, so a single recording covers both CLI and PowerShell,\nincluding record-on-CLI then replay-on-PowerShell.\n\nPart of the scenario testing framework work (Addresses #470).\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-27T14:02:11+10:00",
          "tree_id": "8cafcf51d4b077c6d1d19e3179f14c83be373ea7",
          "url": "https://github.com/Azure/azclips/commit/6506ee39d8d5b51f42ab9ef6c6f98a69457bd77b"
        },
        "date": 1785125794799,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 285178027.96,
            "unit": "ns",
            "range": "± 7037169.990878279"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 285630677.73,
            "unit": "ns",
            "range": "± 8254259.308951448"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 283437927.88,
            "unit": "ns",
            "range": "± 5514027.418784189"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "zelinwang@microsoft.com",
            "name": "ZelinWang",
            "username": "wangzelin007"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "22f669f323c85efd6764ee0d885277da58be1535",
          "message": "{Core} Command not found error message adjustments (#554)\n\n**Related command**\n\n`az agent`, `az agent tool`, `az agent tool resource`\n\n**Description**\n\nTyping an Agent command group that has no runnable leaf produced the raw\ninternal error and then handed off to the AI agent:\n\n```\n$ az agent\n[ERROR] Command 'agent' not found for client type 'CLI'.\n  ⠋ Analyzing error root cause using AI ...\n```\n\ninstead of the standard, fast `CommandNotFound` that other groups (`az\ndeployment`, `az vm`, `az group`) already return:\n\n```\n$ az agent\nerror: [CommandNotFound] 'az agent' is not a recognized command. Run 'az agent --help' to see available commands.\n```\n\n**Cause.** Built-in verbs always resolve in `ExtensionDispatcher`, so\nthey skip the verb-only fallback that #525 added for installed\nextensions. The dispatcher then dispatched the non-runnable group to the\nextension, whose `ExtensionIndex` threw a generic `KeyNotFoundException`\nthat the orchestrator routes to the AI agent.\n\n**Fix.** A Core-level change to the dispatcher, mirroring #525. Add\n`LeafIndex.IsCommandGroup(verb, path)` — true when a longer leaf exists\nunder `(verb, path)` but the exact leaf does not — and gate dispatch on\nit, throwing `CommandNotFoundException` (`code=CommandNotFound`, exit 4)\nfor command groups. This now covers the built-in path that #525 could\nnot reach. A runnable root verb like `az login` (whose only leaf is its\nroot) is unaffected. No Agent extension code is touched; `az agent` is\nthe observable symptom because it is the built-in group exercised here.\n\n**Scope of the behavior change.** This is a general Core fix, not\nAgent-specific. The gate fires for any resolved built-in verb whose\ninvocation lands on a command group with no runnable root leaf — today\nthat is `agent`, `config`, `extension`, and `profile` (each seeded with\nchild leaves, none exposing a runnable root command). All four now\nreturn the standard `CommandNotFound` instead of falling through to the\nAI agent, which is the intended consistency fix (mirrors #525 for the\nbuilt-in path). `az agent` is simply the group reported in the bug.\nRunnable root verbs like `az login` / `az logout` (whose only leaf is\nthe root) are unaffected. Config-owned command *behavior* is untouched —\nonly the not-found routing is unified.\n\n**Testing Guide**\n\nBuilt from `azclips.slnx` and run against\n`artifacts/Release/Core.Cli/az.exe`:\n\n| Command | Result |\n| --- | --- |\n| `az agent` | `[CommandNotFound] 'az agent' ... Run 'az agent --help'`\n|\n| `az agent tool` | `[CommandNotFound] 'az agent tool' ...` |\n| `az agent tool resource` | `[CommandNotFound] 'az agent tool resource'\n...` |\n| `az agent tool env` (valid leaf) | executes normally, returns JSON |\n| `az agent tool resource find --name test --top 1` (valid leaf) |\nexecutes normally |\n| `az login --subscription-id ...` | resolves to the command (not a\ngroup); no regression |\n| `az logout` | executes normally |\n\nAutomated:\n\n- `dotnet test tests/Core.Extensions.Tests` — `LeafIndexTests` +6\n(`IsCommandGroup`)\n- `dotnet test tests/Cli.Tests --filter ExtensionDispatcher` — +5 (group\ngate, valid leaf, runnable root)\n\n**Known follow-up (out of scope):** a typo on a full leaf, e.g. `az\nagent tool resource findxxx`, still returns the old `client type 'CLI'`\nerror. That is a leaf-typo path, distinct from the command-group case\nfixed here, and applies to all extensions.",
          "timestamp": "2026-07-28T11:05:30+10:00",
          "tree_id": "343f9a5546daeca221cf7caf9fc40cb757e8eeaf",
          "url": "https://github.com/Azure/azclips/commit/22f669f323c85efd6764ee0d885277da58be1535"
        },
        "date": 1785201537287,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 352340422.49,
            "unit": "ns",
            "range": "± 88682067.71963122"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 345875251.5,
            "unit": "ns",
            "range": "± 88380236.26713233"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 293472579.73,
            "unit": "ns",
            "range": "± 3753882.4814544674"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "110961157+NoriZC@users.noreply.github.com",
            "name": "NoriZC",
            "username": "NoriZC"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "662ed09b05d145e320f50aff2c1cd6a2928a0da9",
          "message": "{Autocompletion} rename $matches to $completionResultMatches in completion script (#616)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\nThis PR fixes https://github.com/Azure/azclips/issues/613 by renaming\nthe $matches variable to a non built in variable name. No remaining\nPSScriptAnalyzer warnings in this script.\n\nCo-author [bjompen](https://github.com/bjompen)\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-28T18:36:22+10:00",
          "tree_id": "9983161b00ee15eb64492a6c079f1cc57a9efa5e",
          "url": "https://github.com/Azure/azclips/commit/662ed09b05d145e320f50aff2c1cd6a2928a0da9"
        },
        "date": 1785228645475,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 273421092.73,
            "unit": "ns",
            "range": "± 3732133.308579405"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 274562306.95,
            "unit": "ns",
            "range": "± 4410114.341423602"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 273692851.93,
            "unit": "ns",
            "range": "± 4098174.9491519677"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "naga.s.p.nandyala@gmail.com",
            "name": "Naga Nandyala",
            "username": "naga-nandyala"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "cd8b8efc3cdbfc3fbc6eb9005453c6b077a890ba",
          "message": "{DevOps} Remove dev branch references from CI workflows (#625)\n\n**Related command**\n\nN/A\n\n**Original PR**\n\n- #563 (https://github.com/Azure/azclips/pull/563)\n\n**Description**\n\nRemoves CI branch targeting that still referenced `dev` after the branch\nretirement.\n\nChanges in this PR:\n- Removed `dev` from GitHub Actions PR triggers in `build.yml`,\n`build-and-test.yml`, and `benchmark.yml`.\n- Removed `dev` from Azure Pipelines trigger include list in\n`azclips-beta-build.yml`.\n- Deleted `.github/workflows/backport-pr.yml`, which was designed around\nmain/dev backport directions.\n\n**Testing Guide**\n\n- Ran `git diff --check` on the resulting patch.\n\n**History Notes**\n\n{DevOps} Remove dev branch references from CI workflows\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\nCopilot-Session: 234beeee-b64b-41b9-b3fd-cdeea2bf0416",
          "timestamp": "2026-07-30T13:43:50+10:00",
          "tree_id": "bf643e5f502a411aaaafa892fefe608d27948e3e",
          "url": "https://github.com/Azure/azclips/commit/cd8b8efc3cdbfc3fbc6eb9005453c6b077a890ba"
        },
        "date": 1785383886073,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 288508671.03,
            "unit": "ns",
            "range": "± 4500919.945833892"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 289882565.46,
            "unit": "ns",
            "range": "± 4986812.1004803935"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 290592780.33,
            "unit": "ns",
            "range": "± 5125106.974813296"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "aryasadeghi@microsoft.com",
            "name": "aryasadeghi1",
            "username": "aryasadeghi1"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "dd1f7edf63f96ceefd4443c86bda5eef522bdc49",
          "message": "{Test} Add recording sanitisers and playback normalisation (#622)\n\nRecordings are scrubbed before they reach disk: subscription and tenant\nids become a fixed placeholder, bearer tokens are redacted, and the\nAuthorization and api-key headers are dropped. The same rewrites run on\noutgoing requests during playback so sanitised recordings still match,\nincluding under a different subscription. The list is central and\nextensible per scenario.\n\nAlso makes the interceptor safe for orchestrators that fan out requests\nin parallel, with tests driving record and playback from many threads.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-30T15:14:44+10:00",
          "tree_id": "d7b7512572a774a8e3e93ed2dd260856a1b9bf19",
          "url": "https://github.com/Azure/azclips/commit/dd1f7edf63f96ceefd4443c86bda5eef522bdc49"
        },
        "date": 1785389287542,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 279858910.76,
            "unit": "ns",
            "range": "± 6169253.787090272"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 280447895.19,
            "unit": "ns",
            "range": "± 3961722.3604787"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 334837947.73,
            "unit": "ns",
            "range": "± 92105309.16553637"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9cf355ae687974088e427ec06e499786e26a8cf3",
          "message": "{Benchmark} Skip extension loading for builtin extensions (#626)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n- Skip attempting to staging builtin extensions during benchmarking and\nuse binary built-in binaries only.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-30T16:14:44+10:00",
          "tree_id": "3a29c8d5149b8a84f6ee8998e1493cd75740cb17",
          "url": "https://github.com/Azure/azclips/commit/9cf355ae687974088e427ec06e499786e26a8cf3"
        },
        "date": 1785392908128,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 274006148.5,
            "unit": "ns",
            "range": "± 6607929.701200539"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 273675849.93,
            "unit": "ns",
            "range": "± 5279493.620508815"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 273214760.38,
            "unit": "ns",
            "range": "± 5146738.079486028"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "9c369859c0da432878ff9a979590b48b2d429fbc",
          "message": "{Help} Add help tools to solution file (#629)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Fixes IDE not building tools project\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-07-31T08:41:01+10:00",
          "tree_id": "dde5f716e8dab937e4be6b131f9bd480ffe0bb94",
          "url": "https://github.com/Azure/azclips/commit/9c369859c0da432878ff9a979590b48b2d429fbc"
        },
        "date": 1785452111505,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 284861625.04,
            "unit": "ns",
            "range": "± 7976959.032909262"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 348854305.43,
            "unit": "ns",
            "range": "± 142735497.24370775"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 333496807.66,
            "unit": "ns",
            "range": "± 146533051.79638952"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a0869b9c09226d742455a2982ae58dcca4614d48",
          "message": "{Core} Adjust raw console call sites to use output streams (#638)\n\n**Related command**\n\nExtension discovery / help path — affects the diagnostic surface any\nconsumer of `IEngine.OpenDiscoverySession` sees on extension-load\nfailure.\n\n**Description**\n\nFixes [Azure/CLIPS#545](https://github.com/Azure/CLIPS/issues/545) —\nfollow-up from PR #575 (issue #493). Was §6 bullet 3 of that PR's\nplanning doc.\n\nPR #575 harmonized diagnostic prefixes to `LEVEL: message`, routed\nwarn/info/verbose/debug to stderr, and level-gated everything through\n[`core.logging.level`](docs/azure-cli/docs-conceptual/config.md)\n(default: `warn`). A handful of `Console.*` sites were left unaddressed\nand still write raw to stdout/stderr regardless of `core.logging.level`\nor the caller's `OutputStreams` binding.\n\nChanges:\n\n- **[`DiscoverySession.cs`](src/Core/Core.Engine/DiscoverySession.cs)**\n— three `Console.Error.WriteLine` catch sites (L87, L116, L141) now\nroute through the session's `outputStreamAdapter.Warn(...)`. Prefix\nfollows PR #575 D6 (`[{nameof(DiscoverySession)}]`, matches\n[`MsalAuthService`](src/Core/Core.Authentication/MsalAuthService.cs#L52)).\nSeverity is `Warn` because the session degrades gracefully: it\naccumulates successfully-loaded extensions and returns them even when\none fails — the textbook definition of a warning. Matches\n[azure-cli-python\nprecedent](https://github.com/Azure/azure-cli/blob/dev/src/azure-cli-core/azure/cli/core/__init__.py)\n(extension-load failures at `WARNING`). Under the new `warn` default\nthey surface; under `error` they stay silent.\n- **[`FileExporter.cs`](src/Core/Core.Telemetry/FileExporter.cs#L93)** —\nstays raw, comment added. `Core.Telemetry` sits below `Core.Runtime` in\nthe layer stack; routing through `ILoggingService` would invert the\ndependency and risk recursion if any future logger path opens a\ntelemetry activity. Matches OpenTelemetry `SelfLog` / Serilog `SelfLog`\nconvention.\n-\n**[`CompletionGenerator.cs`](src/CLI/Completions/CompletionGenerator.cs)**\n— stays raw, class-level `<remarks>` added covering all five\n`Console.WriteLine` sites. Output is shell-completion protocol payload\nconsumed by tab-completion scripts, not diagnostic logging; must not be\nprefixed, level-gated, or stream-routed.\n\n**Behavior shift worth noting**\n\nThree of the four `DiscoverySession` call sites deliberately bind every\ndiagnostic stream to a no-op sink for silent internal probing:\n\n| Caller | `WriteWarning` binding | Post-change surface |\n| --- | --- | --- |\n|\n[`CompletionGenerator.cs`](src/CLI/Completions/CompletionGenerator.cs#L395)\n| no-op | silent |\n|\n[`DiscoveryBackedExtensionRegistry.cs`](src/Extensions/Extension/DiscoveryBackedExtensionRegistry.cs#L127)\n| no-op | silent |\n|\n[`CliExtensionServiceRegistration.cs`](src/CLI/CliExtensionServiceRegistration.cs#L128)\n| no-op | silent |\n|\n[`RuntimeHelpDispatcher.cs`](src/CLI/Help/RuntimeHelpDispatcher.cs#L263)\n| `_streams.WriteWarning` | visible on stderr |\n\nBefore this PR, the raw `Console.Error.WriteLine` fired unconditionally\nand leaked past every caller's \"stay quiet\" contract. After this PR,\nextension-load failures at the three no-op call sites become silent, and\nonly the help dispatcher's discovery path surfaces the line. This is the\nintended outcome of CLIPS#545 — routing through `ILoggingService` puts\ndiagnostic control back in each caller's hands.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx` — clean.\n- `dotnet test tests/Core.Engine.Tests/Core.Engine.Tests.csproj` — 2/2\npass.\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` — 516/524 pass, 7\nskipped, 1 pre-existing E2E failure\n(`ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`\n— CLI-binary path resolution on Windows) confirmed unrelated by stashing\nmy changes and reproducing on clean tree.\n- **Manual**: corrupted `Microsoft.Azure.Azclips.Extensions.Compute.dll`\nin-place (4 null bytes, keeping the filename so `BuiltinExtensionLoader`\nstill enumerates it) and ran `az vm --help`. Result: `WARNING:\n[DiscoverySession] Failed to load extension: Bad IL format.` on stderr,\nfollowed by `ExtensionNotFound` from dispatch. Other extensions loaded\nfine; graceful-degradation preserved.\n\n**History Notes**\n\n{Core} Route DiscoverySession extension-load failures through\nILoggingService.Warn\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-03T14:21:59+10:00",
          "tree_id": "85c674846d0a2cf3dbc46142c1233f2cd81b2720",
          "url": "https://github.com/Azure/azclips/commit/a0869b9c09226d742455a2982ae58dcca4614d48"
        },
        "date": 1785731751349,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 357714841.79,
            "unit": "ns",
            "range": "± 101659758.22503798"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 313509574.08,
            "unit": "ns",
            "range": "± 66623662.836388886"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 275595097.73,
            "unit": "ns",
            "range": "± 3116909.4807872577"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a1ab50be4b53ef1cde61530d9f25e2892a3179c4",
          "message": "{Telemetry} Add Jaeger for local telemetry monitoring (#605)\n\n- Add Jaeger for local telemetry monitoring\n- Debug build emits to local by default \n- Release build emits to remote by default\n- `AZCLIPS_TELEMETRY_TARGET` env variable overrides which telemetry sink\nto emit to regardless of build\n\n<img width=\"3836\" height=\"1867\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/8887a671-c6b9-4d68-8c6c-28b75c2ea35d\"\n/>\n<img width=\"3838\" height=\"1859\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/3f573fb1-5644-4996-a882-9e154ce15de3\"\n/>\n<img width=\"3836\" height=\"1873\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/e8897778-b5bf-4f1f-a997-f9e834e3e235\"\n/>\n<img width=\"3833\" height=\"1858\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/d69a3b27-0762-4a3f-9ad9-eee109735810\"\n/>\n\n---------\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>\nCopilot-Session: f8b1e1b9-860e-4341-9317-fddc46f8bbc9",
          "timestamp": "2026-08-04T10:02:13+10:00",
          "tree_id": "7a1442a0cc650dd708d1324844b0cb774a0617c9",
          "url": "https://github.com/Azure/azclips/commit/a1ab50be4b53ef1cde61530d9f25e2892a3179c4"
        },
        "date": 1785802533604,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 279068114.4,
            "unit": "ns",
            "range": "± 5762127.326769806"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 278039977.64,
            "unit": "ns",
            "range": "± 4255053.969685251"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 277661116,
            "unit": "ns",
            "range": "± 19663983.689705245"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "jaysontai@microsoft.com",
            "name": "JaysonTaiMicrosoft",
            "username": "JaysonTaiMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "776cba576b3b747e16f4d5bca469cff641a6d6c7",
          "message": "{Extensions} Add process caching for builtin extension loader (#642)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Add caching for builtin extension loader while loading managed\nassembly.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCopilot-Session: 32164222-c594-431a-b78d-7a1bc7bce947",
          "timestamp": "2026-08-04T00:25:23Z",
          "tree_id": "e24837e3d7bceaa789a265a623532ec90e8a15e3",
          "url": "https://github.com/Azure/azclips/commit/776cba576b3b747e16f4d5bca469cff641a6d6c7"
        },
        "date": 1785804019223,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 279201198.06,
            "unit": "ns",
            "range": "± 5277228.2254069885"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 280029389.78,
            "unit": "ns",
            "range": "± 4407462.333119387"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 278443001.67,
            "unit": "ns",
            "range": "± 11377548.967981698"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d32e45c6b500af660d79a50958575434021be431",
          "message": "{Core} Centralize console diagnostic rendering into `ConsoleSink` (#639)\n\n**Related command**\n\nConsole diagnostic rendering — internal.\n\n**Description**\n\nFixes [Azure/CLIPS#543](https://github.com/Azure/CLIPS/issues/543)\n(follow-up to PR #575).\n\nThree surfaces (`ConsoleLoggingService`, `ConsoleOutputAdapter`,\n`BuildStreams` in `Program.cs`) each independently picked a color,\nstream, and redirection guard. Refactor: introduce `ConsoleSink` as the\nsingle owner, delegate all three surfaces to it. Byte-preserving for\nexisting diagnostic paths; one intentional visual delta on the\nstructured-error path (see below).\n\n- **New** [`ConsoleSink.cs`](src/Core/Core.Runtime/ConsoleSink.cs) —\nlow-level primitives (`EmitStderr` / `EmitStdout`) + high-level\ndiagnostic methods (`Error` / `Warn` / `Info` / `Verbose` / `Debug`)\nwith canonical `LEVEL:` prefix.\n- All three surfaces collapse to one-line sink calls. `ColorPolicy` →\n`internal`.\n- `RecordTelemetry` **preserved verbatim** inside\n`ConsoleLoggingService` — not moved into the sink. Wiring it uniformly\nwould silently change telemetry semantics; keep for a separate design\ndiscussion.\n\n**One intentional visual delta**\n\n`Program.EmitStructuredError` previously wrote raw `error: [code]\nmessage` (lowercase, no color) directly to `Console.Error`. Routed\nthrough `ConsoleSink.Error`, so it now renders as `ERROR: [code]\nmessage` (red on non-redirected stderr, `NO_COLOR` / `FORCE_COLOR` /\nredirection guard respected via `ColorPolicy`).\n\nDocumentation is updated in the same PR to match — six pre-existing\nreferences to `error:` / `Error:` in\n[`eng-docs/spec/extension-management.md`](eng-docs/spec/extension-management.md),\n[`eng-docs/howto/extension-management.md`](eng-docs/howto/extension-management.md),\n[`wiki/onboarding-guide.md`](wiki/onboarding-guide.md), and\n[`wiki/zero-to-hero-guide.md`](wiki/zero-to-hero-guide.md) all move to\n`ERROR:`. Also resolves a pre-existing internal drift in\n`extension-management.md` (`error:` vs `Error:` on different lines).\nJSON output (`--output json`) is **unchanged** — that's the public\npayload shape, not a diagnostic prefix.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx` — clean.\n- New: `ConsoleSinkTests` — 9/9 pass.\n- Safety net (byte-preserving for diagnostic paths):\n`StreamRoutingIntegrationTests` 4/4, `TuiClassifyLineTests` 11/11,\n`CliLoggingPolicyTests` 11/11.\n- Full `Cli.Tests`: 525/533 pass (1 pre-existing unrelated failure).\n- Manual: reproduced CLIPS#545 corrupt-DLL test — `WARNING:` line\nbyte-identical to pre-refactor; `az vmss --help` now shows `ERROR:` red\n(previously `error:` white).\n\n**History Notes**\n\n{Core} Centralize console diagnostic rendering into `ConsoleSink`\n\n---\n\n- [x] Title prefix / release note conventions.\n- [x] Tests added for behavior changes.\n- [x] Docs updated where relevant.",
          "timestamp": "2026-08-04T11:41:35+10:00",
          "tree_id": "ec4397a0239b1aa19bc6a34a74a2a795f8f8c3b5",
          "url": "https://github.com/Azure/azclips/commit/d32e45c6b500af660d79a50958575434021be431"
        },
        "date": 1785808547948,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 305005299.7,
            "unit": "ns",
            "range": "± 64941462.8582521"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 280356225.01,
            "unit": "ns",
            "range": "± 5261108.538299353"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 281401225.19,
            "unit": "ns",
            "range": "± 8111159.985019964"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "msarfraz@microsoft.com",
            "name": "mansoor sarfraz",
            "username": "msarfraz"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "b2280c10f017e705318be61f21c4a8e3f397af66",
          "message": "{core} version error fixed for unrecognized arguments (#648)\n\nError message updated for unrecognized arguments\n<img width=\"743\" height=\"148\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/966ff2f0-1aa0-4b0a-8e44-8967cb21eb64\"\n/>\n\nCo-authored-by: Mansoor Sarfraz <msarfraz+microsoft@microsoft.com>",
          "timestamp": "2026-08-04T14:22:57+10:00",
          "tree_id": "c35be5a5fea0d0109e8acf2200139d7d929a3ffa",
          "url": "https://github.com/Azure/azclips/commit/b2280c10f017e705318be61f21c4a8e3f397af66"
        },
        "date": 1785818222059,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 279987725.4,
            "unit": "ns",
            "range": "± 4672707.956767795"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 280795925.19,
            "unit": "ns",
            "range": "± 5468423.21745367"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 280114412.14,
            "unit": "ns",
            "range": "± 5198528.626733625"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "dlanguiller@microsoft.com",
            "name": "DanielMicrosoft",
            "username": "DanielMicrosoft"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "7f017417140445116c6eeee33dd1903ca15e33c6",
          "message": "[CLI] Add `core.output.color` config option (auto/always/never) (#649)\n\n**Related command**\n\n`az config set core.output.color`, `az config get core.output.color`,\n`az config list --all`.\n\n**Description**\n\nFixes [Azure/CLIPS#604](https://github.com/Azure/CLIPS/issues/604)\n(follow-up to PR #639 / CLIPS#543, child of CLIPS#542).\n\nAdds `core.output.color` — three-state persistent color preference for\nCLI diagnostic rendering, matching `git config color.ui` / `cargo\nterm.color` shape (`auto | always | never`).\n\n- **New config key** in\n[`ConfigurationSchema.cs`](src/Core/Core.Configuration/Schema/ConfigurationSchema.cs)\nwith `AllowedValues=[auto,always,never]`, default `auto`.\n- **`ColorMode` enum + `Parse` + `Initialize`** in\n[`ColorPolicy.cs`](src/Core/Core.Runtime/ColorPolicy.cs). `Program.Main`\nresolves the mode once from config after DI init and calls\n`ColorPolicy.Initialize(mode)` before `BuildStreams`. No per-emission\nconfig lookup.\n- **Precedence** (top to bottom): `NO_COLOR` env → `FORCE_COLOR` env →\n`core.output.color = always` → `core.output.color = never` → `TERM=dumb`\n→ target-stream redirected → color on. Env sits above config because env\nis per-invocation override; config is persistent policy. Standard\nconvention (git / cargo / npm).\n- **`auto` (default)** preserves pre-config behavior byte-for-byte.\nUsers who never touch the setting see no change.\n- **CLI-scoped**. PowerShell coloring is controlled by the PS host\n(`$PSStyle` / `$Host.PrivateData`) and is not affected by this setting;\nseparate CLIPS issue if wanted later.\n- **Docs updated**: user-facing table in\n[`docs/azure-cli/docs-conceptual/config.md`](docs/azure-cli/docs-conceptual/config.md)\nand design notes in\n[`eng-docs/spec/config-implementation.md`](eng-docs/spec/config-implementation.md)\n§10.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx` — clean.\n- New: `ColorPolicyTests` — 16/16 pass (9 `Parse` cases + 7\nprecedence-matrix cases).\n- Safety net: `ConsoleSinkTests` 9/9, `StreamRoutingIntegrationTests`\n4/4, `TuiClassifyLineTests` 11/11, `CliLoggingPolicyTests` 11/11 — 51/51\ncombined.\n- Full `Cli.Tests`: 551/559 pass (1 pre-existing unrelated\n`ManagedIdentityE2ETests` failure).\n- Manual (CLI):\n- `az config set core.output.color=never; az vm --help` → no color even\non a live terminal.\n- `az config set core.output.color=always; az vm --help 2> log.txt` →\ncolor present in `log.txt` (redirection bypassed).\n- `NO_COLOR=1 az vm --help` with `core.output.color=always` → no color\n(env wins).\n- `az config unset core.output.color; az vm --help` → colors return to\nauto-detected behavior.\n\n**History Notes**\n\n[CLI] `az config set core.output.color`: Add `auto` | `always` | `never`\nfor persistent color preference\n\n---\n\n- [x] Title prefix / release note conventions.\n- [x] Tests added for behavior changes.\n- [x] Docs updated where relevant.",
          "timestamp": "2026-08-04T14:47:20+10:00",
          "tree_id": "13a6e4d1236a17508b5245c23a748c0747774074",
          "url": "https://github.com/Azure/azclips/commit/7f017417140445116c6eeee33dd1903ca15e33c6"
        },
        "date": 1785819674424,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Config",
            "value": 354539405.75,
            "unit": "ns",
            "range": "± 100469859.21417302"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Login",
            "value": 281928184.22,
            "unit": "ns",
            "range": "± 5185126.646203942"
          },
          {
            "name": "Benchmark.Startup.CliColdStartupBenchmarks.Extension",
            "value": 283296993.35,
            "unit": "ns",
            "range": "± 6344450.018421949"
          }
        ]
      }
    ]
  }
}