window.BENCHMARK_DATA = {
  "lastUpdate": 1784167776289,
  "repoUrl": "https://github.com/Azure/azclips",
  "entries": {
    "Azclips PS Startup (warm)": [
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
        "date": 1783661228464,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 273502934.16,
            "unit": "ns",
            "range": "± 4791207.446385271"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 264550350.62,
            "unit": "ns",
            "range": "± 5190031.615958057"
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
        "date": 1783667049827,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 275344953.99,
            "unit": "ns",
            "range": "± 3569422.8889160114"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 265483731.67,
            "unit": "ns",
            "range": "± 4710542.354059102"
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
        "date": 1783901809687,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 295746233.73,
            "unit": "ns",
            "range": "± 5815131.689026287"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 288551721.53,
            "unit": "ns",
            "range": "± 4849179.402097453"
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
        "date": 1783904092014,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 297300424.21,
            "unit": "ns",
            "range": "± 4607695.029009059"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 289649255.96,
            "unit": "ns",
            "range": "± 4773834.879938621"
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
        "date": 1783904308290,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 272468600.7,
            "unit": "ns",
            "range": "± 4811266.442438641"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 262325786.31,
            "unit": "ns",
            "range": "± 2779614.2990836482"
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
        "date": 1783917245436,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 269535771.35,
            "unit": "ns",
            "range": "± 3009441.1617775653"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 260188607.84,
            "unit": "ns",
            "range": "± 2928512.802142466"
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
        "date": 1783922099185,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 280301850.05,
            "unit": "ns",
            "range": "± 4542156.734717398"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 270979942.32,
            "unit": "ns",
            "range": "± 4744760.892750998"
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
        "date": 1783923407050,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 270164086.57,
            "unit": "ns",
            "range": "± 3628465.444017754"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 260752441.2,
            "unit": "ns",
            "range": "± 3171669.3413408934"
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
        "date": 1784010454369,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 272740135.57,
            "unit": "ns",
            "range": "± 3469458.2255835184"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 264449786.9,
            "unit": "ns",
            "range": "± 3859891.7341691703"
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
        "date": 1784092347558,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 311617609.36,
            "unit": "ns",
            "range": "± 3798092.752232058"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 299411531.73,
            "unit": "ns",
            "range": "± 4679116.408525468"
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
        "date": 1784167776223,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 274653600.68,
            "unit": "ns",
            "range": "± 3003021.705372658"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 266745409.94,
            "unit": "ns",
            "range": "± 3574348.8586520464"
          }
        ]
      }
    ],
    "Azclips PS Startup (cold)": [
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
        "date": 1783661231963,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 385110564.38,
            "unit": "ns",
            "range": "± 80552153.45040643"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 346658571.69,
            "unit": "ns",
            "range": "± 7864877.586762039"
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
        "date": 1783667053597,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 359010541.52,
            "unit": "ns",
            "range": "± 4838095.368351118"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 348005497.08,
            "unit": "ns",
            "range": "± 5783112.325453195"
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
        "date": 1783901812796,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 379112262.68,
            "unit": "ns",
            "range": "± 7368937.5031465795"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 368497658.3,
            "unit": "ns",
            "range": "± 5306772.176035062"
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
        "date": 1783904095381,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 376842402.13,
            "unit": "ns",
            "range": "± 6902690.764451912"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 364092683.13,
            "unit": "ns",
            "range": "± 4587145.868412903"
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
        "date": 1783904311696,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 353711340.22,
            "unit": "ns",
            "range": "± 4228984.545120109"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 351124872.71,
            "unit": "ns",
            "range": "± 34735624.88107886"
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
        "date": 1783917248839,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 355804841.53,
            "unit": "ns",
            "range": "± 5954415.05855779"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 346223366.67,
            "unit": "ns",
            "range": "± 9194261.144086478"
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
        "date": 1783922102540,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363623640.1,
            "unit": "ns",
            "range": "± 6336390.095163052"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 351027023.48,
            "unit": "ns",
            "range": "± 5654431.474672736"
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
        "date": 1783923410462,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 482973889.3,
            "unit": "ns",
            "range": "± 142361023.49774462"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 346756572.94,
            "unit": "ns",
            "range": "± 9117863.39124315"
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
        "date": 1784010458130,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 356826787.39,
            "unit": "ns",
            "range": "± 5663145.852862182"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 347264613.19,
            "unit": "ns",
            "range": "± 5905708.7288779635"
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
        "date": 1784092351022,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 402051748.11,
            "unit": "ns",
            "range": "± 8106939.46146008"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 397275497.4,
            "unit": "ns",
            "range": "± 4716862.112277172"
          }
        ]
      }
    ]
  }
}