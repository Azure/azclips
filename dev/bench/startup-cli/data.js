window.BENCHMARK_DATA = {
  "lastUpdate": 1784526590448,
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
      }
    ]
  }
}