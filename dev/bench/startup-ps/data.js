window.BENCHMARK_DATA = {
  "lastUpdate": 1788847035130,
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
        "date": 1784168864120,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 269571811.56,
            "unit": "ns",
            "range": "± 3412968.147550988"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 259377465.7,
            "unit": "ns",
            "range": "± 3601193.415203042"
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
        "date": 1784173267818,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 267657666,
            "unit": "ns",
            "range": "± 3769755.7322866987"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 258620631.45,
            "unit": "ns",
            "range": "± 2824742.892213308"
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
        "date": 1784187209542,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 304464027.65,
            "unit": "ns",
            "range": "± 4765283.012815938"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 293631452.07,
            "unit": "ns",
            "range": "± 3851445.1315480596"
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
        "date": 1784187724318,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 283114129.85,
            "unit": "ns",
            "range": "± 6060662.433403403"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 272371899.67,
            "unit": "ns",
            "range": "± 4677932.902860673"
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
        "date": 1784202731062,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 292173930.72,
            "unit": "ns",
            "range": "± 4897791.228828999"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 285214134.53,
            "unit": "ns",
            "range": "± 4283227.858204145"
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
        "date": 1784255749523,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 287690289.29,
            "unit": "ns",
            "range": "± 5579800.772325661"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 280864378.09,
            "unit": "ns",
            "range": "± 4457441.462748219"
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
        "date": 1784256018926,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 276362124.89,
            "unit": "ns",
            "range": "± 4294656.522493501"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 264633875.87,
            "unit": "ns",
            "range": "± 3436685.071482051"
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
        "date": 1784258043252,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 292452499.41,
            "unit": "ns",
            "range": "± 7660744.830518503"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 281204204.63,
            "unit": "ns",
            "range": "± 6419390.699244072"
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
        "date": 1784259656184,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 293505887.7,
            "unit": "ns",
            "range": "± 5497401.020039385"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 282977941.27,
            "unit": "ns",
            "range": "± 5124184.189691467"
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
        "date": 1784261268305,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 293328905.29,
            "unit": "ns",
            "range": "± 7311491.63187387"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 282065174.7,
            "unit": "ns",
            "range": "± 5505864.610442663"
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
        "date": 1784345511157,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 281239514.52,
            "unit": "ns",
            "range": "± 6724441.688610097"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 270668064.51,
            "unit": "ns",
            "range": "± 4696054.285818755"
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
        "date": 1784368512283,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 270944838.07,
            "unit": "ns",
            "range": "± 3023273.3709425326"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 259743033.94,
            "unit": "ns",
            "range": "± 3133301.9194862405"
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
        "date": 1784526592282,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 272756803.66,
            "unit": "ns",
            "range": "± 3272710.859809561"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 265961644.85,
            "unit": "ns",
            "range": "± 6703123.419324161"
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
        "date": 1784589566733,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 280404568.74,
            "unit": "ns",
            "range": "± 6358053.466485042"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 270533837.07,
            "unit": "ns",
            "range": "± 6644037.948764035"
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
        "date": 1784590766807,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 273488926.59,
            "unit": "ns",
            "range": "± 2812356.4814359024"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 264292498.17,
            "unit": "ns",
            "range": "± 3681825.601896092"
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
        "date": 1784693078434,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 288085535.7,
            "unit": "ns",
            "range": "± 6639004.370076211"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 281300167.61,
            "unit": "ns",
            "range": "± 4748515.792836162"
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
        "date": 1784694248195,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 272193409.75,
            "unit": "ns",
            "range": "± 2908912.2085992456"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 260448335.19,
            "unit": "ns",
            "range": "± 3336177.395061632"
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
        "date": 1784696304560,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271045096.35,
            "unit": "ns",
            "range": "± 3383489.265689249"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 263685997.51,
            "unit": "ns",
            "range": "± 3053460.9649564507"
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
        "date": 1784783689651,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 289348817.8,
            "unit": "ns",
            "range": "± 5763998.892739999"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 275964770.46,
            "unit": "ns",
            "range": "± 3970589.67580711"
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
        "date": 1784794094603,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 293391075.98,
            "unit": "ns",
            "range": "± 6107704.989226468"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 284236165.76,
            "unit": "ns",
            "range": "± 4252644.358386178"
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
        "date": 1785116470707,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271172366.86,
            "unit": "ns",
            "range": "± 3527950.9611033313"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 265064226.13,
            "unit": "ns",
            "range": "± 7240377.993069437"
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
        "date": 1785123407565,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271042802.5,
            "unit": "ns",
            "range": "± 3171956.841971283"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 261569614.77,
            "unit": "ns",
            "range": "± 3301231.70877833"
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
        "date": 1785125792783,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 282669085.36,
            "unit": "ns",
            "range": "± 7248676.129233136"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 276261236.15,
            "unit": "ns",
            "range": "± 6590133.23403336"
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
        "date": 1785201535418,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 295196148.35,
            "unit": "ns",
            "range": "± 6476568.432458189"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 286461308.57,
            "unit": "ns",
            "range": "± 3542719.8320943657"
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
        "date": 1785228643455,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271420369.37,
            "unit": "ns",
            "range": "± 3618361.9856714667"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 263265771.13,
            "unit": "ns",
            "range": "± 2716653.455189328"
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
        "date": 1785383884165,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 288844495.79,
            "unit": "ns",
            "range": "± 4667117.822564944"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 282310016.69,
            "unit": "ns",
            "range": "± 4525800.935771575"
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
        "date": 1785389285968,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 282629368.34,
            "unit": "ns",
            "range": "± 7545927.33217676"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 270144801.71,
            "unit": "ns",
            "range": "± 4424812.02203049"
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
        "date": 1785392906333,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271992402.27,
            "unit": "ns",
            "range": "± 3729652.906444613"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 262711949.96,
            "unit": "ns",
            "range": "± 2787304.633105356"
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
        "date": 1785452109821,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 279326005.85,
            "unit": "ns",
            "range": "± 5483302.430613066"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 269610943.55,
            "unit": "ns",
            "range": "± 4451398.190123654"
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
        "date": 1785731748557,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 273196683.03,
            "unit": "ns",
            "range": "± 3087495.3533136044"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 263414297.02,
            "unit": "ns",
            "range": "± 2878132.200262612"
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
        "date": 1785802531657,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 273933365.29,
            "unit": "ns",
            "range": "± 3644848.880766709"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 266840375.71,
            "unit": "ns",
            "range": "± 4321825.021181668"
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
        "date": 1785804017015,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 277507096.7,
            "unit": "ns",
            "range": "± 3746972.3036747635"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 266861654.61,
            "unit": "ns",
            "range": "± 3094119.4651533454"
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
        "date": 1785808546205,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 277827315.99,
            "unit": "ns",
            "range": "± 5492529.187431306"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 267777618.49,
            "unit": "ns",
            "range": "± 3890607.6914653243"
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
        "date": 1785818220297,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 276210821.52,
            "unit": "ns",
            "range": "± 6139194.782958761"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 267744379.21,
            "unit": "ns",
            "range": "± 5881046.872195289"
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
        "date": 1785819672632,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 276135867.98,
            "unit": "ns",
            "range": "± 3895326.302720267"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 267522876.43,
            "unit": "ns",
            "range": "± 3537834.102945982"
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
        "date": 1785904916812,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 287985030.78,
            "unit": "ns",
            "range": "± 4662321.512867119"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 279799742.53,
            "unit": "ns",
            "range": "± 6543213.243533214"
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
          "id": "d3b61011b21789d561c2144647f90e6988ea5d64",
          "message": "{Help} Change public facing references to Azclips (#667)\n\nFixes https://github.com/azure/azclips/issues/647\n\n## Summary\n\nRemoves the internal codename `AzClips` / `Azclips` from user-visible\nhelp text on both the CLI and PowerShell surfaces, so `az help`, `az\nconfig --help`, `az version --help`, the welcome banner, and\n`Get-Module` describe the products as **Azure CLI** and **Azure\nPowerShell**.\n\n## Changes\n\n**CLI help docs**\n\n- `src/CLI/Help/Docs/config/index.md` -- `summary` + `Description` now\nreference \"Azure CLI\" instead of \"AzClips\". (This is the exact string\nshown in the reporter's screenshot on `az help`.)\n- `src/CLI/Help/Docs/version/index.md` -- `Description` on `az version\n--help` now says \"Azure CLI\".\n\n**Welcome banner** (`src/CLI/Help/WelcomeBanner.cs`)\n\n- ASCII figlet regenerated in the same `small` font, now spelling\n**Azure CLI**.\n- Tagline simplified to `Welcome to the Azure CLI.` (dropped the\nmulti-host copy since the banner only fires from the CLI).\n- `az --help` hint line preserved.\n- `tests/Cli.Tests/WelcomeBannerTests.cs` assertion updated from\n`AzCLIPS` -> `Azure CLI`; all three banner tests pass.\n\n**PowerShell module manifests** (`Description` field only -- the string\nsurfaced by `Get-Module` / `Find-Module`)\n\n- `src/PS/Az/Az.psd1`\n- `src/PS/Compute/Compute.psd1`\n- `src/PS/Config/Config.psd1`\n- `src/PS/Login/Login.psd1`\n- `src/PS/Resources/Resources.psd1`\n\n## Explicitly not touched\n\n- Anything under `Agent/` (out of my area).\n- Real identifiers: `Microsoft.Azure.Azclips.*` package/assembly ids,\nthe `~/.azclips/` on-disk paths, `ProjectUri =\n'https://github.com/Azure/azclips'`, and the auto-generated `# Module\nmanifest for module 'Azclips.<X>'` header comment in each `.psd1`.\n- PSGallery `Tags` on `Compute.psd1` / `Resources.psd1` that still\ninclude `'AzClips'` -- happy to strip in a follow-up if reviewers want\nthem gone (they're gallery-search facets, not help text).\n\n## Validation\n\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter WelcomeBanner`\n-- 3/3 pass.\n- `az help` output grep for `AzClips|Azclips|AzCLIPS` -- now empty.\n- `Import-Module .\\src\\PS\\Login\\Login.psd1 -Force; Get-Module Login | fl\nDescription` -- reports `Azure authentication cmdlets for Azure\nPowerShell`.",
          "timestamp": "2026-08-07T00:58:24Z",
          "tree_id": "cc90a81df4b0593c6acd2bc8feeedfc6a20e58d0",
          "url": "https://github.com/Azure/azclips/commit/d3b61011b21789d561c2144647f90e6988ea5d64"
        },
        "date": 1786065611487,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 284607301.69,
            "unit": "ns",
            "range": "± 3067828.0166033395"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 272313040.78,
            "unit": "ns",
            "range": "± 3296291.272298871"
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
          "id": "5bd521582e94d0ecd9c92c41122859c897e531a3",
          "message": "[Config] Rename assemblyLoading extension-root config keys (breaking) (#668)\n\n## Summary\n\nRenames the two user-observable `assemblyLoading.*InstallationDir`\nconfig keys to a clearer bundled/installed vocabulary, and\ncorrespondingly renames the two extension-loader classes and their\n`AssemblyLoadingOptions` property counterparts.\n\nCloses Azure/CLIPS#533.\n\n## Motivation\n\nThe `Builtin*` and `MultiVersion*` names are legacy MVP terminology that\nno longer describes what the loaders do:\n\n- `BuiltinExtensionLoader` serves extensions **bundled** in the shipped\ninstall (`<install>/Extensions/`). This is orthogonal to the\ncompile-time `BuiltInExtensions.Map` \"Core\" seed, and the naming\ncollision has been a persistent source of confusion.\n- `MultiVersionExtensionLoader` serves extensions **installed** by the\nuser via `az extension add` under `~/.azclips/extensions/`.\n\"MultiVersion\" describes its filesystem layout, not its role.\n\nBridging XML docs introducing the target Core / Bundled / Installed\nvocabulary landed earlier in #550. This PR realizes the rename.\n\nThe `assemblyLoading.builtinInstallationDir` /\n`assemblyLoading.versionedInstallationDir` config keys are\nuser-observable via `az config`, so they are renamed in the same PR to\nkeep the C# property names and the user-facing keys in sync. Introducing\nan alias layer would extend the confusion; a clean break is preferred\nwhile the project is still in preview.\n\n## Changes\n\n### User-visible\n\n- Config key `assemblyLoading.builtinInstallationDir` →\n`assemblyLoading.bundledExtensionsRoot`\n- Config key `assemblyLoading.versionedInstallationDir` →\n`assemblyLoading.installedExtensionsRoot`\n- Descriptions updated in `docs/azure-cli/docs-conceptual/config.md`\n- CHANGELOG entry under **Upcoming Release → `#### CLI`**\n\nAny existing user config referencing the old keys must be renamed\nmanually. The old spellings now fail with `unknown key` via `az config\nset`.\n\n### Internal (mechanical)\n\n- Class `BuiltinExtensionLoader` → `BundledExtensionLoader`\n- Class `MultiVersionExtensionLoader` → `InstalledExtensionLoader`\n- Property `AssemblyLoadingOptions.BuiltinInstallationDir` →\n`BundledExtensionsRoot`\n- Property `AssemblyLoadingOptions.VersionedInstallationDir` →\n`InstalledExtensionsRoot`\n- Test file `BuiltinExtensionLoaderTests.cs` →\n`BundledExtensionLoaderTests.cs` (`git mv`)\n- Comment / XML-doc / log-tag string updates in\n`ExtensionLoadTracker.cs`, `CLIAssemblyLoadingProvider.cs`,\n`Program.cs`, `VersionInformationProvider.cs`, `CompletionGenerator.cs`,\n`CLIBenchmarkBase.cs`\n- In-scope wiki updates: `wiki/02-deep-dive/extension-system.md`,\n`wiki/02-deep-dive/core-engine-orchestration.md`,\n`wiki/onboarding-guide.md`, `wiki/zero-to-hero-guide.md`\n- Log-tag regex assertion in `StreamRoutingIntegrationTests.cs` updated\nto match the new class names\n\n### Explicitly out of scope (follow-up: Azure/CLIPS#622)\n\n- Deleting `BuiltInExtensions.Map` and the drift-guard test\n- Removing `IsBuiltInVerb` / `s_builtInPackageIds` and the \"Core wins\nover registry\" rule\n- Rewriting the \"Core tier\" narrative in `eng-docs/spec/extension-*.md`\n- Making `az extension update login` (and other Core-verb extensions)\nwork\n\nScratch design plan on the branch at\n`scratch/CLIPS-533-TEMP-PR-PLAN.md`.\n\n## History Notes\n\n`BREAKING CHANGE: [Config] Renamed extension-root config keys —\n'assemblyLoading.builtinInstallationDir' →\n'assemblyLoading.bundledExtensionsRoot' and\n'assemblyLoading.versionedInstallationDir' →\n'assemblyLoading.installedExtensionsRoot'. Existing user config\nreferencing the old keys must be updated manually; the old spellings now\nfail with \"unknown key\".`\n\n`[CLI] Renamed extension-loader classes 'BuiltinExtensionLoader' →\n'BundledExtensionLoader' and 'MultiVersionExtensionLoader' →\n'InstalledExtensionLoader' to match the on-disk tier they serve.`\n\n## Validation\n\n- `dotnet build azclips.slnx` — green (3 pre-existing warnings unrelated\nto this PR).\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` — 51 rename-affected\ntests pass (`BundledExtensionLoaderTests`,\n`ExtensionSelectionPathOverrideTests`,\n`ExtensionLoadContextDirectoryTests`, `StreamRoutingIntegrationTests`,\n`ConfigurationServiceTests`, `VersionInformationProviderTests`). One\npre-existing environmental failure\n(`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`) requires a\npublished `az` binary that isn't produced by a Debug build; not related\nto this change.\n- `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj`\n— 85/85 pass.\n- `git diff --stat`: 22 files, +178 / −172, all mechanical.",
          "timestamp": "2026-08-07T17:01:56+10:00",
          "tree_id": "58a3a98180678b00b24de8f2c46e6ba579e8d904",
          "url": "https://github.com/Azure/azclips/commit/5bd521582e94d0ecd9c92c41122859c897e531a3"
        },
        "date": 1786087223704,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 294648923.56,
            "unit": "ns",
            "range": "± 8488509.238908105"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 286109065.68,
            "unit": "ns",
            "range": "± 10828897.796645924"
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
          "id": "1ea1a66d32bb7df79bf9db236f8fef1cbe0484f0",
          "message": "[Config] Remove bundledExtensionsRoot from config (#676)\n\n## Summary\n\nRemoves the `assemblyLoading.bundledExtensionsRoot` config key. Bundled\nextensions are now resolved from a fixed candidate root list in\n`BundledExtensionLoader.GetCandidateRoots` — no runtime knob required,\npackaging always writes to `<install>/Extensions/`.\n\nCloses Azure/CLIPS#630.\n\n## Motivation\n\nThe config value was a leftover from an early env-var absorbed into the\nconfig system with no real user-facing purpose:\n\n- Packaging is hardcoded —\n[`inject_builtin_extensions.sh`](https://github.com/Azure/azclips/blob/main/devops/scripts/packaging/common/inject_builtin_extensions.sh),\nthe staging scripts, and every tarball/zip test all write bundled\nextensions to `<install>/Extensions/`. Zero references to\n`bundledExtensionsRoot` anywhere in `devops/`, `install/`, or\n`.azure-pipelines/`.\n- Users who want extra extensions already have `az extension add --path\n<nupkg>` and the Installed tier.\n- Keeping the config surface risked exactly the divergence class\ncaptured in Azure/CLIPS#624 / #629 — dispatch honors it, other consumers\ndon't.\n\nThe RELEASE default (`<install>/Extensions`) has been folded into the\nloader's candidate list, so packaged installs continue to work with no\nconfig knob.\n\n## Changes\n\n### User-visible\n\n- Config key `assemblyLoading.bundledExtensionsRoot` removed. `az config\nset assemblyLoading.bundledExtensionsRoot <path>` now fails with\n`unknown key`.\n- Doc row in `docs/azure-cli/docs-conceptual/config.md` deleted.\n- CHANGELOG entry under **Upcoming Release → `#### CLI`**.\n\n### Internal\n\n- `AssemblyLoadingOptions.BundledExtensionsRoot` property and\n`GetDefaultBundledExtensionsRoot` helper deleted.\n- New `public static string BundledExtensionLoader.GetDefaultRoot()` —\nthe same DEBUG/RELEASE computation, now sitting on the loader (its only\nconsumer).\n- `BundledExtensionLoader.GetCandidateRoots` becomes parameterless; the\nthree loader call sites (`LoadExtension`, `LoadExtension` error log,\n`GetInstalledExtensions`) updated.\n- Schema entry deleted from `ConfigurationSchema.cs`.\n\n### Tests\n\n- `BundledExtensionLoaderTests` restructured: each test stages under\n`BundledExtensionLoader.GetDefaultRoot()` with a Guid-suffixed short\nname so the process-static cache and enumeration stay isolated.\nPer-package cleanup instead of temp-root cleanup.\n- `ExtensionSelectionPathOverrideTests`: bundled fixtures moved to\n`GetDefaultRoot()`; installed fixtures still use temp dirs (that tier's\nconfig stays). Stub `TestConfigurationService` no longer sets the\nremoved property.\n- `BenchmarkAssemblyConfigurationService` and `CLIBenchmarkBase`\nupdated: `EnsureBuiltInPayload` now copies the Release Core-extension\nsource into `GetDefaultRoot()` so the benchmark's loader can discover it\nvia the fallback list.\n- `ConfigurationServiceTests`: assertion for the removed key deleted.\n\n## History Notes\n\n`BREAKING CHANGE: [Config] Removed\n'assemblyLoading.bundledExtensionsRoot'. Bundled extensions are now\ndiscovered from a fixed candidate root list ('<install>/Extensions' in\nRelease, plus the local Debug/Release build outputs). Any user config\nreferencing this key will fail with \"unknown key\".`\n\n## Validation\n\n- `dotnet build azclips.slnx` — green (3 unrelated pre-existing\n`GHCP001` warnings in `Agent/ChatAgent.cs`).\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` — 586 passing, 1\npre-existing environmental failure\n(`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`, needs a published\n`az` binary).\n- `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj`\n— 85/85.\n- Local `az config list --all` confirms the key no longer appears.\n- `git diff --stat`: 10 files, +240 / −238.",
          "timestamp": "2026-08-10T14:24:42+10:00",
          "tree_id": "5f9859cbf02dc860f87862203f698e01b79ba57a",
          "url": "https://github.com/Azure/azclips/commit/1ea1a66d32bb7df79bf9db236f8fef1cbe0484f0"
        },
        "date": 1786336913636,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 280625527.74,
            "unit": "ns",
            "range": "± 3674063.1374874483"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 270832971.05,
            "unit": "ns",
            "range": "± 3981050.587720819"
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
          "id": "031ec291d73dc41c9686f9596a801af57157d493",
          "message": "{Extensions} Honor assemblyLoading.installedExtensionsRoot in registry + install paths (#678)\n\n## Summary\n\nEvery consumer of the extension registry (the 6 `az extension *`\ncommands, tab-completion, runtime help, dispatch, the loader provider)\nnow goes through DI for `IExtensionRegistryStore`. The DI factory\nresolves the path from `assemblyLoading.installedExtensionsRoot` in\nexactly one place. `ExtensionInstaller` derives its install root the\nsame way. Paths no longer appear in any consumer code — they're a\nplumbing detail.\n\nCloses Azure/CLIPS#629.\n\n## The bug\n\n`assemblyLoading.installedExtensionsRoot` was only half-honored: the\nloader read the config, but every registry consumer\n(`FileExtensionRegistryStore`'s parameterless ctor, all six `az\nextension *` commands, `CompletionGenerator`, `RuntimeHelpDispatcher`,\n`ExtensionDispatcher`, `CLIAssemblyLoadingProvider`,\n`ExtensionInstaller.InstallPath`) reached for the hardcoded\n`~/.azclips/extensions/` default.\n\nRepro (broken before, fixed after):\n\n1. `az config set assemblyLoading.installedExtensionsRoot\n/custom/installed`\n2. `az extension add --path <pkg>.nupkg` — DLLs land under\n`/custom/installed/` (loader honored it)\n3. `az extension list` — **empty** (reads\n`~/.azclips/extensions/registry.json`)\n4. `az extension remove <pkg>` — **not found**\n5. `az <TAB>` completion — misses the extension's verbs\n\n## Design\n\nThe right shape wasn't \"sprinkle a path resolver everywhere.\" Every `new\nFileExtensionRegistryStore()` in `src/` was the actual bug — 9 sites\nbypassing DI. The fix is disciplined encapsulation:\n\n- **Delete** the parameterless `FileExtensionRegistryStore()` ctor + its\n`HomePaths.GetDefaultRegistryPath()` helper. Ctor now requires an\nexplicit path.\n- **Single seat of truth**: DI factory in\n`CliExtensionServiceRegistration` resolves\n`IConfigurationService.GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot`\nand joins `HomePaths.RegistryFileName`. This is the **only** place path\nresolution happens for the registry.\n- **Consumers get `IExtensionRegistryStore` via DI**; no consumer ever\nmentions a path.\n- **`ExtensionInstaller`**: `installPath` parameter removed. Ctor\nresolves the root internally from the same config key.\n- **`HomePaths.GetDefault*` stays** — it remains the fallback the\noptions bind to when the config key is unset, so default behavior is\nunchanged.\n\n## Changes\n\n### Product code (`src/`)\n- `Core.Extensions/FileExtensionRegistryStore.cs` — required-path ctor;\ndeleted `DefaultPath()` + `HomePaths` import\n- `Core.Extensions/EmptyExtensionRegistryStore.cs` — **new**, tiny\nread-only null-object impl used by the dispatcher's test-only ctor\n- `CLI/CliExtensionServiceRegistration.cs` — DI factory resolves the\npath from config\n- `CLI/AssemblyLoading/CLIAssemblyLoadingProvider.cs` — `registryStore`\nctor param is now required\n- `CLI/Parser/ExtensionDispatcher.cs` — deleted the private\n`FileExtensionRegistryStore`-based ctor; parameterless ctor uses\n`EmptyExtensionRegistryStore`\n- `CLI/Help/RuntimeHelpDispatcher.cs` — `IExtensionRegistryStore`\nthreaded through both ctors\n- `CLI/Program.cs` — `DispatchHelp` resolves the store from DI and\npasses it\n- `CLI/Completions/CompletionGenerator.cs` — 3 sites resolve via\n`AzclipsServiceProvider`\n- `Extensions/Extension/Add.cs, List.cs, Pin.cs, Remove.cs, Show.cs,\nUpdate.cs` — 6 processors resolve via `AzclipsServiceProvider`\n- `Extensions/Extension/ExtensionInstaller.cs` — `installPath` param\nremoved; resolves internally from config\n\n### Tests\n- `Core.Extensions.Tests/FileExtensionRegistryStoreTests.cs` — removed\n`DefaultPath_ShouldPointToUserProfile` (the concept no longer exists)\n\n## Testing\n\n- Full solution build: green, 0 warnings, 0 errors\n- `Core.Extensions.Tests`: **84/84**\n- `Cli.Tests`: **586/586** (excluding the pre-existing\n`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail` env flake unrelated\nto this change)\n\nIntegration test for the `az extension add → list → remove`\nconfig-override round-trip is a good follow-up but isn't gated on this\nPR — the DI factory is the one place a regression could re-introduce the\nbug, and it's covered by construction.\n\n## Non-goals\n\n- Not renaming the config key (already done in azclips#668).\n- Not removing `HomePaths.GetDefaultExtensionsRoot()` /\n`GetDefaultRegistryPath()` — they remain the default the options bind\nto.\n- Not touching Bundled-tier discovery (azclips#669 covered the\ntab-completion side; a broader Bundled audit belongs under a follow-up\nif #624 turns up more).\n\n## References\n\n- Investigation umbrella: Azure/CLIPS#624\n- Bundled companion fix: azclips#669 (merged)\n- Config-key rename that surfaced this: Azure/CLIPS#533 / azclips#668\n- Immediately-preceding removal of `bundledExtensionsRoot`:\nAzure/CLIPS#630 / azclips#676\n- Sub-issue of Extension framework epic: Azure/CLIPS#209",
          "timestamp": "2026-08-11T15:06:43+10:00",
          "tree_id": "c83a74cfd08fbdded87eb3d5714107acbe5a7c43",
          "url": "https://github.com/Azure/azclips/commit/031ec291d73dc41c9686f9596a801af57157d493"
        },
        "date": 1786425788545,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 310870771.97,
            "unit": "ns",
            "range": "± 7905296.260824118"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 296783439.15,
            "unit": "ns",
            "range": "± 4376573.403122205"
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
          "id": "bc608d1a5d7d888b61154d85dc5cf472dedacd34",
          "message": "{Extensions} Delete BuiltInExtensions.Map and treat all extensions as bundled or installed (#683)\n\n## Summary\n\nDeletes the compile-time \"Core\" tier from the runtime. Login, Config,\nExtension, and Agent become ordinary extensions — same discovery, same\ndispatch, same install/remove/update semantics as Compute, Resources, or\nany third-party extension.\n\nCloses Azure/CLIPS#622.\n\n## What actually changes for users\n\n- **`az extension add`, `remove`, `update` work for former Core verbs.**\nTrying to `az extension update login` used to fail with a `VerbConflict`\nrefusal (\"`login` is a built-in verb…\"); it now upgrades the shipped\nLogin extension in place. Same for `remove` and `add` when a package\nlegitimately declares one of these verbs.\n- **Tab completion sees installed-tier extensions.** `az\n--completion-candidates` and `az --completions` now enumerate through\nthe same loader chain the runtime uses, so an extension installed via\n`az extension add` under a config-overridden root surfaces in\ncompletions at every depth. Previously the completion generator only\nscanned bundled roots — the exact gap I flagged in the [#622\ncomment](https://github.com/Azure/CLIPS/issues/622#issuecomment-5247698834)\nafter CLIPS-629's runbook.\n- **`(verb, path)` collision protection stays.** The classic-Azure-CLI\nshadowing-gap guards (`LeafConflict` at install time, duplicate-leaf\nhard-fail at index build) are untouched. #622 removes only the\nCore-specific refusals; general conflict detection between independent\nextensions still fires.\n\n## Design in one line\n\nRuntime knows two tiers (Bundled + Installed). Every leaf comes from\ndiscovery over both loaders. No static seed, no \"Core wins\" precedence,\nno verb-refusal in `az extension *`. Dispatch is uniform.\n\n## Implications\n\n- **Bootstrap paradox left as-is.** `az extension remove extension`\n(removing the extension-management extension itself) is a self-inflicted\ncorruption with no clean in-band recovery. Same class of problem as `apt\nremove dpkg`. Treated as manual CLI reinstall, per the issue's explicit\nnon-goal.\n- **Packaging profile is now the sole source of truth for \"what ships in\nevery install.\"**\n[`devops/scripts/packaging/config/{lean,full}_package.config`](devops/scripts/packaging/config/)\ndrives the injector; the runtime no longer duplicates that list.\n- **Add.cs's pre-existing gap (doesn't persist `Contributions` to\n`registry.json`) becomes moot for completion.** Phase 4's rewrite drops\nthe dependency; `Contributions` is now a runtime-only projection built\nfrom discovery. Registry stays a preferences store.\n\n## Testing\n\n- Build clean across the solution.\n- `Cli.Tests` 580/587 · `Core.Extensions.Tests` 78/78 · `Scenario.Tests`\n42/42 (7 test facts removed as part of the deletion; the pre-existing\n`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail` env flake is the\nonly fail).\n- Smoke-tested on the compiled binary end-to-end:\n  - `az config list`, `az group --help`, all Core verbs route unchanged.\n- Installed Resources (staged under `~/.azclips/extensions/` after\ndeleting the bundled copy) is discovered, dispatched, help-rendered, and\ncompleted correctly.\n- `az --completion-candidates group` → `create list show`; `az\n--completion-candidates deployment group` → `create what-if` — the\nCLIPS-629 runbook's B8–B12 checks that had been deferred to this PR now\npass.\n- `az extension remove --name login` returns `ExtensionNotFound`\n(ordinary not-installed error) instead of the old `VerbConflict`\nrefusal.\n\n## Non-goals\n\n- Not renaming loader classes (already done in azclips#668).\n- Not changing packaging or the packaging profile files.\n- Not adding compat-range enforcement (`AzclipsCompatibility` +\n`CompatibilityGuard` already exist).\n- Not solving the bootstrap paradox.\n\n## References\n\n- Parent epic: Azure/CLIPS#209 (Extension framework support)\n- Config-honor precursor (merged): Azure/CLIPS#629 / azclips#678\n- Rename precursor (merged): Azure/CLIPS#533 / azclips#668\n- Follow-up scoped into this PR via [#622\ncomment](https://github.com/Azure/CLIPS/issues/622#issuecomment-5247698834):\ntab-completion enumeration",
          "timestamp": "2026-08-12T15:51:13+10:00",
          "tree_id": "69e9f3d514e3d362b3fc6e86480a4d96d6ffc9fb",
          "url": "https://github.com/Azure/azclips/commit/bc608d1a5d7d888b61154d85dc5cf472dedacd34"
        },
        "date": 1786514793679,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 283138521.77,
            "unit": "ns",
            "range": "± 4706112.683873544"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 267477148.82,
            "unit": "ns",
            "range": "± 3808848.0314416317"
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
          "id": "675d1caed1f399cf57aa89ce0bb646e65318de06",
          "message": "[Extensions] `extension list` displays all available candidates (#688)\n\nCloses https://github.com/Azure/CLIPS/issues/643\nCloses https://github.com/Azure/CLIPS/issues/534\n\n`az extension list` will now show all available extension candidates\n(not just the extension that will dispatch). Note here, `0.1.1` and\n`0.1.2` are listed, along with their location (one bundledIn install\nlocation, one externally installed).\n\n`0.1.1` is pinned, so it is listed as `selectedVersion`.\n\n\n<img width=\"1882\" height=\"712\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/ff1b9e4a-8e55-4b18-b3d2-06f2b94e23fc\"\n/>\n\nUnpinning will show `0.1.2` as selected version now (it wins selection\nrules: higher semver), but `0.1.1` is still listed as a candidate:\n<img width=\"1898\" height=\"710\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/485ed055-fa92-4943-9b79-f75a37e1a91c\"\n/>\n\n---\n\n## Implementation notes\n\nBoth issues live under Azure/CLIPS#209.\n\n**CLIPS-534** - flip `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>`\non the five dispatchable extension csprojs (Compute, Config, Extension,\nLogin, Resources) so the SDK stamps `AssemblyInformationalVersion` on\neach DLL; extend `BundledExtensionLoader.ResolvePackageVersion` with a\n`MetadataLoadContext` fallback that reads the attribute (stripping the\n`+sha` suffix) when the `.nupkg` is stripped by packaging. Local builds\nstill hit the `.nupkg` scan first, so behavior is unchanged there.\n\n**CLIPS-643** - new\n`DiscoveryBackedExtensionRegistry.BuildDiscoveredWithCandidates` returns\nboth the collapsed `ExtensionRegistry` and per-package\n`SelectedExtensionCandidate` (Selected + Ignored).\n`ListExtensionsProcessor` projects that into a `candidates: []` array\nand a `selectedVersion` field. Tier is inferred by path-matching\n`Location` against `assemblyLoading.installedExtensionsRoot`.\n\n**Shape is additive** - every existing top-level field on `az extension\nlist` output survives; `version` still equals `selectedVersion` for\nbackward compat.\n\n**Follow-up captured on Azure/CLIPS#489**: `az extension pin --version\n<v>` becomes the natural companion once side-by-side install lands.\n\n## Testing\n\n- 4 unit tests for `AssemblyVersionReader` (real assembly, missing dir,\nmissing DLL, unreadable DLL).\n- 2 loader integration tests (fallback-hit +\nfallback-miss-with-empty-DLL).\n- 6 unit tests for `BuildCandidates` / `InferTier` (single-tier,\ndual-tier no pin, dual-tier with older-Bundled pin, null-selection\nfallback, `InferTier` branches).\n- Existing `ExtensionListByVerbTests` updated for the new record shape.\n- Live-verified scenarios shown in the screenshots above (pin, unpin,\n`--by-verb`).\n\n593/601 in `Cli.Tests` pass; 1 pre-existing `ManagedIdentityE2ETests`\nfailure is environment-dependent and unrelated.",
          "timestamp": "2026-08-13T15:32:36+10:00",
          "tree_id": "537caa43e380266c9b4805c12270df482c1da2ce",
          "url": "https://github.com/Azure/azclips/commit/675d1caed1f399cf57aa89ce0bb646e65318de06"
        },
        "date": 1786600032901,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 289354999.09,
            "unit": "ns",
            "range": "± 4515337.817046825"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 277289743.38,
            "unit": "ns",
            "range": "± 3337318.452882811"
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
          "id": "28dbe75d719d30a8e5ee4d8340c232e3d1ce78a7",
          "message": "{Telemetry} Trace startup time for CLI and PS module import (#685)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n- Add startup performance context to track start up time before\nTelemetryService initialisation\n- Replay startup performance context as part of trace hierarchy for CLI\ncommands\n- Instrument more sections of Program.cs entry point for better\ntraceability.\n- Measure PS import time as separate trace activity on cmdlet import\n\n<img width=\"2463\" height=\"974\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/afebf67b-2d80-456c-b5e8-feee6b15b273\"\n/>\n\n<img width=\"2453\" height=\"1151\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/c689ab7a-31b8-4c75-b5d5-3e0274dd0bdf\"\n/>\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-17T10:15:24+10:00",
          "tree_id": "02d17f3b1985d236ed123ce1e63f5819d62aa82f",
          "url": "https://github.com/Azure/azclips/commit/28dbe75d719d30a8e5ee4d8340c232e3d1ce78a7"
        },
        "date": 1786927204071,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 273049840.13,
            "unit": "ns",
            "range": "± 5866833.41483058"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 260566404.37,
            "unit": "ns",
            "range": "± 5943981.537611188"
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
          "id": "0d9127fea4d40e12cdf08ae2de77a6d985bfbc45",
          "message": "{Tests} Add the vm create scenario with a live recording (#695)\n\nFirst orchestration scenario for the record/playback framework (CLIPS\ntask [#564](https://github.com/Azure/CLIPS/issues/564), scenario side;\nthe benchmark handoff follows separately).\n\n`az vm create` / `New-AzVM` is recorded once as a whole: the resource\ngroup, vnet and nsg in parallel, the nic built from their returned ids,\nand the vm as a real LRO with its polling. The committed recording is\nreal ARM traffic from a live run, 15 entries, replaying credential-free\non both surfaces in about two seconds.\n\nSupporting changes the live run drove: playback serves `Retry-After: 0`\nso recorded poll delays cost nothing; the sanitisers scrub passwords and\ndrop ARM's `x-ms-operation-identifier` header (tenant and object ids),\nwith a matching secret-scan rule; the harness supports multi-extension\nscenarios since vm create needs its resource group first.\n\nConcurrency is proven directly: the parallel vnet/nsg entries replay\nwith their recorded order deliberately swapped. Re-recording steps are\nin the README.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-17T11:43:19+10:00",
          "tree_id": "4e294cb510d24067a760b0dfb30d8c4abbed3f78",
          "url": "https://github.com/Azure/azclips/commit/0d9127fea4d40e12cdf08ae2de77a6d985bfbc45"
        },
        "date": 1786931912091,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 311502639.18,
            "unit": "ns",
            "range": "± 6007426.398026537"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 293419329.85,
            "unit": "ns",
            "range": "± 4882284.839167588"
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
          "id": "fd3287836c3d90de1936fbcca2d65a4da35666d2",
          "message": "{Help} MsLearn export tooling for CLI (#691)\n\nRef [CLIPS#588](https://github.com/Azure/CLIPS/issues/588).\n\nAdds the in-repo tooling for AzClips CLI reference to reach\n`learn.microsoft.com` via the same pipeline `azure-cli` uses. The\nout-of-repo pieces (publish workflow, cross-repo PRs, target docs repo)\nare separate CLIPS issues.\n\n## What ships\n\n- **`Azclips.Cli.Export`** (new build-time tool at\n`tools/Help/Azclips.Cli.Export/`) -- walks each extension's\n`IExtensionIndex`, builds `HelpManifest`, projects into CLI2Yaml's\nsnake_case `HelpFile` JSON contract, writes one file per top-level verb\nto `artifacts/cli-export/latest/<verb>.json`.\n- `dotnet run --project tools/Help/Azclips.Cli.Export` is the whole\ninvocation.\n- Optional `--extension <name>` filters to a single extension for\ndev-loop / partial regen.\n- Emits a synthesised `_root_.json` for the root `az` group (CLI2Yaml\nrequires it).\n- **Metadata attributes** for docs-only fields on parameters and\ncommands:\n- `[ParameterGroup(\"Storage Arguments\")]` -> CLI2Yaml\n`parameter.group_name`\n- `[PopulatorCommand(\"az vm list-sizes\")]` -> `parameter.value_sources`\n- `ProcessorInfoAttribute` extended with `PreviewMessage`,\n`ExperimentalMessage`, `DeprecatedMessage` -- Tags gate emission;\n`[Breaking Change]` sentinel routes to `breaking_change_info`.\n- **Side-car config files** at `src/CLI/Help/` --\n`.docsreference.azclips.json` (E1), `service_name.json` (E2),\n`index.json` (E3). Placeholder content per PR discussion.\n- **`build/Get-DocCommitMap.ps1`** -- generates `doc_commit_map.json`\n(E4) at CI time. Powers Learn's \"Edit this doc\" links and \"Last updated\"\ndates.\n- **PR-time fixtures + CI diff** -- small representative set (`_root_`,\n`login`, `vm`) at\n`tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/`. The\n`help-tool-tests` job fails loudly on shape drift with a message\npointing at `pwsh build/Update-CliExportFixtures.ps1`.\n\n## Globals -- single source of truth\n\n`Program.cs` (CLI2Yaml docs pipeline) and `RuntimeHelpRenderer` (in-tool\n`az X --help`) previously had no shared definition of global arguments.\nThis PR adds:\n\n- **`KnownGlobalFlags.All`** in `src/CLI/Parser/` -- public descriptor\nlist with `ParseAction` per flag.\n- `GlobalArgParser.Parse` dispatches through this list (`ParseAction`\nmirrors argparse's `Action` pattern used by `knack`/azure-cli).\n- `RuntimeHelpRenderer` renders a `Global Arguments` section at the\nbottom of every command / group `--help`.\n- Docs exporter's `GlobalArguments.cs` is a thin projection into\n`HelpFileParameter[]`.\n- Drift test in `Cli.Tests` asserts every declared global is recognised\nby the parser.\n\nNet effect: adding a new global flag = **one edit** in\n`KnownGlobalFlags.All`. Docs and runtime stay in sync by construction.\n\n## What it does NOT do\n\n- **Publish workflow** -- a bot PR that clones the target docs repo and\npushes YAML. Blocked on the target docs repo decision and `apidrop` feed\nauth on GH Actions runners.\n- **Markdown well-formedness preflight** (Markdig over `contentType:\nmarkdown` fields) -- planned alongside the publish workflow.\n- **Realistic per-verb `service_name.json` mapping** -- everything\ncurrently maps to `azure-cli`. Docs-review pass.\n- **Realistic `.docsreference.azclips.json` template copy** --\nplaceholder strings. Templates never render at emit time under Q3\nall-core anyway.\n- **Runtime `--help` parameter grouping** -- the `[ParameterGroup]`\nannotations flow through `HelpParameter.GroupName`;\n`RuntimeHelpRenderer` still lists parameters flat. One-file follow-up.\n- **`CLI2Yaml editLink` hardcode fix** (BL1) -- upstream PR to CLI2Yaml\nor regex-swap in the publish workflow. Not this PR.\n\n## Verification\n\n**Build + tests**:\n\n```\ndotnet build azclips.slnx --configuration Debug\ndotnet test azclips.slnx --no-build --configuration Debug\n```\n\nExpect all green except the pre-existing Windows-only\n`ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`.\n\n**Exporter**:\n\n```\ndotnet run --project tools/Help/Azclips.Cli.Export/Azclips.Cli.Export.csproj --no-build --configuration Debug\n```\n\nExpect `Wrote 11 verb file(s) + _root_.json to\n<repo>/artifacts/cli-export/latest`.\n\n**CLI2Yaml round-trip smoke** (needs Microsoft-tenant machine + apidrop\nfeed auth via silent MSAL): full runbook at\n[scratch/CLIPS-588-CLI2YAML-SMOKE-RUNBOOK.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/CLIPS-588-CLI2YAML-SMOKE-RUNBOOK.md).\nProduces `YamlMime:AzureCLIGroup` files under\n`%TEMP%\\azclips-cli2yaml-smoke\\output\\latest\\`.\n\n## Design context\n\nDesign and phase-by-phase implementation notes live at:\n\n-\n[scratch/CLIPS-588-TEMP-PR-PLAN.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/CLIPS-588-TEMP-PR-PLAN.md)\n-- design decisions (Q1-Q5), blockers (BL1-BL6), validation gates.\n-\n[scratch/CLIPS-588-A-IMPLEMENTATION-PLAN.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/CLIPS-588-A-IMPLEMENTATION-PLAN.md)\n-- 9-phase execution log with every checkpoint verified.\n-\n[scratch/AZCLIPS-TO-MSLEARN-LOOP.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/AZCLIPS-TO-MSLEARN-LOOP.md)\n-- pipeline spec (source of truth for the Loop page).\n\nTwo decisions worth calling out for the reviewer:\n\n- **We reuse CLI2Yaml** (docs.ms NuGet) rather than emit YAML ourselves.\nBypasses ~500 lines of transform reimplementation and inherits\n`azure-cli`'s operational path. Trade-off discussion in the Loop doc.\n- **All-core model** (`command_source: null` on every emitted record).\nAzClips has no author-visible core-vs-extension distinction today;\nexposing one would leak an implementation detail into\n`learn.microsoft.com`. Revisit if we ship a truly external extension.",
          "timestamp": "2026-08-17T17:32:32+10:00",
          "tree_id": "6e408cdfda4fe79a823c1dc37997a3f228a1be3a",
          "url": "https://github.com/Azure/azclips/commit/fd3287836c3d90de1936fbcca2d65a4da35666d2"
        },
        "date": 1786952760935,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 270888495.36,
            "unit": "ns",
            "range": "± 5347100.566764303"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 257879410.45,
            "unit": "ns",
            "range": "± 5375909.669040543"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "299377795+azure-client-tools-agent[bot]@users.noreply.github.com",
            "name": "azure-client-tools-agent[bot]",
            "username": "azure-client-tools-agent[bot]"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "2eee9597baee3f61cfaa258e5046865fc1d8a0bb",
          "message": "{DevOps} Trigger stable release 0.1.1 (#697)\n\n**Description**\n\nPrepare stable release 0.1.1.\n\n- Updates src/CLI/Core.Cli.csproj\n- Updates the root CHANGELOG.md\n- Updates src/PS/Az/Az.psd1\n- Updates PowerShell module changelogs and manifests\n\n**After merge**\n\nThe release trigger validates the version-only change on main, tags the\nmerged commit as v0.1.1.<UTC yyyyMMddTHHmmssZ>, and starts the stable\nbuild, signing, and publishing flow.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-08-18T09:10:03+10:00",
          "tree_id": "19b7e9312490ae409b2b74a2d62a607c6a95420b",
          "url": "https://github.com/Azure/azclips/commit/2eee9597baee3f61cfaa258e5046865fc1d8a0bb"
        },
        "date": 1787009488758,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 268040290.45,
            "unit": "ns",
            "range": "± 4984411.166260381"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 255187395.45,
            "unit": "ns",
            "range": "± 4546274.813279884"
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
          "id": "c0bd57c62e609ec484ea3423baab9c1d6ae590ef",
          "message": "{Help} Markdig well-formedness preflight (#706)\n\nAdd a build-time Markdig-based validator that parses each CLI help\nmarkdown file and flags inline patterns that render as surprising HTML\nthrough docfx's downstream Markdig pipeline. Complements the existing\nMarkdownStructureValidator, which handles structural checks; this one\ncatches inline-prose issues that pass structural checks but break at\nrender time.\n\nTwo new problem IDs:\n- MD_SuspiciousHtmlTag: <foo>-style placeholders that Markdig treats as\nHTML inlines (only a small allowlist of real HTML tags is accepted).\n- MD_UnbalancedBackticks: odd backtick counts inside paragraph text\n(fenced code blocks are excluded).\n\nWired into Azclips.Help.Tool's aggregate report so both PR CI (GitHub\nActions) and the internal build already exercise it, no new step needed.\n\nRuns clean against the current CLI docs corpus (11 verb docs, zero\nfindings). 7 unit tests cover clean content, placeholder detection,\nallowed real HTML, balanced/unbalanced backticks, fenced-code exclusion,\nand multi-issue aggregation.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-18T12:03:40+10:00",
          "tree_id": "6460fdb7d5c3ae97f4da6ad09a90b6d08f37ac76",
          "url": "https://github.com/Azure/azclips/commit/c0bd57c62e609ec484ea3423baab9c1d6ae590ef"
        },
        "date": 1787019683948,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 266175830.26,
            "unit": "ns",
            "range": "± 6384303.949398912"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 252670622.6,
            "unit": "ns",
            "range": "± 4921656.496881123"
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
          "id": "a0947abac40ca61ef46b33f2f9c309c09219e2fa",
          "message": "[Help] Group --help parameters into named sections (#711)\n\n## Summary\n\nParameters in `az <verb> --help` output can now be grouped into named\nsections via a new `[ParameterGroup]` attribute on processor properties.\nFirst adoption: `az vm create` splits its 22 parameters across\n`Arguments` / `Authentication Arguments` / `Network Arguments`, matching\nreal Azure CLI conventions.\n\nCloses the runtime loop on the 588 track — the exporter side already\nemitted `group_name` fields from 588-A (#691); this PR teaches the\nruntime help renderer to consume them.\n\n## User-visible impact\n\n`az vm create --help` before this PR: 22 parameters rendered flat under\na single `Arguments` header.\n\nAfter:\n\n    Command\naz vm create : Create a virtual machine and its supporting network\nresources.\n\n    Arguments\n        --subscription-id [Required] : Azure Subscription Id.\n        --name [Required]            : The name of the virtual machine.\n        --location [Required]        : Location for all resources.\n        --resource-group [Required]  : The resource group name.\n        --vm-size [Required]         : The size of the virtual machine.\n--profile : Use the named profile for this command ...\n\n    Authentication Arguments\n        --admin-username [Required]  : The administrator username.\n        --admin-password [Required]  : The administrator password.\n\n    Network Arguments\n        --network-interface-name [Required] : ...\n        --virtual-network-name [Required]   : ...\n        ... 15 params\n\n    Global Arguments\n        --debug ...\n\nUn-annotated commands (`az login`, `az config get`, etc.) render\nbyte-identically to `main` — the mechanism is dormant until an attribute\nfires.\n\n## Implementation\n\n- **`RuntimeHelpParameter.GroupName`** — new nullable field on the\nruntime help contract.\n- **`ProcessorParameterReflection`** — new shared helper in\n`src/CLI/Help/`. Both the build-time exporter\n(`ExtensionHelpManifestBuilder`) and the runtime adapter\n(`ExtensionIndexHelpAdapter`) call it to read `[ParameterGroup]` off the\nprocessor property. Consolidating here keeps the two consumers from\ndrifting.\n- **`ExtensionIndexHelpAdapter`** — reflects on the processor type at\nhelp-render time and populates `RuntimeHelpParameter.GroupName`. Small\nreflection cost; help is not a hot path.\n- **`RuntimeHelpRenderer`** — new `AppendParameterSections`: splits\nparams into buckets keyed by `GroupName`, renders default `Arguments`\nfirst, named groups alphabetical, `Global Arguments` last. Empty\nsections skipped. Insertion order preserved within each bucket.\n\n## Compute annotations\n\nMapping cross-referenced against real `az vm create --help` (azure-cli\n`dev`):\n\n- `[ParameterGroup(\"Authentication Arguments\")]` on `AdminUsername`,\n`AdminPassword`.\n- `[ParameterGroup(\"Network Arguments\")]` on `NetworkInterfaceName`,\n`VirtualNetworkName`, `NetworkSecurityGroupName`, `SubnetNames`,\n`SubnetAddressPrefixes`, `AddressPrefixes`, and all 9 `Rule*` fields.\n- Ungrouped (default `Arguments`): `SubscriptionId`, `Name`, `Location`,\n`ResourceGroupName`, `VmSize`.\n\n## Open decisions worth flagging\n\n- **`SubscriptionId` left in default `Arguments`.** Azure CLI treats\n`--subscription` as a *global* argument; promoting `SubscriptionId` to\nour `KnownGlobalFlags` would match that convention but touches broader\nprofile plumbing. Deferred as a follow-up.\n- **`Rule*` fields under `Network Arguments`.** Azure CLI exposes only\n`--nsg-rule` (a preset). Our orchestrator exposes each rule field\nindividually; grouping under `Network Arguments` matches \"network\nconfiguration\". Alternative would be a dedicated `NSG Rule Arguments` —\nrejected as over-taxonomy for a handful of fields.\n- **Grouping is opt-in, not enforced.** Matches azure-cli's own\nconvention (no linter requires every param to be grouped). Small\ncommands rightfully use only the default bucket.\n\n## Not in this PR (deferred)\n\n- Annotating processors other than `NewVMProcessor_Orchestrator`. Extend\non evidence in follow-ups.\n- `PopulatorCommand` / `ValueSources` in runtime output — runtime\ndoesn't display value-source hints today; separate feature.\n- Column alignment / padding polish — renderer has none today either.\n\n## Validation\n\n- `dotnet build azclips.slnx` — 0 errors, 0 warnings.\n- `Cli.Tests` Help suite: 78 → 82 (4 new renderer tests: grouped\nheaders, alphabetical ordering, legacy-layout regression guard, and\nomitted default `Arguments` when everything is grouped).\n- `Azclips.Help.Tool.Tests`: 190/190 (`vm.json` golden regenerated via\n`pwsh build/Update-CliExportFixtures.ps1`; diff is 17 `group_name`\nadditions, zero unrelated churn).\n- Live `az vm create --help`: 3 named sections + `Global Arguments`,\ninsertion order preserved.\n- Live `az login --help`: byte-identical to `main`.\n\n## History Notes\n\n[Help] `az <verb> --help` now supports grouping parameters into named\nsections via a `[ParameterGroup]` attribute on processor properties.\nFirst user-visible adoption: `az vm create` splits its parameters into\n`Authentication Arguments` and `Network Arguments` sections.\n\n## References\n\n- 588 master plan: `scratch/CLIPS-588-TEMP-PR-PLAN.md` (follow-up entry)\n- PR plan: `scratch/CLIPS-588-runtime-help-grouping-PR-PLAN.md`\n- Related: #691 (588-A exporter), #706 (588-D1 Markdig preflight)",
          "timestamp": "2026-08-18T16:17:19+10:00",
          "tree_id": "d95a31f955076f60526fd2f0c684af127d6ddcf1",
          "url": "https://github.com/Azure/azclips/commit/a0947abac40ca61ef46b33f2f9c309c09219e2fa"
        },
        "date": 1787034708636,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 267210270.42,
            "unit": "ns",
            "range": "± 4752297.981939618"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 255203823.85,
            "unit": "ns",
            "range": "± 4861012.666131851"
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
          "id": "58777b9ee26b7566efb56cf387cdfebc4c4bad42",
          "message": "[Help] Show 'Values from:' hint under --help parameters (#714)\n\n## Summary\n\nSecond runtime-side follow-up to 588-A (#691). Pairs with #711's\nparameter grouping — same reflection pattern, twin attribute.\n\n`RuntimeHelpRenderer` now emits `Values from: <cmd>.` under any\nparameter annotated with `[PopulatorCommand]`, matching Azure CLI's\ndiscoverability hint format. First user-visible adoption: `az vm create`\n— `--resource-group` now points users at `az group list`.\n\n## User-visible impact\n\n`az vm create --help` after this PR:\n\n    Arguments\n        --subscription-id [Required] : Azure Subscription Id.\n        --name [Required]            : The name of the virtual machine.\n        --location [Required]        : Location for all resources.\n        --resource-group [Required]  : The resource group name.\n            Values from: az group list.\n        --vm-size [Required]         : The size of the virtual machine.\n        ...\n\nOnly `--resource-group` picks up the new line; every other parameter is\nunchanged. Un-annotated commands (`az login`, `az config get`, etc.)\nrender byte-identical to `main`.\n\n## History Notes\n\n[Help] `az <verb> --help` now shows a `Values from: <command>` hint\nunder parameters annotated with `[PopulatorCommand]`, matching Azure\nCLI's discoverability convention. First user-visible adoption: `az vm\ncreate --resource-group` points users at `az group list`.\n\n## References\n\n- PR plan: `scratch/CLIPS-588-populator-command-PR-PLAN.md`\n- Preceding runtime rendering PR: #711 (parameter grouping) — same\nreflection pattern\n- Related: #691 (588-A exporter, where `[PopulatorCommand]` was\nintroduced), #706 (588-D1 Markdig preflight)",
          "timestamp": "2026-08-19T12:47:04+10:00",
          "tree_id": "627f0368902b30e6efa0f2e3ba28d22a8213656f",
          "url": "https://github.com/Azure/azclips/commit/58777b9ee26b7566efb56cf387cdfebc4c4bad42"
        },
        "date": 1787108510298,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 272925032.7,
            "unit": "ns",
            "range": "± 5243795.981963735"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 258582775.63,
            "unit": "ns",
            "range": "± 5289691.340806126"
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
          "id": "528350afeccb2994d715537428ff5192faed1296",
          "message": "{Benchmark} Benchmark to use real ProfileService I/O (#689)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- In terms of performance it is only this solution's responsibility to\nmonitor and improve client overhead performance and not downstream\nservices\n- Use production `ProfileService` during benchmarking to ensure accurate\nmeasurement of user experience of client sections.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-20T15:11:09+10:00",
          "tree_id": "5b17c7a9867b21719f9839adcbf8886315cee687",
          "url": "https://github.com/Azure/azclips/commit/528350afeccb2994d715537428ff5192faed1296"
        },
        "date": 1787203547170,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271855813.67,
            "unit": "ns",
            "range": "± 4790046.825214346"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 259816074.36,
            "unit": "ns",
            "range": "± 5100040.100654879"
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
          "id": "ee65ffe75e05ed5cfa4b71ef0052585185540512",
          "message": "{Telemetry} Refactor file telemetry sender (#722)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Refactor detached process telemetry exporter workflow\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n- `Debug` export telemetry to Jaeger\n- `Release` export telemetry App Insights\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-24T10:08:24+10:00",
          "tree_id": "58bd78428337ff2853b7e6f44c4a8e4aeead5cf9",
          "url": "https://github.com/Azure/azclips/commit/ee65ffe75e05ed5cfa4b71ef0052585185540512"
        },
        "date": 1787530986187,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 269139036.94,
            "unit": "ns",
            "range": "± 6454219.462001626"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 256701561.14,
            "unit": "ns",
            "range": "± 5737819.869332236"
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
          "id": "743b7adbeccf60f920b03069576b13951404c953",
          "message": "{Benchmark} Mock out network calls for MsalService (#693)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Microbenchmarks to use real msalservice during benchmarking.\n- During benchmark real file i/o will be measured for msalservice, but\nany potential network calls for fetching/refreshing auth tokens will be\nshort circuited.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-25T08:57:42+10:00",
          "tree_id": "c0f217fecc7e703a30235d61d6b3ac3688dda545",
          "url": "https://github.com/Azure/azclips/commit/743b7adbeccf60f920b03069576b13951404c953"
        },
        "date": 1787613720426,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 275616382.31,
            "unit": "ns",
            "range": "± 6324902.529839122"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 263957903.63,
            "unit": "ns",
            "range": "± 6353247.795832883"
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
          "id": "d035a5168dd3d9467fd089917ce3ea6ff2f0984a",
          "message": "{Telemetry} Fix piped STDOUT telemetry stall (#753)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n`az account show profile --active | cat -`\n`az profile show --active | jq -r .userPrincipalName`\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Fix telemetry emission child processes maintaining references to\nparent pipe buffers for both unix and windows causing delayed pipe\nexecution\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n`time az account show profile --active | cat -`\n`time az profile show --active | jq -r .userPrincipalName`\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-26T01:35:21+10:00",
          "tree_id": "6c9245b4524536e49aa74a46798c04b2805e24bf",
          "url": "https://github.com/Azure/azclips/commit/d035a5168dd3d9467fd089917ce3ea6ff2f0984a"
        },
        "date": 1787673050602,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 275419420.12,
            "unit": "ns",
            "range": "± 5424534.658431508"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 269383436.3,
            "unit": "ns",
            "range": "± 11519442.967139602"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "55773468+notyashhh@users.noreply.github.com",
            "name": "Yash",
            "username": "notyashhh"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e8e0610acc531f93eb6e963667f52b0523f383bf",
          "message": "[MsGraph] Module (#423)\n\nhttps://github.com/Azure/CLIPS/issues/356\n\n---------\n\nCo-authored-by: Maddison Das <maddisondas@microsoft.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>\nCo-authored-by: Maddison Das <272712104+MaddyMicrosoft@users.noreply.github.com>\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-27T15:30:54+10:00",
          "tree_id": "926f31614850231274f86ba2ff5817428a2a7220",
          "url": "https://github.com/Azure/azclips/commit/e8e0610acc531f93eb6e963667f52b0523f383bf"
        },
        "date": 1787810122815,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 274693820.92,
            "unit": "ns",
            "range": "± 4981822.380284131"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 262200666.49,
            "unit": "ns",
            "range": "± 4970874.0654802965"
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
          "id": "2787390220ad1df54f4aeef45b295aae16f82fc7",
          "message": "{Misc} update min version of dotnet sdk (#767)\n\nUpdated Min version of .Net SDK for CVE mitigations",
          "timestamp": "2026-08-28T14:09:04+08:00",
          "tree_id": "640ed6d730606938671721f36bed21e1dcd842ea",
          "url": "https://github.com/Azure/azclips/commit/2787390220ad1df54f4aeef45b295aae16f82fc7"
        },
        "date": 1787898234313,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 270733584.21,
            "unit": "ns",
            "range": "± 5745764.82763424"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 257900529.42,
            "unit": "ns",
            "range": "± 5291502.9220099505"
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
          "id": "c95435039be30271dee86a17416282b72ee92f61",
          "message": "{Benchmark} Emit telemetry for CLI benchmarks (#744)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Have CLI benchmarks emit a sampled set of telemetry traces to be used\nin further aggregation in benchmark pipelines to show potential\nregressions.\n- Sampled telemetry emitted as `benchmarkMethod.jsonl` in\n`artifacts/Benchmarks/telmetry`\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-31T11:54:37+10:00",
          "tree_id": "4fc89f2c8363fcdb93cd2de48254ce0eef60cf3c",
          "url": "https://github.com/Azure/azclips/commit/c95435039be30271dee86a17416282b72ee92f61"
        },
        "date": 1788142435299,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 271461804.58,
            "unit": "ns",
            "range": "± 5498214.069746168"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 263873784.48,
            "unit": "ns",
            "range": "± 8230502.009544765"
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
          "id": "347d9c301e6a90fbbe197d858513d966f6248a5f",
          "message": "[Login] Rename reserved bare-login profile from temp to default (#776)\n\n**Related command**\n\n`az login` / `Connect-AzAccount` (bare sign-in without `--out-profile` /\n`-OutProfile`). Also surfaced by `az profile list` / `az profile show` /\n`Get-AzProfile`.\n\n**Description**\n\nRenames the reserved profile that bare login creates from `temp` to\n`default`, per private-preview feedback (#490) that \"temp\" was confusing\nabout the profile's purpose and lifecycle (maintainer suggestion:\n\"Default makes more sense\").\n\n- `az login` / `Connect-AzAccount` without `--out-profile` now saves and\nactivates a reserved profile named `default` (previously `temp`).\n- `default` is reserved: it is rejected as an `--out-profile` /\n`-OutProfile` value, exactly as `temp` was.\n- Centralized rename: `ProfileNameValidator.ReservedTempName` →\n`ReservedDefaultName` (`\"temp\"` → `\"default\"`); the reserved-name error\nmessage derives from the constant.\n- No migration: any pre-existing `temp` profile on disk remains as an\nordinary, no-longer-reserved profile. This is acceptable for private\npreview.\n- Docs, wiki, and the openspec change are updated; the openspec\n`design.md` Decision 3 keeps its original \"why temp\" rationale with a\ndated `#490` reversal note for audit trail.\n\nNote: `default` reintroduces the \"is this a specific profile or the\nactive/default one?\" mental-model tension the original design cited.\nVerified there is no code collision — `Engine.ResolveProfileAsync`\ntracks the active profile via the separate `active-profile` pointer file\nand never treats `\"default\"` as a sentinel. The naming is the\nmaintainer's call.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx`\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~Profile\"`\n- `dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj`\n- Manual: `az login --subscription-id <sub>` (no `--out-profile`) → `az\nprofile list` shows an active profile named `default`; `az login\n--out-profile default` fails as reserved.\n\n**History Notes**\n\n[Login] `az login` / `Connect-AzAccount`: Bare sign-in now saves the\nreserved profile as `default` instead of `temp` (#490)\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\nCo-authored-by: Maddison Das <272712104+MaddyMicrosoft@users.noreply.github.com>",
          "timestamp": "2026-08-31T15:19:20+10:00",
          "tree_id": "95f1c33359c8bc47f1dc40a9f3a92e5f8f8e9414",
          "url": "https://github.com/Azure/azclips/commit/347d9c301e6a90fbbe197d858513d966f6248a5f"
        },
        "date": 1788154450390,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 277394904.44,
            "unit": "ns",
            "range": "± 5178468.09897728"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 265722037.45,
            "unit": "ns",
            "range": "± 5143344.080962698"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "299377795+azure-client-tools-agent[bot]@users.noreply.github.com",
            "name": "azure-client-tools-agent[bot]",
            "username": "azure-client-tools-agent[bot]"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6b96536b5f2213b1e6e7c5c4a9be4d95e5c444e0",
          "message": "{DevOps} Trigger stable release 0.1.2 (#781)\n\n**Description**\n\nPrepare stable release 0.1.2.\n\n- Updates src/CLI/Core.Cli.csproj\n- Updates the root CHANGELOG.md\n- Updates src/PS/Az/Az.psd1\n- Updates PowerShell module changelogs and manifests\n\n**After merge**\n\nThe release trigger validates the version-only change on main, tags the\nmerged commit as v0.1.2.<UTC yyyyMMddTHHmmssZ>, and starts the stable\nbuild, signing, and publishing flow.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-09-01T08:53:12+09:30",
          "tree_id": "34bd9a46b4869d828e47a751f7d92860f53f4ab9",
          "url": "https://github.com/Azure/azclips/commit/6b96536b5f2213b1e6e7c5c4a9be4d95e5c444e0"
        },
        "date": 1788219551960,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 284778964.34,
            "unit": "ns",
            "range": "± 74819245.15822418"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 264155367.82,
            "unit": "ns",
            "range": "± 6559136.864479817"
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
          "id": "8acf5923ad492eb7e2771ad3f2801ca5f8e8f5a8",
          "message": "[Identity] `az identity`: Add cli support for create, list, show, update, delete (#780)\n\nImplements https://github.com/Azure/CLIPS/issues/840\n\n## Summary\n\n- Added CLI support for `az identity` - \"create\", \"list\", \"show\",\n\"update\", \"delete\"\n- Added recording sanitizer coverage for identity-related values in\nsession recordings, including tenant ID, client ID, and subscription ID.\n- Added CLI help documentation for `az identity`.\n- Added unit and scenario coverage for the new identity command surface.\n- **Note: PowerShell cmdlet support and PowerShell help documentation\nwill be added in a follow-up PR.**\n\n## Validation\n\nUnit/scenario tests added, and live E2E validation passed using:\n\n```bash\n./tests/Scripts/CLI/IdentityLifecycle.sh\n```\n\n---\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-01T09:58:34+08:00",
          "tree_id": "65ec62cdea73fc2fd40ab7d1115805d504ce8287",
          "url": "https://github.com/Azure/azclips/commit/8acf5923ad492eb7e2771ad3f2801ca5f8e8f5a8"
        },
        "date": 1788228806291,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 276633026.68,
            "unit": "ns",
            "range": "± 5119827.171609484"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 265393776.99,
            "unit": "ns",
            "range": "± 7504765.314353231"
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
          "id": "8b82573f9e7a32a90e0fbc7307d636444b1efe12",
          "message": "{Telemetry} Emit real telemetry from PowerShell benchmarks (#757)\n\nStacked on #744 — review that one first.\n\n## What\n\nPowerShell micro-benchmarks previously suppressed telemetry via\n`StubTelemetryService`, so a PS benchmark told you *how long*\n`Get-AzResourceGroup` took but not *where the time went*. This makes\nthem emit real spans through the production pipeline, split by\nBenchmarkDotNet stage and sampled into one `.jsonl` per benchmark method\n— matching what the CLI benchmarks already produce.\n\n## The constraint\n\nThe file write must not land inside the measured region. Production\nPowerShell batches its export off-thread, so benchmarks preserve that:\nthe timed region pays only a flag check and a queue enqueue, and\n`[IterationCleanup]`'s container disposal drains the queue to disk —\nuntimed.\n\n## Production changes\n\n**`ModuleInitializer`** — `ITelemetrySender` moves from the override\nphase to the prepend phase, so a host (the benchmark) can substitute\none. Production PowerShell still resolves `DirectTelemetrySender`; two\ntests lock that contract.\n\n**`FileExporter`** — fixes a latent bug found while validating this\nwork. It enumerated the batch twice: once to name the file, once to\nwrite. A circular-buffer-backed `Batch<T>` dequeues as it is enumerated,\nso the first span of the first batch was silently dropped. Now a single\npass. This affects the CLI and TUI file-buffered paths too; they escaped\nit only because `SimpleActivityExportProcessor` delivers one span per\nbatch.\n\n## Benchmark-side\n\nFour `internal sealed` types in `tests/Benchmark/Telemetry/`, none\nreachable from `src/`. `StageLatch` samples the stage at span end — the\nonly moment the answer is knowable, since the engine's flag reads false\nin both `[IterationSetup]` and `[IterationCleanup]`.\n\nShared helpers extracted along the way: `BenchmarkTelemetryArtifacts`,\nand `DirectoryUtils.TryDeleteDirectory` in `Tests.Common`, which\nreplaced four duplicate implementations.\n\n## Validation\n\n- `dotnet build azclips.slnx` — 0 errors\n- Core.Telemetry.Tests 226/226, Cmdlets.Tests 100/100\n- Live `--product ALL` run at 5 iterations: every `.jsonl` holds exactly\n5 traces, 0 module-import spans, 5 activity roots. Exactly 5 — not 0\n(stage read outside the measured region) and not 10 (stage listener\nnever fired) — is the load-bearing assertion.\n- CLI output unchanged, confirming the shared-helper extraction did not\nregress it.\n\n## Note for reviewers\n\nPS benchmark means and `[MemoryDiagnoser]` numbers step up once,\nuniformly, because every iteration now creates real spans where the stub\ndid nothing. The regression gate will likely need a baseline reset on\nthe run this lands — the same one-time discontinuity the CLI change\ndocumented.\n\nDesign doc:\n`docs/superpowers/specs/2026-08-20-ps-benchmark-telemetry-design.md`\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-01T13:30:34+10:00",
          "tree_id": "daa95980df3896d7f183ebe0e6a8c802c1dbe670",
          "url": "https://github.com/Azure/azclips/commit/8b82573f9e7a32a90e0fbc7307d636444b1efe12"
        },
        "date": 1788234609553,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 272042680.08,
            "unit": "ns",
            "range": "± 5001646.385104142"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 260397067.16,
            "unit": "ns",
            "range": "± 4994432.731614274"
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
          "id": "3ab4578748e88131e3e67f608683eea6aacc8829",
          "message": "{Benchmark} Fix start up login benchmarks (#792)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-03T10:59:36+10:00",
          "tree_id": "5c32ee353a65d695383df725a7e82fe76584d7f5",
          "url": "https://github.com/Azure/azclips/commit/3ab4578748e88131e3e67f608683eea6aacc8829"
        },
        "date": 1788398229056,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 276405318.5,
            "unit": "ns",
            "range": "± 4867130.093577679"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 264572262.85,
            "unit": "ns",
            "range": "± 6067003.785982525"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "xumi@microsoft.com",
            "name": "xuming-ms",
            "username": "xuming-ms"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "610257fb45020825ce93032690d49519dafa1ad8",
          "message": "[Login] Make managed identity authentication compatible with classic CLIPS (#806)\n\n**Related command**\n\n`az login`, `Connect-AzAccount`\n\n**Description**\nMake managed identity authentication compatible with classic Azure CLI\nand Azure PowerShell\n\n\n**PowerShell — `Connect-AzAccount`**\n\n| Parameter | Added aliases |\n| --- | --- |\n| `-Identity` | `MSI`, `ManagedIdentity`, `ManagedService` |\n| `-ClientId` | `AccountId` |\n\n**CLI — `az login`**\n\n| Parameter | Added aliases |\n| --- | --- |\n| `--identity` | `-i` |\n\n\n**Testing Guide**\n\n```bash\ndotnet build azclips.slnx\ndotnet test tests/Cli.Tests/Cli.Tests.csproj\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj\ndotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj\ndotnet test tests/Scenario.Tests/Scenario.Tests.csproj\n```\n\n989 passed / 0 failed. New tests resolve the binder through DI rather\nthan constructing core services directly. Also verified manually against\nthe built client:\n\n\n**History Notes**\n\n[Login] `Connect-AzAccount`: Add `MSI`, `ManagedIdentity` and\n`ManagedService` aliases for `-Identity`, and `AccountId` for\n`-ClientId`\n[Login] `az login`: Add the `-i` alias for `--identity`\n[Login] BREAKING CHANGE: `az login`: Remove the `--subscription` and\n`-s` aliases, use `--subscription-id`\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-04T15:45:22+10:00",
          "tree_id": "5c8caf56ab6390b16b860656e18549aab78ca447",
          "url": "https://github.com/Azure/azclips/commit/610257fb45020825ce93032690d49519dafa1ad8"
        },
        "date": 1788502010271,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 284319051.58,
            "unit": "ns",
            "range": "± 6310788.4079390615"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 272114556.63,
            "unit": "ns",
            "range": "± 6349635.707924666"
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
          "id": "f7d72b40d4694026552332cb40b37738078007f1",
          "message": "[Identity] `Az.ManagedServiceIdentity`: Add Get/New/Update/Remove-AzUserAssignedIdentity (#801)\n\n**Description**\nThis PR completes https://github.com/Azure/CLIPS/issues/840 to include\nPS Cmdlet\n-  Added Cmdlets.Identity PowerShell module with:\n   - New-AzUserAssignedIdentity\n   - Get-AzUserAssignedIdentity\n   - Update-AzUserAssignedIdentity\n   - Remove-AzUserAssignedIdentity\n- Added help documentation\n- Added test coverage\n- Refactored Extensions.Identity based on processor/orchestrator\nmigration guidance for customisations and spec alignment - got rid of\nany customisations in the processor and superfluous validations, also\nupdated to use core pagination for the list command\n\n\n**Testing Guide**\n- Unit tests added, and live E2E validation passed using:\n```\n./tests/Scripts/PS/IdentityLifecycle.ps1\n```\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-07T09:51:26+10:00",
          "tree_id": "b837313a6f05f20865704acc39353a9d5502e766",
          "url": "https://github.com/Azure/azclips/commit/f7d72b40d4694026552332cb40b37738078007f1"
        },
        "date": 1788739884500,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 266636242.09,
            "unit": "ns",
            "range": "± 4554868.124482006"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 257460022.13,
            "unit": "ns",
            "range": "± 4461251.971234808"
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
          "id": "b46e898b82ec48fc2ab31744907c5507ab2a6ea3",
          "message": "{Telemetry} Add separate trace for AI agent orchestration (#782)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\nAny command that errors out \n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- This fixes the issue outlined in\nhttps://github.com/Azure/CLIPS/issues/602\n- Dispose of core command activity before AI agent executable is run\n- Instrument AI agent orchestration for tracability.\n- The relationship between traces is (AI agent trace (child)) ->\n(Command root trace (parent)) (singular direction).\n\nAs illustrated below, when a command is going through the AI agent to\npropose potential recover steps. The trace only captures the duration of\nthe excepted command\n\n<img width=\"3838\" height=\"1024\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/dc85c74f-4bd7-41e5-ae70-7ff15aa5ec7d\"\n/>\n\nThe entire duration of the AI agent loop is captured as a separate trace\ninstead, as illustrated below.\n\n<img width=\"3815\" height=\"1783\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/373a542e-3c63-46d2-a330-fbd4398cace3\"\n/>\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n1. Ensure `agent.enabled` is enabled using `az config set --key\nagent.enabled --value true`\n2. Run any command that will lead to an exception being generated i.e\n`az profile show`\n3. Observe the trace in app insights or jaeger\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-07T12:58:49+10:00",
          "tree_id": "adf9d3021f5db5686906812239967d287960e603",
          "url": "https://github.com/Azure/azclips/commit/b46e898b82ec48fc2ab31744907c5507ab2a6ea3"
        },
        "date": 1788753066093,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 280106181.47,
            "unit": "ns",
            "range": "± 5096343.911853693"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 265781670.52,
            "unit": "ns",
            "range": "± 4821506.329740233"
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
          "id": "b52c031228e57579b95524e46cbaac0c91f083d7",
          "message": "[Auth] Rename Az.Login module and Login extension to Az.Accounts (#804)\n\n**Related command**\n\nAz.Accounts (Connect-AzAccount, Disconnect-AzAccount, Get-AzProfile,\nSet-AzProfile, Remove-AzProfile); az login / az logout / az profile\n\n**Description**\n\nRenames the authentication surface from `Login` to `Accounts` so the\npreview matches the shipping Azure PowerShell name `Az.Accounts`. Two\nlogical steps (one commit each):\n\n1. PowerShell module `Az.Login` -> `Az.Accounts`: folder `src/PS/Login`\n-> `src/PS/Accounts`, `Login.psd1` -> `Accounts.psd1`,\nassembly/namespace `Microsoft.Azure.Azclips.Cmdlets.Login` ->\n`...Cmdlets.Accounts`, and the `Az` rollup plus\n`Compute`/`Config`/`Resources`/`MsGraph` `RequiredModules` references.\n2. Extension `Login` -> `Accounts`: folder `src/Extensions/Login` ->\n`src/Extensions/Accounts`, assembly/namespace\n`Microsoft.Azure.Azclips.Extensions.Login` -> `...Extensions.Accounts`,\nand `Constants.ExtensionName` (the short name the cmdlets hand to\n`LoadExtension`).\n\nUser/maintainer impact:\n- The imported module is now `Accounts` (`Import-Module Accounts.psd1`,\n`Get-Command -Module Accounts`).\n- The bundled extension short name is now `Accounts` (`az extension add\n--name Accounts`).\n- No behavior change to cmdlets or CLI verbs: `Connect-AzAccount` and\n`az login` / `az logout` / `az profile` are unchanged. Processor class\nnames (`LoginProcessor`, etc.) are also unchanged.\n\n**Testing Guide**\n\n    dotnet build azclips.slnx -c Debug\n    dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Debug\ndotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj -c\nDebug\n    dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -c Debug\n\nManual smoke:\n\n    ./artifacts/Debug/Core.Cli/az login --help\n    ./artifacts/Debug/Core.Cli/az profile list\npwsh -NoProfile -c \"Import-Module ./src/PS/Accounts/Accounts.psd1\n-Force; (Get-Command -Module Accounts).Name\"\n\nExpected: build clean; Cli.Tests 634 passed / 8 skipped,\nCore.Extensions.Tests 82 passed, Cmdlets.Tests 100 passed; `az\nlogin`/`az profile` resolve; the module exports the five cmdlets.\n\nNote for reviewers/local envs: any previously installed pre-rename\nextension at\n`~/.azclips/extensions/Microsoft.Azure.Azclips.Extensions.Login` must be\nremoved, otherwise it collides with the bundled `Accounts` extension\n(both claim the `login` verb) and every `az` command fails with\n`Duplicate leaf '(login, )'`.\n\n**History Notes**\n\n[PowerShell] `Az.Accounts`: Rename the `Az.Login` module to\n`Az.Accounts`\n[CLI] `az extension`: Rename the bundled Login extension to Accounts\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Maddison Das <272712104+MaddyMicrosoft@users.noreply.github.com>\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-07T17:39:08+10:00",
          "tree_id": "fce52aa36cb05b9a03197ca15d97609039758f5b",
          "url": "https://github.com/Azure/azclips/commit/b52c031228e57579b95524e46cbaac0c91f083d7"
        },
        "date": 1788768167514,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 267945219.82,
            "unit": "ns",
            "range": "± 4677776.2988716895"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 257031327.6,
            "unit": "ns",
            "range": "± 4741221.30545085"
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
          "id": "4096f675af7b68e540d54dfd9fc2db0dc982627e",
          "message": "{Telemetry} Aggregate benchmark traces into ASCII span-tree reports (#762)\n\nStacked on #757.\n\nThere has been feedback that although a regression is detected it\ndifficult for a team to make informed decision on whether there is a\ntrue regression, benchmark noise or there has been an acceptable\nbusiness logic change due to a lack of context.\n\nThis addresses it by sampling and emitting the trace tree as an ASCII\ntree in the CI/CD summary to aid in performance decision making.\n\nThe entire CI/CD pipeline along with a dedicated benchmark agent pool\nwill be migrated/constructed later in `.azure-pipeliens`\n\n## What it looks like\n\n```\n Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine\n COMPUTE · CLI\n\n                          main @ 285fabd · 3 days ago · 100 traces                            │         │                                   PR @ ceb088e · 100 traces\n         AVG  %ROOT                                                                           │    Δ%   │          AVG  %ROOT\n   10.312 ms   100%  ####################  CLI.Activity.Root                                  │   -7.8% │     9.506 ms   100%  ####################  CLI.Activity.Root\n    1.529 ms    15%  ###                   ├─ AzureCLI.Startup                                │   +0.4% │     1.535 ms    16%  ###                   ├─ AzureCLI.Startup\n    0.045 ms     0%                        │  ├─ AzureCLI.Startup.Container                   │  +34.0% │     0.061 ms     1%                        │  ├─ AzureCLI.Startup.Container\n    1.141 ms    11%  ##                    │  ├─ AzureCLI.Startup.Services                    │   -1.8% │     1.120 ms    12%  ##                    │  ├─ AzureCLI.Startup.Services\n    0.315 ms     3%                        │  └─ AzureCLI.Startup.Telemetry                   │   +7.3% │     0.338 ms     4%                        │  └─ AzureCLI.Startup.Telemetry\n    0.005 ms     0%                        ├─ AzureCLI.ParseGlobalArgs                        │  +27.9% │     0.007 ms     0%                        ├─ AzureCLI.ParseGlobalArgs\n    0.053 ms     1%                        ├─ AzureCLI.HandleGlobalArgs                       │  +18.3% │     0.063 ms     1%                        ├─ AzureCLI.HandleGlobalArgs\n    0.525 ms     5%  #                     ├─ AzureCLI.ParseCommandInvocation                 │   +4.3% │     0.548 ms     6%  #                     ├─ AzureCLI.ParseCommandInvocation\n    0.244 ms     2%                        │  └─ SessionCreated                               │   -3.1% │     0.237 ms     2%                        │  └─ SessionCreated\n    7.905 ms    77%  ###############       └─ AzureCLI.ProcessCommand                         │   -9.0% │     7.192 ms    76%  ###############       └─ AzureCLI.ProcessCommand\n    7.774 ms    75%  ###############          ├─ SessionCreated                               │   -8.4% │     7.121 ms    75%  ###############          ├─ SessionCreated\n    7.264 ms    70%  ##############           ├─ ProcessorProcessAsyncStart                   │   -8.6% │     6.641 ms    70%  ##############           ├─ ProcessorProcessAsyncStart\n    0.590 ms     6%  #                        ├─ AuthBeforeTokenAcquisition                   │   -1.6% │     0.581 ms     6%  #                        ├─ AuthBeforeTokenAcquisition\n    0.521 ms     5%  #                        ├─ MsalAuthService.AcquireTokenSilentlyAsync    │   -0.9% │     0.517 ms     5%  #                        ├─ MsalAuthService.AcquireTokenSilentlyAsync\n    0.496 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[1]                │  -19.0% │     0.402 ms     4%                           ├─ AuthBeforeTokenAcquisition[1]\n    0.429 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[1] │  -19.2% │     0.347 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[1]\n    0.486 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[2]                │  -21.9% │     0.379 ms     4%                           ├─ AuthBeforeTokenAcquisition[2]\n    0.421 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[2] │  -22.0% │     0.328 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[2]\n    0.478 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[3]                │  -28.4% │     0.342 ms     4%                           ├─ AuthBeforeTokenAcquisition[3]\n    0.415 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[3] │  -29.5% │     0.293 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[3]\n    0.481 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[4]                │  -25.4% │     0.359 ms     4%                           ├─ AuthBeforeTokenAcquisition[4]\n    0.421 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[4] │  -25.3% │     0.314 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[4]\n    0.482 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[5]                │  -28.6% │     0.344 ms     4%                           ├─ AuthBeforeTokenAcquisition[5]\n    0.412 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[5] │  -29.1% │     0.292 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[5]\n    0.497 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[6]                │  -23.9% │     0.378 ms     4%                           ├─ AuthBeforeTokenAcquisition[6]\n    0.433 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[6] │  -24.0% │     0.329 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[6]\n    0.478 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[7]                │  -27.3% │     0.348 ms     4%                           ├─ AuthBeforeTokenAcquisition[7]\n    0.414 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[7] │  -27.9% │     0.299 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[7]\n    0.475 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[8]                │  -29.5% │     0.335 ms     4%                           ├─ AuthBeforeTokenAcquisition[8]\n```\n   \n\n## How it works\n\n- **Discovery** walks recursively for `telemetry/*.jsonl`, so one\ninvocation handles both the CI layout (`<EXT>/<PRODUCT>/telemetry/`) and\na local run's (`<timestamp>/telemetry/`).\n- **Aggregation** groups spans by `TraceId`, rebuilds each tree from\n`SpanId`/`ParentSpanId`, and merges across traces keyed by node path.\nSame-named siblings are indexed by start order (`SessionCreated`,\n`SessionCreated[1]`) so a path is unique within a trace — necessary\nbecause `SessionCreated`, `AuthBeforeTokenAcquisition`, and\n`ActionStart` are event-driven and can each fire more than once under\none parent.\n- **Coverage** shows `(n/N)` on any node that didn't appear in every\ntrace, so a truncated export or a conditional branch is visible rather\nthan silently averaged away.\n- **Publishing** happens in the existing `collect` job, which already\ndownloads every benchmark artifact. It writes the job summary and\nuploads the rendered comment body; `benchmark-comment.yml` gains a\n`trace-comment` job that posts it.\n\n## Fork support\n\nA fork PR's `GITHUB_TOKEN` is read-only, so `collect` can never post\nfrom there. The comment body is therefore rendered once in `collect`\n(under `contents: read`) and only *delivered* by the `workflow_run` job,\nwhich runs in the base repo with a writable token. Fork and same-repo\ncomments are byte-identical because they come from the same rendered\nartifact. No fork-controlled value reaches a shell or an eval.\n\n## Reading the output\n\n`%ROOT` is a node's duration divided by the root's — not a share of\ntotal. Sibling percentages don't sum to 100% wherever spans overlap.\nThis matters under `AzureCLI.ProcessCommand`, where `SessionCreated`,\n`ProcessorProcessAsyncStart`, `AuthBeforeTokenAcquisition`, and\n`MsalAuthService.AcquireTokenSilentlyAsync` are strictly concentric in\nwall-clock terms but recorded as siblings, because those spans are\nparked on a stack in `Engine.Subscriptions.cs` rather than scoped around\nthe work they measure. The aggregator reports the spans faithfully;\ncorrecting the shape is a separate change.\n\n## Scope\n\nNo product behaviour changes. The telemetry producer already emitted\neverything required.\n\n- `.github/scripts/aggregate-benchmark-traces.py` — new\n- `.github/workflows/benchmark.yml` — three steps in the `collect` job\n- `.github/workflows/benchmark-comment.yml` — one new job\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-08T11:41:07+10:00",
          "tree_id": "4a7af274e62b53d808a4e0ffb3646448c8739622",
          "url": "https://github.com/Azure/azclips/commit/4096f675af7b68e540d54dfd9fc2db0dc982627e"
        },
        "date": 1788834167144,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 294946222.48,
            "unit": "ns",
            "range": "± 9063284.149436757"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 285062726.81,
            "unit": "ns",
            "range": "± 7132383.046617396"
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
          "id": "3b430de9ed2bcbffaa13cca79a8a7b9b33f4c2b2",
          "message": "[Compute] `az vm start`, `Start-AzVM`: Support start vm commands (#823)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n`az vm start`\n`Start-AzVM`\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\nCLI:\n`--ids`: Dummy serial placeholder implementation 🟠 \n`--name`: ✅ \n`--resource-group`: ✅ \n`--no-wait`: blocked https://github.com/Azure/CLIPS/issues/883\n`--acquire-policy-token`: blocked\nhttps://github.com/Azure/CLIPS/issues/878\n`--change-reference`: blocked https://github.com/Azure/CLIPS/issues/878\n\nPS:\n`-Id`: ✅ \n`-Name`: ✅ \n`-ResourceGroup`: ✅ \n`-NoWait`: blocked https://github.com/Azure/CLIPS/issues/883\n`-Confirm`: ✅ \n`-WhatIf`: ✅ \n`-AsJob`: https://github.com/Azure/CLIPS/issues/876\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n`az vm start --name <vm-name> --resource-group <rg-name>`\n`Start-AzVM`\n`dotnet test tests/Scenario.Tests --filter\nFullyQualifiedName~VmLifecycleScenarioTests`\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-08T14:02:50+10:00",
          "tree_id": "90ec508d287c6ee5754150e2aa01992651a1af82",
          "url": "https://github.com/Azure/azclips/commit/3b430de9ed2bcbffaa13cca79a8a7b9b33f4c2b2"
        },
        "date": 1788842302699,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 279365069.56,
            "unit": "ns",
            "range": "± 5474006.375127799"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 266248187.97,
            "unit": "ns",
            "range": "± 4592183.229460348"
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
          "id": "8fa17f0631232c0065b381211e8e689e63a3f802",
          "message": "[Profile] `az profile show`: Support default active selection (#825)\n\nImplements feature: https://github.com/Azure/azclips/issues/630",
          "timestamp": "2026-09-08T13:30:50+08:00",
          "tree_id": "56240ea329ddb59e85ea77084c4bafca5fb7c515",
          "url": "https://github.com/Azure/azclips/commit/8fa17f0631232c0065b381211e8e689e63a3f802"
        },
        "date": 1788847030416,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Config",
            "value": 278179482.58,
            "unit": "ns",
            "range": "± 5643046.421479975"
          },
          {
            "name": "Benchmark.Startup.PsWarmStartupBenchmarks.Login",
            "value": 266637084.19,
            "unit": "ns",
            "range": "± 4909232.532625932"
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
        "date": 1784167779629,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 353776928.23,
            "unit": "ns",
            "range": "± 3868137.2308578817"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 344904759.53,
            "unit": "ns",
            "range": "± 5572552.199392485"
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
        "date": 1784168867345,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 353705544.64,
            "unit": "ns",
            "range": "± 3654547.2982251905"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 372499587.6,
            "unit": "ns",
            "range": "± 89616149.37620348"
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
        "date": 1784173271344,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 365872036.43,
            "unit": "ns",
            "range": "± 41921466.09240938"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 476968140.21,
            "unit": "ns",
            "range": "± 142407694.83437443"
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
        "date": 1784187213764,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 385164177.61,
            "unit": "ns",
            "range": "± 6603066.477406125"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 543886468.35,
            "unit": "ns",
            "range": "± 229771850.88171706"
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
        "date": 1784187727833,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 365828997.29,
            "unit": "ns",
            "range": "± 7110704.682429886"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 354575057.27,
            "unit": "ns",
            "range": "± 5372238.816625104"
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
        "date": 1784202734617,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 379902913.07,
            "unit": "ns",
            "range": "± 5102868.583866922"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 368530370.89,
            "unit": "ns",
            "range": "± 6363764.96387169"
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
        "date": 1784255753310,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 376808542.09,
            "unit": "ns",
            "range": "± 5736025.578763039"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 365978710.11,
            "unit": "ns",
            "range": "± 6242848.297678769"
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
        "date": 1784256022433,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363587642.89,
            "unit": "ns",
            "range": "± 29766035.95548145"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 355904106.92,
            "unit": "ns",
            "range": "± 35892351.2764272"
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
        "date": 1784258047034,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 531881477.84,
            "unit": "ns",
            "range": "± 196811629.25786215"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 363149701.93,
            "unit": "ns",
            "range": "± 6974911.263060794"
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
        "date": 1784259659699,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 375453443.89,
            "unit": "ns",
            "range": "± 5583111.446344359"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 368481905.93,
            "unit": "ns",
            "range": "± 5869024.556892418"
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
        "date": 1784261271675,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 376012773.3,
            "unit": "ns",
            "range": "± 6605663.179792028"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 363503537.28,
            "unit": "ns",
            "range": "± 6230273.909852843"
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
        "date": 1784345514704,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 366200753.58,
            "unit": "ns",
            "range": "± 6517793.244084075"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 354559279.65,
            "unit": "ns",
            "range": "± 6788719.608965974"
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
        "date": 1784368515622,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 360863455.71,
            "unit": "ns",
            "range": "± 5080524.08608835"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 347718014.85,
            "unit": "ns",
            "range": "± 5502594.1004988095"
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
        "date": 1784526596014,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363569002.36,
            "unit": "ns",
            "range": "± 12401792.676969672"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 356767782.85,
            "unit": "ns",
            "range": "± 25387066.79363983"
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
        "date": 1784589570064,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363501311.95,
            "unit": "ns",
            "range": "± 4825078.50050114"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 352426481.41,
            "unit": "ns",
            "range": "± 6965969.889484886"
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
        "date": 1784590770987,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 358790656.7,
            "unit": "ns",
            "range": "± 4013549.217211097"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 423388772.71,
            "unit": "ns",
            "range": "± 120865708.6764161"
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
        "date": 1784693082070,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 374043371.62,
            "unit": "ns",
            "range": "± 6904606.284632662"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 504347859.35,
            "unit": "ns",
            "range": "± 141949957.94665894"
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
        "date": 1784694252337,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 361157386.75,
            "unit": "ns",
            "range": "± 45045130.57682142"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 344127439.72,
            "unit": "ns",
            "range": "± 6722569.163538161"
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
        "date": 1784696308390,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 357000498.02,
            "unit": "ns",
            "range": "± 5355885.048168964"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 346020368.82,
            "unit": "ns",
            "range": "± 7050609.125544101"
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
        "date": 1784783692917,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 402088118.21,
            "unit": "ns",
            "range": "± 88872785.1317284"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 462033794.95,
            "unit": "ns",
            "range": "± 136875640.56366938"
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
        "date": 1784794098615,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 376555647.76,
            "unit": "ns",
            "range": "± 6839354.432119765"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 366849386.95,
            "unit": "ns",
            "range": "± 7487869.970532416"
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
        "date": 1785116473999,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 418101345.41,
            "unit": "ns",
            "range": "± 179066529.87433776"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 454556226.87,
            "unit": "ns",
            "range": "± 228329133.0600574"
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
        "date": 1785123411598,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 356158490.68,
            "unit": "ns",
            "range": "± 4408109.1349272365"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 347452667.27,
            "unit": "ns",
            "range": "± 5020596.190066674"
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
        "date": 1785125796779,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 370354977.16,
            "unit": "ns",
            "range": "± 9015641.932885783"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 490539823.46,
            "unit": "ns",
            "range": "± 140740654.3609122"
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
        "date": 1785201539275,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 379778695.79,
            "unit": "ns",
            "range": "± 5378427.998507878"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 366994416.42,
            "unit": "ns",
            "range": "± 7253774.2662206525"
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
        "date": 1785228647433,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 432785806.4,
            "unit": "ns",
            "range": "± 107767200.35461846"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 346872308.63,
            "unit": "ns",
            "range": "± 4589347.558947219"
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
        "date": 1785383888024,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 487888220.74,
            "unit": "ns",
            "range": "± 137002465.87668556"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 357670384.62,
            "unit": "ns",
            "range": "± 6876507.90826872"
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
        "date": 1785389289182,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 397150193.7,
            "unit": "ns",
            "range": "± 91882986.57549204"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 354582332.27,
            "unit": "ns",
            "range": "± 6341907.160824899"
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
        "date": 1785392909844,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 356012141.78,
            "unit": "ns",
            "range": "± 3803450.1292694113"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 510816624.56,
            "unit": "ns",
            "range": "± 219834592.24411833"
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
        "date": 1785452113280,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 370221656.49,
            "unit": "ns",
            "range": "± 5503959.346879395"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 360154121.59,
            "unit": "ns",
            "range": "± 14723862.446515195"
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
        "date": 1785731752974,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 362018312.68,
            "unit": "ns",
            "range": "± 7308683.381695581"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 350721234.92,
            "unit": "ns",
            "range": "± 5558046.071801009"
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
        "date": 1785802535613,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 483700121.51,
            "unit": "ns",
            "range": "± 133864225.25459842"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 351192623.96,
            "unit": "ns",
            "range": "± 19004375.836139463"
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
        "date": 1785804021124,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 359657875.42,
            "unit": "ns",
            "range": "± 4150402.372637208"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 418245266.65,
            "unit": "ns",
            "range": "± 105992573.75589842"
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
        "date": 1785808549596,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363979622.53,
            "unit": "ns",
            "range": "± 7506367.543036886"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 354167608.23,
            "unit": "ns",
            "range": "± 7220745.181155203"
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
        "date": 1785818223916,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 361603721.11,
            "unit": "ns",
            "range": "± 4906479.809627029"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 467579645.91,
            "unit": "ns",
            "range": "± 126165993.62137862"
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
        "date": 1785819676247,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 368183113.7,
            "unit": "ns",
            "range": "± 5887191.346549689"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 356448105.71,
            "unit": "ns",
            "range": "± 5015489.145352186"
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
        "date": 1785904920380,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 374824256.01,
            "unit": "ns",
            "range": "± 7170060.05191167"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 362616909.34,
            "unit": "ns",
            "range": "± 6293102.818737911"
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
          "id": "d3b61011b21789d561c2144647f90e6988ea5d64",
          "message": "{Help} Change public facing references to Azclips (#667)\n\nFixes https://github.com/azure/azclips/issues/647\n\n## Summary\n\nRemoves the internal codename `AzClips` / `Azclips` from user-visible\nhelp text on both the CLI and PowerShell surfaces, so `az help`, `az\nconfig --help`, `az version --help`, the welcome banner, and\n`Get-Module` describe the products as **Azure CLI** and **Azure\nPowerShell**.\n\n## Changes\n\n**CLI help docs**\n\n- `src/CLI/Help/Docs/config/index.md` -- `summary` + `Description` now\nreference \"Azure CLI\" instead of \"AzClips\". (This is the exact string\nshown in the reporter's screenshot on `az help`.)\n- `src/CLI/Help/Docs/version/index.md` -- `Description` on `az version\n--help` now says \"Azure CLI\".\n\n**Welcome banner** (`src/CLI/Help/WelcomeBanner.cs`)\n\n- ASCII figlet regenerated in the same `small` font, now spelling\n**Azure CLI**.\n- Tagline simplified to `Welcome to the Azure CLI.` (dropped the\nmulti-host copy since the banner only fires from the CLI).\n- `az --help` hint line preserved.\n- `tests/Cli.Tests/WelcomeBannerTests.cs` assertion updated from\n`AzCLIPS` -> `Azure CLI`; all three banner tests pass.\n\n**PowerShell module manifests** (`Description` field only -- the string\nsurfaced by `Get-Module` / `Find-Module`)\n\n- `src/PS/Az/Az.psd1`\n- `src/PS/Compute/Compute.psd1`\n- `src/PS/Config/Config.psd1`\n- `src/PS/Login/Login.psd1`\n- `src/PS/Resources/Resources.psd1`\n\n## Explicitly not touched\n\n- Anything under `Agent/` (out of my area).\n- Real identifiers: `Microsoft.Azure.Azclips.*` package/assembly ids,\nthe `~/.azclips/` on-disk paths, `ProjectUri =\n'https://github.com/Azure/azclips'`, and the auto-generated `# Module\nmanifest for module 'Azclips.<X>'` header comment in each `.psd1`.\n- PSGallery `Tags` on `Compute.psd1` / `Resources.psd1` that still\ninclude `'AzClips'` -- happy to strip in a follow-up if reviewers want\nthem gone (they're gallery-search facets, not help text).\n\n## Validation\n\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter WelcomeBanner`\n-- 3/3 pass.\n- `az help` output grep for `AzClips|Azclips|AzCLIPS` -- now empty.\n- `Import-Module .\\src\\PS\\Login\\Login.psd1 -Force; Get-Module Login | fl\nDescription` -- reports `Azure authentication cmdlets for Azure\nPowerShell`.",
          "timestamp": "2026-08-07T00:58:24Z",
          "tree_id": "cc90a81df4b0593c6acd2bc8feeedfc6a20e58d0",
          "url": "https://github.com/Azure/azclips/commit/d3b61011b21789d561c2144647f90e6988ea5d64"
        },
        "date": 1786065615564,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 361998102.24,
            "unit": "ns",
            "range": "± 4368375.181995329"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 351240166.87,
            "unit": "ns",
            "range": "± 4944099.275203479"
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
          "id": "5bd521582e94d0ecd9c92c41122859c897e531a3",
          "message": "[Config] Rename assemblyLoading extension-root config keys (breaking) (#668)\n\n## Summary\n\nRenames the two user-observable `assemblyLoading.*InstallationDir`\nconfig keys to a clearer bundled/installed vocabulary, and\ncorrespondingly renames the two extension-loader classes and their\n`AssemblyLoadingOptions` property counterparts.\n\nCloses Azure/CLIPS#533.\n\n## Motivation\n\nThe `Builtin*` and `MultiVersion*` names are legacy MVP terminology that\nno longer describes what the loaders do:\n\n- `BuiltinExtensionLoader` serves extensions **bundled** in the shipped\ninstall (`<install>/Extensions/`). This is orthogonal to the\ncompile-time `BuiltInExtensions.Map` \"Core\" seed, and the naming\ncollision has been a persistent source of confusion.\n- `MultiVersionExtensionLoader` serves extensions **installed** by the\nuser via `az extension add` under `~/.azclips/extensions/`.\n\"MultiVersion\" describes its filesystem layout, not its role.\n\nBridging XML docs introducing the target Core / Bundled / Installed\nvocabulary landed earlier in #550. This PR realizes the rename.\n\nThe `assemblyLoading.builtinInstallationDir` /\n`assemblyLoading.versionedInstallationDir` config keys are\nuser-observable via `az config`, so they are renamed in the same PR to\nkeep the C# property names and the user-facing keys in sync. Introducing\nan alias layer would extend the confusion; a clean break is preferred\nwhile the project is still in preview.\n\n## Changes\n\n### User-visible\n\n- Config key `assemblyLoading.builtinInstallationDir` →\n`assemblyLoading.bundledExtensionsRoot`\n- Config key `assemblyLoading.versionedInstallationDir` →\n`assemblyLoading.installedExtensionsRoot`\n- Descriptions updated in `docs/azure-cli/docs-conceptual/config.md`\n- CHANGELOG entry under **Upcoming Release → `#### CLI`**\n\nAny existing user config referencing the old keys must be renamed\nmanually. The old spellings now fail with `unknown key` via `az config\nset`.\n\n### Internal (mechanical)\n\n- Class `BuiltinExtensionLoader` → `BundledExtensionLoader`\n- Class `MultiVersionExtensionLoader` → `InstalledExtensionLoader`\n- Property `AssemblyLoadingOptions.BuiltinInstallationDir` →\n`BundledExtensionsRoot`\n- Property `AssemblyLoadingOptions.VersionedInstallationDir` →\n`InstalledExtensionsRoot`\n- Test file `BuiltinExtensionLoaderTests.cs` →\n`BundledExtensionLoaderTests.cs` (`git mv`)\n- Comment / XML-doc / log-tag string updates in\n`ExtensionLoadTracker.cs`, `CLIAssemblyLoadingProvider.cs`,\n`Program.cs`, `VersionInformationProvider.cs`, `CompletionGenerator.cs`,\n`CLIBenchmarkBase.cs`\n- In-scope wiki updates: `wiki/02-deep-dive/extension-system.md`,\n`wiki/02-deep-dive/core-engine-orchestration.md`,\n`wiki/onboarding-guide.md`, `wiki/zero-to-hero-guide.md`\n- Log-tag regex assertion in `StreamRoutingIntegrationTests.cs` updated\nto match the new class names\n\n### Explicitly out of scope (follow-up: Azure/CLIPS#622)\n\n- Deleting `BuiltInExtensions.Map` and the drift-guard test\n- Removing `IsBuiltInVerb` / `s_builtInPackageIds` and the \"Core wins\nover registry\" rule\n- Rewriting the \"Core tier\" narrative in `eng-docs/spec/extension-*.md`\n- Making `az extension update login` (and other Core-verb extensions)\nwork\n\nScratch design plan on the branch at\n`scratch/CLIPS-533-TEMP-PR-PLAN.md`.\n\n## History Notes\n\n`BREAKING CHANGE: [Config] Renamed extension-root config keys —\n'assemblyLoading.builtinInstallationDir' →\n'assemblyLoading.bundledExtensionsRoot' and\n'assemblyLoading.versionedInstallationDir' →\n'assemblyLoading.installedExtensionsRoot'. Existing user config\nreferencing the old keys must be updated manually; the old spellings now\nfail with \"unknown key\".`\n\n`[CLI] Renamed extension-loader classes 'BuiltinExtensionLoader' →\n'BundledExtensionLoader' and 'MultiVersionExtensionLoader' →\n'InstalledExtensionLoader' to match the on-disk tier they serve.`\n\n## Validation\n\n- `dotnet build azclips.slnx` — green (3 pre-existing warnings unrelated\nto this PR).\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` — 51 rename-affected\ntests pass (`BundledExtensionLoaderTests`,\n`ExtensionSelectionPathOverrideTests`,\n`ExtensionLoadContextDirectoryTests`, `StreamRoutingIntegrationTests`,\n`ConfigurationServiceTests`, `VersionInformationProviderTests`). One\npre-existing environmental failure\n(`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`) requires a\npublished `az` binary that isn't produced by a Debug build; not related\nto this change.\n- `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj`\n— 85/85 pass.\n- `git diff --stat`: 22 files, +178 / −172, all mechanical.",
          "timestamp": "2026-08-07T17:01:56+10:00",
          "tree_id": "58a3a98180678b00b24de8f2c46e6ba579e8d904",
          "url": "https://github.com/Azure/azclips/commit/5bd521582e94d0ecd9c92c41122859c897e531a3"
        },
        "date": 1786087227779,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 507925050.39,
            "unit": "ns",
            "range": "± 137444651.891166"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 372546548.71,
            "unit": "ns",
            "range": "± 10138912.245041968"
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
          "id": "1ea1a66d32bb7df79bf9db236f8fef1cbe0484f0",
          "message": "[Config] Remove bundledExtensionsRoot from config (#676)\n\n## Summary\n\nRemoves the `assemblyLoading.bundledExtensionsRoot` config key. Bundled\nextensions are now resolved from a fixed candidate root list in\n`BundledExtensionLoader.GetCandidateRoots` — no runtime knob required,\npackaging always writes to `<install>/Extensions/`.\n\nCloses Azure/CLIPS#630.\n\n## Motivation\n\nThe config value was a leftover from an early env-var absorbed into the\nconfig system with no real user-facing purpose:\n\n- Packaging is hardcoded —\n[`inject_builtin_extensions.sh`](https://github.com/Azure/azclips/blob/main/devops/scripts/packaging/common/inject_builtin_extensions.sh),\nthe staging scripts, and every tarball/zip test all write bundled\nextensions to `<install>/Extensions/`. Zero references to\n`bundledExtensionsRoot` anywhere in `devops/`, `install/`, or\n`.azure-pipelines/`.\n- Users who want extra extensions already have `az extension add --path\n<nupkg>` and the Installed tier.\n- Keeping the config surface risked exactly the divergence class\ncaptured in Azure/CLIPS#624 / #629 — dispatch honors it, other consumers\ndon't.\n\nThe RELEASE default (`<install>/Extensions`) has been folded into the\nloader's candidate list, so packaged installs continue to work with no\nconfig knob.\n\n## Changes\n\n### User-visible\n\n- Config key `assemblyLoading.bundledExtensionsRoot` removed. `az config\nset assemblyLoading.bundledExtensionsRoot <path>` now fails with\n`unknown key`.\n- Doc row in `docs/azure-cli/docs-conceptual/config.md` deleted.\n- CHANGELOG entry under **Upcoming Release → `#### CLI`**.\n\n### Internal\n\n- `AssemblyLoadingOptions.BundledExtensionsRoot` property and\n`GetDefaultBundledExtensionsRoot` helper deleted.\n- New `public static string BundledExtensionLoader.GetDefaultRoot()` —\nthe same DEBUG/RELEASE computation, now sitting on the loader (its only\nconsumer).\n- `BundledExtensionLoader.GetCandidateRoots` becomes parameterless; the\nthree loader call sites (`LoadExtension`, `LoadExtension` error log,\n`GetInstalledExtensions`) updated.\n- Schema entry deleted from `ConfigurationSchema.cs`.\n\n### Tests\n\n- `BundledExtensionLoaderTests` restructured: each test stages under\n`BundledExtensionLoader.GetDefaultRoot()` with a Guid-suffixed short\nname so the process-static cache and enumeration stay isolated.\nPer-package cleanup instead of temp-root cleanup.\n- `ExtensionSelectionPathOverrideTests`: bundled fixtures moved to\n`GetDefaultRoot()`; installed fixtures still use temp dirs (that tier's\nconfig stays). Stub `TestConfigurationService` no longer sets the\nremoved property.\n- `BenchmarkAssemblyConfigurationService` and `CLIBenchmarkBase`\nupdated: `EnsureBuiltInPayload` now copies the Release Core-extension\nsource into `GetDefaultRoot()` so the benchmark's loader can discover it\nvia the fallback list.\n- `ConfigurationServiceTests`: assertion for the removed key deleted.\n\n## History Notes\n\n`BREAKING CHANGE: [Config] Removed\n'assemblyLoading.bundledExtensionsRoot'. Bundled extensions are now\ndiscovered from a fixed candidate root list ('<install>/Extensions' in\nRelease, plus the local Debug/Release build outputs). Any user config\nreferencing this key will fail with \"unknown key\".`\n\n## Validation\n\n- `dotnet build azclips.slnx` — green (3 unrelated pre-existing\n`GHCP001` warnings in `Agent/ChatAgent.cs`).\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` — 586 passing, 1\npre-existing environmental failure\n(`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`, needs a published\n`az` binary).\n- `dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj`\n— 85/85.\n- Local `az config list --all` confirms the key no longer appears.\n- `git diff --stat`: 10 files, +240 / −238.",
          "timestamp": "2026-08-10T14:24:42+10:00",
          "tree_id": "5f9859cbf02dc860f87862203f698e01b79ba57a",
          "url": "https://github.com/Azure/azclips/commit/1ea1a66d32bb7df79bf9db236f8fef1cbe0484f0"
        },
        "date": 1786336917736,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 358487407.31,
            "unit": "ns",
            "range": "± 4602340.491405584"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 349244840.05,
            "unit": "ns",
            "range": "± 6393822.396668207"
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
          "id": "031ec291d73dc41c9686f9596a801af57157d493",
          "message": "{Extensions} Honor assemblyLoading.installedExtensionsRoot in registry + install paths (#678)\n\n## Summary\n\nEvery consumer of the extension registry (the 6 `az extension *`\ncommands, tab-completion, runtime help, dispatch, the loader provider)\nnow goes through DI for `IExtensionRegistryStore`. The DI factory\nresolves the path from `assemblyLoading.installedExtensionsRoot` in\nexactly one place. `ExtensionInstaller` derives its install root the\nsame way. Paths no longer appear in any consumer code — they're a\nplumbing detail.\n\nCloses Azure/CLIPS#629.\n\n## The bug\n\n`assemblyLoading.installedExtensionsRoot` was only half-honored: the\nloader read the config, but every registry consumer\n(`FileExtensionRegistryStore`'s parameterless ctor, all six `az\nextension *` commands, `CompletionGenerator`, `RuntimeHelpDispatcher`,\n`ExtensionDispatcher`, `CLIAssemblyLoadingProvider`,\n`ExtensionInstaller.InstallPath`) reached for the hardcoded\n`~/.azclips/extensions/` default.\n\nRepro (broken before, fixed after):\n\n1. `az config set assemblyLoading.installedExtensionsRoot\n/custom/installed`\n2. `az extension add --path <pkg>.nupkg` — DLLs land under\n`/custom/installed/` (loader honored it)\n3. `az extension list` — **empty** (reads\n`~/.azclips/extensions/registry.json`)\n4. `az extension remove <pkg>` — **not found**\n5. `az <TAB>` completion — misses the extension's verbs\n\n## Design\n\nThe right shape wasn't \"sprinkle a path resolver everywhere.\" Every `new\nFileExtensionRegistryStore()` in `src/` was the actual bug — 9 sites\nbypassing DI. The fix is disciplined encapsulation:\n\n- **Delete** the parameterless `FileExtensionRegistryStore()` ctor + its\n`HomePaths.GetDefaultRegistryPath()` helper. Ctor now requires an\nexplicit path.\n- **Single seat of truth**: DI factory in\n`CliExtensionServiceRegistration` resolves\n`IConfigurationService.GetConfiguration<AssemblyLoadingOptions>().InstalledExtensionsRoot`\nand joins `HomePaths.RegistryFileName`. This is the **only** place path\nresolution happens for the registry.\n- **Consumers get `IExtensionRegistryStore` via DI**; no consumer ever\nmentions a path.\n- **`ExtensionInstaller`**: `installPath` parameter removed. Ctor\nresolves the root internally from the same config key.\n- **`HomePaths.GetDefault*` stays** — it remains the fallback the\noptions bind to when the config key is unset, so default behavior is\nunchanged.\n\n## Changes\n\n### Product code (`src/`)\n- `Core.Extensions/FileExtensionRegistryStore.cs` — required-path ctor;\ndeleted `DefaultPath()` + `HomePaths` import\n- `Core.Extensions/EmptyExtensionRegistryStore.cs` — **new**, tiny\nread-only null-object impl used by the dispatcher's test-only ctor\n- `CLI/CliExtensionServiceRegistration.cs` — DI factory resolves the\npath from config\n- `CLI/AssemblyLoading/CLIAssemblyLoadingProvider.cs` — `registryStore`\nctor param is now required\n- `CLI/Parser/ExtensionDispatcher.cs` — deleted the private\n`FileExtensionRegistryStore`-based ctor; parameterless ctor uses\n`EmptyExtensionRegistryStore`\n- `CLI/Help/RuntimeHelpDispatcher.cs` — `IExtensionRegistryStore`\nthreaded through both ctors\n- `CLI/Program.cs` — `DispatchHelp` resolves the store from DI and\npasses it\n- `CLI/Completions/CompletionGenerator.cs` — 3 sites resolve via\n`AzclipsServiceProvider`\n- `Extensions/Extension/Add.cs, List.cs, Pin.cs, Remove.cs, Show.cs,\nUpdate.cs` — 6 processors resolve via `AzclipsServiceProvider`\n- `Extensions/Extension/ExtensionInstaller.cs` — `installPath` param\nremoved; resolves internally from config\n\n### Tests\n- `Core.Extensions.Tests/FileExtensionRegistryStoreTests.cs` — removed\n`DefaultPath_ShouldPointToUserProfile` (the concept no longer exists)\n\n## Testing\n\n- Full solution build: green, 0 warnings, 0 errors\n- `Core.Extensions.Tests`: **84/84**\n- `Cli.Tests`: **586/586** (excluding the pre-existing\n`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail` env flake unrelated\nto this change)\n\nIntegration test for the `az extension add → list → remove`\nconfig-override round-trip is a good follow-up but isn't gated on this\nPR — the DI factory is the one place a regression could re-introduce the\nbug, and it's covered by construction.\n\n## Non-goals\n\n- Not renaming the config key (already done in azclips#668).\n- Not removing `HomePaths.GetDefaultExtensionsRoot()` /\n`GetDefaultRegistryPath()` — they remain the default the options bind\nto.\n- Not touching Bundled-tier discovery (azclips#669 covered the\ntab-completion side; a broader Bundled audit belongs under a follow-up\nif #624 turns up more).\n\n## References\n\n- Investigation umbrella: Azure/CLIPS#624\n- Bundled companion fix: azclips#669 (merged)\n- Config-key rename that surfaced this: Azure/CLIPS#533 / azclips#668\n- Immediately-preceding removal of `bundledExtensionsRoot`:\nAzure/CLIPS#630 / azclips#676\n- Sub-issue of Extension framework epic: Azure/CLIPS#209",
          "timestamp": "2026-08-11T15:06:43+10:00",
          "tree_id": "c83a74cfd08fbdded87eb3d5714107acbe5a7c43",
          "url": "https://github.com/Azure/azclips/commit/031ec291d73dc41c9686f9596a801af57157d493"
        },
        "date": 1786425792550,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 394842020.93,
            "unit": "ns",
            "range": "± 6800342.691356915"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 387308149.49,
            "unit": "ns",
            "range": "± 31500318.898497496"
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
          "id": "bc608d1a5d7d888b61154d85dc5cf472dedacd34",
          "message": "{Extensions} Delete BuiltInExtensions.Map and treat all extensions as bundled or installed (#683)\n\n## Summary\n\nDeletes the compile-time \"Core\" tier from the runtime. Login, Config,\nExtension, and Agent become ordinary extensions — same discovery, same\ndispatch, same install/remove/update semantics as Compute, Resources, or\nany third-party extension.\n\nCloses Azure/CLIPS#622.\n\n## What actually changes for users\n\n- **`az extension add`, `remove`, `update` work for former Core verbs.**\nTrying to `az extension update login` used to fail with a `VerbConflict`\nrefusal (\"`login` is a built-in verb…\"); it now upgrades the shipped\nLogin extension in place. Same for `remove` and `add` when a package\nlegitimately declares one of these verbs.\n- **Tab completion sees installed-tier extensions.** `az\n--completion-candidates` and `az --completions` now enumerate through\nthe same loader chain the runtime uses, so an extension installed via\n`az extension add` under a config-overridden root surfaces in\ncompletions at every depth. Previously the completion generator only\nscanned bundled roots — the exact gap I flagged in the [#622\ncomment](https://github.com/Azure/CLIPS/issues/622#issuecomment-5247698834)\nafter CLIPS-629's runbook.\n- **`(verb, path)` collision protection stays.** The classic-Azure-CLI\nshadowing-gap guards (`LeafConflict` at install time, duplicate-leaf\nhard-fail at index build) are untouched. #622 removes only the\nCore-specific refusals; general conflict detection between independent\nextensions still fires.\n\n## Design in one line\n\nRuntime knows two tiers (Bundled + Installed). Every leaf comes from\ndiscovery over both loaders. No static seed, no \"Core wins\" precedence,\nno verb-refusal in `az extension *`. Dispatch is uniform.\n\n## Implications\n\n- **Bootstrap paradox left as-is.** `az extension remove extension`\n(removing the extension-management extension itself) is a self-inflicted\ncorruption with no clean in-band recovery. Same class of problem as `apt\nremove dpkg`. Treated as manual CLI reinstall, per the issue's explicit\nnon-goal.\n- **Packaging profile is now the sole source of truth for \"what ships in\nevery install.\"**\n[`devops/scripts/packaging/config/{lean,full}_package.config`](devops/scripts/packaging/config/)\ndrives the injector; the runtime no longer duplicates that list.\n- **Add.cs's pre-existing gap (doesn't persist `Contributions` to\n`registry.json`) becomes moot for completion.** Phase 4's rewrite drops\nthe dependency; `Contributions` is now a runtime-only projection built\nfrom discovery. Registry stays a preferences store.\n\n## Testing\n\n- Build clean across the solution.\n- `Cli.Tests` 580/587 · `Core.Extensions.Tests` 78/78 · `Scenario.Tests`\n42/42 (7 test facts removed as part of the deletion; the pre-existing\n`ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail` env flake is the\nonly fail).\n- Smoke-tested on the compiled binary end-to-end:\n  - `az config list`, `az group --help`, all Core verbs route unchanged.\n- Installed Resources (staged under `~/.azclips/extensions/` after\ndeleting the bundled copy) is discovered, dispatched, help-rendered, and\ncompleted correctly.\n- `az --completion-candidates group` → `create list show`; `az\n--completion-candidates deployment group` → `create what-if` — the\nCLIPS-629 runbook's B8–B12 checks that had been deferred to this PR now\npass.\n- `az extension remove --name login` returns `ExtensionNotFound`\n(ordinary not-installed error) instead of the old `VerbConflict`\nrefusal.\n\n## Non-goals\n\n- Not renaming loader classes (already done in azclips#668).\n- Not changing packaging or the packaging profile files.\n- Not adding compat-range enforcement (`AzclipsCompatibility` +\n`CompatibilityGuard` already exist).\n- Not solving the bootstrap paradox.\n\n## References\n\n- Parent epic: Azure/CLIPS#209 (Extension framework support)\n- Config-honor precursor (merged): Azure/CLIPS#629 / azclips#678\n- Rename precursor (merged): Azure/CLIPS#533 / azclips#668\n- Follow-up scoped into this PR via [#622\ncomment](https://github.com/Azure/CLIPS/issues/622#issuecomment-5247698834):\ntab-completion enumeration",
          "timestamp": "2026-08-12T15:51:13+10:00",
          "tree_id": "69e9f3d514e3d362b3fc6e86480a4d96d6ffc9fb",
          "url": "https://github.com/Azure/azclips/commit/bc608d1a5d7d888b61154d85dc5cf472dedacd34"
        },
        "date": 1786514797671,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 424618373.29,
            "unit": "ns",
            "range": "± 104433717.63261768"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 434113663.02,
            "unit": "ns",
            "range": "± 129855619.27958107"
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
          "id": "675d1caed1f399cf57aa89ce0bb646e65318de06",
          "message": "[Extensions] `extension list` displays all available candidates (#688)\n\nCloses https://github.com/Azure/CLIPS/issues/643\nCloses https://github.com/Azure/CLIPS/issues/534\n\n`az extension list` will now show all available extension candidates\n(not just the extension that will dispatch). Note here, `0.1.1` and\n`0.1.2` are listed, along with their location (one bundledIn install\nlocation, one externally installed).\n\n`0.1.1` is pinned, so it is listed as `selectedVersion`.\n\n\n<img width=\"1882\" height=\"712\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/ff1b9e4a-8e55-4b18-b3d2-06f2b94e23fc\"\n/>\n\nUnpinning will show `0.1.2` as selected version now (it wins selection\nrules: higher semver), but `0.1.1` is still listed as a candidate:\n<img width=\"1898\" height=\"710\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/485ed055-fa92-4943-9b79-f75a37e1a91c\"\n/>\n\n---\n\n## Implementation notes\n\nBoth issues live under Azure/CLIPS#209.\n\n**CLIPS-534** - flip `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>`\non the five dispatchable extension csprojs (Compute, Config, Extension,\nLogin, Resources) so the SDK stamps `AssemblyInformationalVersion` on\neach DLL; extend `BundledExtensionLoader.ResolvePackageVersion` with a\n`MetadataLoadContext` fallback that reads the attribute (stripping the\n`+sha` suffix) when the `.nupkg` is stripped by packaging. Local builds\nstill hit the `.nupkg` scan first, so behavior is unchanged there.\n\n**CLIPS-643** - new\n`DiscoveryBackedExtensionRegistry.BuildDiscoveredWithCandidates` returns\nboth the collapsed `ExtensionRegistry` and per-package\n`SelectedExtensionCandidate` (Selected + Ignored).\n`ListExtensionsProcessor` projects that into a `candidates: []` array\nand a `selectedVersion` field. Tier is inferred by path-matching\n`Location` against `assemblyLoading.installedExtensionsRoot`.\n\n**Shape is additive** - every existing top-level field on `az extension\nlist` output survives; `version` still equals `selectedVersion` for\nbackward compat.\n\n**Follow-up captured on Azure/CLIPS#489**: `az extension pin --version\n<v>` becomes the natural companion once side-by-side install lands.\n\n## Testing\n\n- 4 unit tests for `AssemblyVersionReader` (real assembly, missing dir,\nmissing DLL, unreadable DLL).\n- 2 loader integration tests (fallback-hit +\nfallback-miss-with-empty-DLL).\n- 6 unit tests for `BuildCandidates` / `InferTier` (single-tier,\ndual-tier no pin, dual-tier with older-Bundled pin, null-selection\nfallback, `InferTier` branches).\n- Existing `ExtensionListByVerbTests` updated for the new record shape.\n- Live-verified scenarios shown in the screenshots above (pin, unpin,\n`--by-verb`).\n\n593/601 in `Cli.Tests` pass; 1 pre-existing `ManagedIdentityE2ETests`\nfailure is environment-dependent and unrelated.",
          "timestamp": "2026-08-13T15:32:36+10:00",
          "tree_id": "537caa43e380266c9b4805c12270df482c1da2ce",
          "url": "https://github.com/Azure/azclips/commit/675d1caed1f399cf57aa89ce0bb646e65318de06"
        },
        "date": 1786600037053,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 478838840.66,
            "unit": "ns",
            "range": "± 137529150.50069407"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 359287942.81,
            "unit": "ns",
            "range": "± 5678112.479449804"
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
          "id": "28dbe75d719d30a8e5ee4d8340c232e3d1ce78a7",
          "message": "{Telemetry} Trace startup time for CLI and PS module import (#685)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n- Add startup performance context to track start up time before\nTelemetryService initialisation\n- Replay startup performance context as part of trace hierarchy for CLI\ncommands\n- Instrument more sections of Program.cs entry point for better\ntraceability.\n- Measure PS import time as separate trace activity on cmdlet import\n\n<img width=\"2463\" height=\"974\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/afebf67b-2d80-456c-b5e8-feee6b15b273\"\n/>\n\n<img width=\"2453\" height=\"1151\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/c689ab7a-31b8-4c75-b5d5-3e0274dd0bdf\"\n/>\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-17T10:15:24+10:00",
          "tree_id": "02d17f3b1985d236ed123ce1e63f5819d62aa82f",
          "url": "https://github.com/Azure/azclips/commit/28dbe75d719d30a8e5ee4d8340c232e3d1ce78a7"
        },
        "date": 1786927208404,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 410946898.07,
            "unit": "ns",
            "range": "± 104433595.23599489"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 397485858.44,
            "unit": "ns",
            "range": "± 99275554.51574436"
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
          "id": "0d9127fea4d40e12cdf08ae2de77a6d985bfbc45",
          "message": "{Tests} Add the vm create scenario with a live recording (#695)\n\nFirst orchestration scenario for the record/playback framework (CLIPS\ntask [#564](https://github.com/Azure/CLIPS/issues/564), scenario side;\nthe benchmark handoff follows separately).\n\n`az vm create` / `New-AzVM` is recorded once as a whole: the resource\ngroup, vnet and nsg in parallel, the nic built from their returned ids,\nand the vm as a real LRO with its polling. The committed recording is\nreal ARM traffic from a live run, 15 entries, replaying credential-free\non both surfaces in about two seconds.\n\nSupporting changes the live run drove: playback serves `Retry-After: 0`\nso recorded poll delays cost nothing; the sanitisers scrub passwords and\ndrop ARM's `x-ms-operation-identifier` header (tenant and object ids),\nwith a matching secret-scan rule; the harness supports multi-extension\nscenarios since vm create needs its resource group first.\n\nConcurrency is proven directly: the parallel vnet/nsg entries replay\nwith their recorded order deliberately swapped. Re-recording steps are\nin the README.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-17T11:43:19+10:00",
          "tree_id": "4e294cb510d24067a760b0dfb30d8c4abbed3f78",
          "url": "https://github.com/Azure/azclips/commit/0d9127fea4d40e12cdf08ae2de77a6d985bfbc45"
        },
        "date": 1786931916322,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 508953130.62,
            "unit": "ns",
            "range": "± 142213465.71519327"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 382567434.71,
            "unit": "ns",
            "range": "± 15564927.364105849"
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
          "id": "fd3287836c3d90de1936fbcca2d65a4da35666d2",
          "message": "{Help} MsLearn export tooling for CLI (#691)\n\nRef [CLIPS#588](https://github.com/Azure/CLIPS/issues/588).\n\nAdds the in-repo tooling for AzClips CLI reference to reach\n`learn.microsoft.com` via the same pipeline `azure-cli` uses. The\nout-of-repo pieces (publish workflow, cross-repo PRs, target docs repo)\nare separate CLIPS issues.\n\n## What ships\n\n- **`Azclips.Cli.Export`** (new build-time tool at\n`tools/Help/Azclips.Cli.Export/`) -- walks each extension's\n`IExtensionIndex`, builds `HelpManifest`, projects into CLI2Yaml's\nsnake_case `HelpFile` JSON contract, writes one file per top-level verb\nto `artifacts/cli-export/latest/<verb>.json`.\n- `dotnet run --project tools/Help/Azclips.Cli.Export` is the whole\ninvocation.\n- Optional `--extension <name>` filters to a single extension for\ndev-loop / partial regen.\n- Emits a synthesised `_root_.json` for the root `az` group (CLI2Yaml\nrequires it).\n- **Metadata attributes** for docs-only fields on parameters and\ncommands:\n- `[ParameterGroup(\"Storage Arguments\")]` -> CLI2Yaml\n`parameter.group_name`\n- `[PopulatorCommand(\"az vm list-sizes\")]` -> `parameter.value_sources`\n- `ProcessorInfoAttribute` extended with `PreviewMessage`,\n`ExperimentalMessage`, `DeprecatedMessage` -- Tags gate emission;\n`[Breaking Change]` sentinel routes to `breaking_change_info`.\n- **Side-car config files** at `src/CLI/Help/` --\n`.docsreference.azclips.json` (E1), `service_name.json` (E2),\n`index.json` (E3). Placeholder content per PR discussion.\n- **`build/Get-DocCommitMap.ps1`** -- generates `doc_commit_map.json`\n(E4) at CI time. Powers Learn's \"Edit this doc\" links and \"Last updated\"\ndates.\n- **PR-time fixtures + CI diff** -- small representative set (`_root_`,\n`login`, `vm`) at\n`tools/Help/Azclips.Help.Tool.Tests/Fixtures/cli-export/`. The\n`help-tool-tests` job fails loudly on shape drift with a message\npointing at `pwsh build/Update-CliExportFixtures.ps1`.\n\n## Globals -- single source of truth\n\n`Program.cs` (CLI2Yaml docs pipeline) and `RuntimeHelpRenderer` (in-tool\n`az X --help`) previously had no shared definition of global arguments.\nThis PR adds:\n\n- **`KnownGlobalFlags.All`** in `src/CLI/Parser/` -- public descriptor\nlist with `ParseAction` per flag.\n- `GlobalArgParser.Parse` dispatches through this list (`ParseAction`\nmirrors argparse's `Action` pattern used by `knack`/azure-cli).\n- `RuntimeHelpRenderer` renders a `Global Arguments` section at the\nbottom of every command / group `--help`.\n- Docs exporter's `GlobalArguments.cs` is a thin projection into\n`HelpFileParameter[]`.\n- Drift test in `Cli.Tests` asserts every declared global is recognised\nby the parser.\n\nNet effect: adding a new global flag = **one edit** in\n`KnownGlobalFlags.All`. Docs and runtime stay in sync by construction.\n\n## What it does NOT do\n\n- **Publish workflow** -- a bot PR that clones the target docs repo and\npushes YAML. Blocked on the target docs repo decision and `apidrop` feed\nauth on GH Actions runners.\n- **Markdown well-formedness preflight** (Markdig over `contentType:\nmarkdown` fields) -- planned alongside the publish workflow.\n- **Realistic per-verb `service_name.json` mapping** -- everything\ncurrently maps to `azure-cli`. Docs-review pass.\n- **Realistic `.docsreference.azclips.json` template copy** --\nplaceholder strings. Templates never render at emit time under Q3\nall-core anyway.\n- **Runtime `--help` parameter grouping** -- the `[ParameterGroup]`\nannotations flow through `HelpParameter.GroupName`;\n`RuntimeHelpRenderer` still lists parameters flat. One-file follow-up.\n- **`CLI2Yaml editLink` hardcode fix** (BL1) -- upstream PR to CLI2Yaml\nor regex-swap in the publish workflow. Not this PR.\n\n## Verification\n\n**Build + tests**:\n\n```\ndotnet build azclips.slnx --configuration Debug\ndotnet test azclips.slnx --no-build --configuration Debug\n```\n\nExpect all green except the pre-existing Windows-only\n`ManagedIdentityE2ETests.ManagedIdentity_E2E_MultipleIdSelectors_ShouldFail`.\n\n**Exporter**:\n\n```\ndotnet run --project tools/Help/Azclips.Cli.Export/Azclips.Cli.Export.csproj --no-build --configuration Debug\n```\n\nExpect `Wrote 11 verb file(s) + _root_.json to\n<repo>/artifacts/cli-export/latest`.\n\n**CLI2Yaml round-trip smoke** (needs Microsoft-tenant machine + apidrop\nfeed auth via silent MSAL): full runbook at\n[scratch/CLIPS-588-CLI2YAML-SMOKE-RUNBOOK.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/CLIPS-588-CLI2YAML-SMOKE-RUNBOOK.md).\nProduces `YamlMime:AzureCLIGroup` files under\n`%TEMP%\\azclips-cli2yaml-smoke\\output\\latest\\`.\n\n## Design context\n\nDesign and phase-by-phase implementation notes live at:\n\n-\n[scratch/CLIPS-588-TEMP-PR-PLAN.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/CLIPS-588-TEMP-PR-PLAN.md)\n-- design decisions (Q1-Q5), blockers (BL1-BL6), validation gates.\n-\n[scratch/CLIPS-588-A-IMPLEMENTATION-PLAN.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/CLIPS-588-A-IMPLEMENTATION-PLAN.md)\n-- 9-phase execution log with every checkpoint verified.\n-\n[scratch/AZCLIPS-TO-MSLEARN-LOOP.md](https://github.com/Azure/azclips/blob/azclips-588-cli-implementation/scratch/AZCLIPS-TO-MSLEARN-LOOP.md)\n-- pipeline spec (source of truth for the Loop page).\n\nTwo decisions worth calling out for the reviewer:\n\n- **We reuse CLI2Yaml** (docs.ms NuGet) rather than emit YAML ourselves.\nBypasses ~500 lines of transform reimplementation and inherits\n`azure-cli`'s operational path. Trade-off discussion in the Loop doc.\n- **All-core model** (`command_source: null` on every emitted record).\nAzClips has no author-visible core-vs-extension distinction today;\nexposing one would leak an implementation detail into\n`learn.microsoft.com`. Revisit if we ship a truly external extension.",
          "timestamp": "2026-08-17T17:32:32+10:00",
          "tree_id": "6e408cdfda4fe79a823c1dc37997a3f228a1be3a",
          "url": "https://github.com/Azure/azclips/commit/fd3287836c3d90de1936fbcca2d65a4da35666d2"
        },
        "date": 1786952765050,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 481301435.07,
            "unit": "ns",
            "range": "± 151477454.61141193"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 344332874.71,
            "unit": "ns",
            "range": "± 6281862.506770106"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "299377795+azure-client-tools-agent[bot]@users.noreply.github.com",
            "name": "azure-client-tools-agent[bot]",
            "username": "azure-client-tools-agent[bot]"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "2eee9597baee3f61cfaa258e5046865fc1d8a0bb",
          "message": "{DevOps} Trigger stable release 0.1.1 (#697)\n\n**Description**\n\nPrepare stable release 0.1.1.\n\n- Updates src/CLI/Core.Cli.csproj\n- Updates the root CHANGELOG.md\n- Updates src/PS/Az/Az.psd1\n- Updates PowerShell module changelogs and manifests\n\n**After merge**\n\nThe release trigger validates the version-only change on main, tags the\nmerged commit as v0.1.1.<UTC yyyyMMddTHHmmssZ>, and starts the stable\nbuild, signing, and publishing flow.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-08-18T09:10:03+10:00",
          "tree_id": "19b7e9312490ae409b2b74a2d62a607c6a95420b",
          "url": "https://github.com/Azure/azclips/commit/2eee9597baee3f61cfaa258e5046865fc1d8a0bb"
        },
        "date": 1787009493176,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 378674943.51,
            "unit": "ns",
            "range": "± 83256268.80991912"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 339922337.9,
            "unit": "ns",
            "range": "± 8983863.756841904"
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
          "id": "c0bd57c62e609ec484ea3423baab9c1d6ae590ef",
          "message": "{Help} Markdig well-formedness preflight (#706)\n\nAdd a build-time Markdig-based validator that parses each CLI help\nmarkdown file and flags inline patterns that render as surprising HTML\nthrough docfx's downstream Markdig pipeline. Complements the existing\nMarkdownStructureValidator, which handles structural checks; this one\ncatches inline-prose issues that pass structural checks but break at\nrender time.\n\nTwo new problem IDs:\n- MD_SuspiciousHtmlTag: <foo>-style placeholders that Markdig treats as\nHTML inlines (only a small allowlist of real HTML tags is accepted).\n- MD_UnbalancedBackticks: odd backtick counts inside paragraph text\n(fenced code blocks are excluded).\n\nWired into Azclips.Help.Tool's aggregate report so both PR CI (GitHub\nActions) and the internal build already exercise it, no new step needed.\n\nRuns clean against the current CLI docs corpus (11 verb docs, zero\nfindings). 7 unit tests cover clean content, placeholder detection,\nallowed real HTML, balanced/unbalanced backticks, fenced-code exclusion,\nand multi-issue aggregation.\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-18T12:03:40+10:00",
          "tree_id": "6460fdb7d5c3ae97f4da6ad09a90b6d08f37ac76",
          "url": "https://github.com/Azure/azclips/commit/c0bd57c62e609ec484ea3423baab9c1d6ae590ef"
        },
        "date": 1787019688211,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 357679295.9,
            "unit": "ns",
            "range": "± 24762182.478449766"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 338257033.95,
            "unit": "ns",
            "range": "± 5137868.161337119"
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
          "id": "a0947abac40ca61ef46b33f2f9c309c09219e2fa",
          "message": "[Help] Group --help parameters into named sections (#711)\n\n## Summary\n\nParameters in `az <verb> --help` output can now be grouped into named\nsections via a new `[ParameterGroup]` attribute on processor properties.\nFirst adoption: `az vm create` splits its 22 parameters across\n`Arguments` / `Authentication Arguments` / `Network Arguments`, matching\nreal Azure CLI conventions.\n\nCloses the runtime loop on the 588 track — the exporter side already\nemitted `group_name` fields from 588-A (#691); this PR teaches the\nruntime help renderer to consume them.\n\n## User-visible impact\n\n`az vm create --help` before this PR: 22 parameters rendered flat under\na single `Arguments` header.\n\nAfter:\n\n    Command\naz vm create : Create a virtual machine and its supporting network\nresources.\n\n    Arguments\n        --subscription-id [Required] : Azure Subscription Id.\n        --name [Required]            : The name of the virtual machine.\n        --location [Required]        : Location for all resources.\n        --resource-group [Required]  : The resource group name.\n        --vm-size [Required]         : The size of the virtual machine.\n--profile : Use the named profile for this command ...\n\n    Authentication Arguments\n        --admin-username [Required]  : The administrator username.\n        --admin-password [Required]  : The administrator password.\n\n    Network Arguments\n        --network-interface-name [Required] : ...\n        --virtual-network-name [Required]   : ...\n        ... 15 params\n\n    Global Arguments\n        --debug ...\n\nUn-annotated commands (`az login`, `az config get`, etc.) render\nbyte-identically to `main` — the mechanism is dormant until an attribute\nfires.\n\n## Implementation\n\n- **`RuntimeHelpParameter.GroupName`** — new nullable field on the\nruntime help contract.\n- **`ProcessorParameterReflection`** — new shared helper in\n`src/CLI/Help/`. Both the build-time exporter\n(`ExtensionHelpManifestBuilder`) and the runtime adapter\n(`ExtensionIndexHelpAdapter`) call it to read `[ParameterGroup]` off the\nprocessor property. Consolidating here keeps the two consumers from\ndrifting.\n- **`ExtensionIndexHelpAdapter`** — reflects on the processor type at\nhelp-render time and populates `RuntimeHelpParameter.GroupName`. Small\nreflection cost; help is not a hot path.\n- **`RuntimeHelpRenderer`** — new `AppendParameterSections`: splits\nparams into buckets keyed by `GroupName`, renders default `Arguments`\nfirst, named groups alphabetical, `Global Arguments` last. Empty\nsections skipped. Insertion order preserved within each bucket.\n\n## Compute annotations\n\nMapping cross-referenced against real `az vm create --help` (azure-cli\n`dev`):\n\n- `[ParameterGroup(\"Authentication Arguments\")]` on `AdminUsername`,\n`AdminPassword`.\n- `[ParameterGroup(\"Network Arguments\")]` on `NetworkInterfaceName`,\n`VirtualNetworkName`, `NetworkSecurityGroupName`, `SubnetNames`,\n`SubnetAddressPrefixes`, `AddressPrefixes`, and all 9 `Rule*` fields.\n- Ungrouped (default `Arguments`): `SubscriptionId`, `Name`, `Location`,\n`ResourceGroupName`, `VmSize`.\n\n## Open decisions worth flagging\n\n- **`SubscriptionId` left in default `Arguments`.** Azure CLI treats\n`--subscription` as a *global* argument; promoting `SubscriptionId` to\nour `KnownGlobalFlags` would match that convention but touches broader\nprofile plumbing. Deferred as a follow-up.\n- **`Rule*` fields under `Network Arguments`.** Azure CLI exposes only\n`--nsg-rule` (a preset). Our orchestrator exposes each rule field\nindividually; grouping under `Network Arguments` matches \"network\nconfiguration\". Alternative would be a dedicated `NSG Rule Arguments` —\nrejected as over-taxonomy for a handful of fields.\n- **Grouping is opt-in, not enforced.** Matches azure-cli's own\nconvention (no linter requires every param to be grouped). Small\ncommands rightfully use only the default bucket.\n\n## Not in this PR (deferred)\n\n- Annotating processors other than `NewVMProcessor_Orchestrator`. Extend\non evidence in follow-ups.\n- `PopulatorCommand` / `ValueSources` in runtime output — runtime\ndoesn't display value-source hints today; separate feature.\n- Column alignment / padding polish — renderer has none today either.\n\n## Validation\n\n- `dotnet build azclips.slnx` — 0 errors, 0 warnings.\n- `Cli.Tests` Help suite: 78 → 82 (4 new renderer tests: grouped\nheaders, alphabetical ordering, legacy-layout regression guard, and\nomitted default `Arguments` when everything is grouped).\n- `Azclips.Help.Tool.Tests`: 190/190 (`vm.json` golden regenerated via\n`pwsh build/Update-CliExportFixtures.ps1`; diff is 17 `group_name`\nadditions, zero unrelated churn).\n- Live `az vm create --help`: 3 named sections + `Global Arguments`,\ninsertion order preserved.\n- Live `az login --help`: byte-identical to `main`.\n\n## History Notes\n\n[Help] `az <verb> --help` now supports grouping parameters into named\nsections via a `[ParameterGroup]` attribute on processor properties.\nFirst user-visible adoption: `az vm create` splits its parameters into\n`Authentication Arguments` and `Network Arguments` sections.\n\n## References\n\n- 588 master plan: `scratch/CLIPS-588-TEMP-PR-PLAN.md` (follow-up entry)\n- PR plan: `scratch/CLIPS-588-runtime-help-grouping-PR-PLAN.md`\n- Related: #691 (588-A exporter), #706 (588-D1 Markdig preflight)",
          "timestamp": "2026-08-18T16:17:19+10:00",
          "tree_id": "d95a31f955076f60526fd2f0c684af127d6ddcf1",
          "url": "https://github.com/Azure/azclips/commit/a0947abac40ca61ef46b33f2f9c309c09219e2fa"
        },
        "date": 1787034713294,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 372221527.13,
            "unit": "ns",
            "range": "± 67197463.58517244"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 340845949.61,
            "unit": "ns",
            "range": "± 12243572.412357943"
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
          "id": "58777b9ee26b7566efb56cf387cdfebc4c4bad42",
          "message": "[Help] Show 'Values from:' hint under --help parameters (#714)\n\n## Summary\n\nSecond runtime-side follow-up to 588-A (#691). Pairs with #711's\nparameter grouping — same reflection pattern, twin attribute.\n\n`RuntimeHelpRenderer` now emits `Values from: <cmd>.` under any\nparameter annotated with `[PopulatorCommand]`, matching Azure CLI's\ndiscoverability hint format. First user-visible adoption: `az vm create`\n— `--resource-group` now points users at `az group list`.\n\n## User-visible impact\n\n`az vm create --help` after this PR:\n\n    Arguments\n        --subscription-id [Required] : Azure Subscription Id.\n        --name [Required]            : The name of the virtual machine.\n        --location [Required]        : Location for all resources.\n        --resource-group [Required]  : The resource group name.\n            Values from: az group list.\n        --vm-size [Required]         : The size of the virtual machine.\n        ...\n\nOnly `--resource-group` picks up the new line; every other parameter is\nunchanged. Un-annotated commands (`az login`, `az config get`, etc.)\nrender byte-identical to `main`.\n\n## History Notes\n\n[Help] `az <verb> --help` now shows a `Values from: <command>` hint\nunder parameters annotated with `[PopulatorCommand]`, matching Azure\nCLI's discoverability convention. First user-visible adoption: `az vm\ncreate --resource-group` points users at `az group list`.\n\n## References\n\n- PR plan: `scratch/CLIPS-588-populator-command-PR-PLAN.md`\n- Preceding runtime rendering PR: #711 (parameter grouping) — same\nreflection pattern\n- Related: #691 (588-A exporter, where `[PopulatorCommand]` was\nintroduced), #706 (588-D1 Markdig preflight)",
          "timestamp": "2026-08-19T12:47:04+10:00",
          "tree_id": "627f0368902b30e6efa0f2e3ba28d22a8213656f",
          "url": "https://github.com/Azure/azclips/commit/58777b9ee26b7566efb56cf387cdfebc4c4bad42"
        },
        "date": 1787108514344,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 365502320.33,
            "unit": "ns",
            "range": "± 7680429.526736817"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 352710490.21,
            "unit": "ns",
            "range": "± 6607551.780363495"
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
          "id": "528350afeccb2994d715537428ff5192faed1296",
          "message": "{Benchmark} Benchmark to use real ProfileService I/O (#689)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- In terms of performance it is only this solution's responsibility to\nmonitor and improve client overhead performance and not downstream\nservices\n- Use production `ProfileService` during benchmarking to ensure accurate\nmeasurement of user experience of client sections.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-20T15:11:09+10:00",
          "tree_id": "5b17c7a9867b21719f9839adcbf8886315cee687",
          "url": "https://github.com/Azure/azclips/commit/528350afeccb2994d715537428ff5192faed1296"
        },
        "date": 1787203551225,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 361177375.71,
            "unit": "ns",
            "range": "± 7488895.930236798"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 349446817.47,
            "unit": "ns",
            "range": "± 6345920.037103185"
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
          "id": "ee65ffe75e05ed5cfa4b71ef0052585185540512",
          "message": "{Telemetry} Refactor file telemetry sender (#722)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Refactor detached process telemetry exporter workflow\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n- `Debug` export telemetry to Jaeger\n- `Release` export telemetry App Insights\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-24T10:08:24+10:00",
          "tree_id": "58bd78428337ff2853b7e6f44c4a8e4aeead5cf9",
          "url": "https://github.com/Azure/azclips/commit/ee65ffe75e05ed5cfa4b71ef0052585185540512"
        },
        "date": 1787530990153,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 438155866.71,
            "unit": "ns",
            "range": "± 126284419.81724511"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 337656043.14,
            "unit": "ns",
            "range": "± 5642738.06899706"
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
          "id": "743b7adbeccf60f920b03069576b13951404c953",
          "message": "{Benchmark} Mock out network calls for MsalService (#693)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Microbenchmarks to use real msalservice during benchmarking.\n- During benchmark real file i/o will be measured for msalservice, but\nany potential network calls for fetching/refreshing auth tokens will be\nshort circuited.\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-25T08:57:42+10:00",
          "tree_id": "c0f217fecc7e703a30235d61d6b3ac3688dda545",
          "url": "https://github.com/Azure/azclips/commit/743b7adbeccf60f920b03069576b13951404c953"
        },
        "date": 1787613725037,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 359481937.49,
            "unit": "ns",
            "range": "± 21743059.320037045"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 390504601.91,
            "unit": "ns",
            "range": "± 110267863.46894228"
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
          "id": "d035a5168dd3d9467fd089917ce3ea6ff2f0984a",
          "message": "{Telemetry} Fix piped STDOUT telemetry stall (#753)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n`az account show profile --active | cat -`\n`az profile show --active | jq -r .userPrincipalName`\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Fix telemetry emission child processes maintaining references to\nparent pipe buffers for both unix and windows causing delayed pipe\nexecution\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n`time az account show profile --active | cat -`\n`time az profile show --active | jq -r .userPrincipalName`\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-26T01:35:21+10:00",
          "tree_id": "6c9245b4524536e49aa74a46798c04b2805e24bf",
          "url": "https://github.com/Azure/azclips/commit/d035a5168dd3d9467fd089917ce3ea6ff2f0984a"
        },
        "date": 1787673054491,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 359166944.75,
            "unit": "ns",
            "range": "± 6857522.5229954235"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 348299557.05,
            "unit": "ns",
            "range": "± 6051013.218485613"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "55773468+notyashhh@users.noreply.github.com",
            "name": "Yash",
            "username": "notyashhh"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "e8e0610acc531f93eb6e963667f52b0523f383bf",
          "message": "[MsGraph] Module (#423)\n\nhttps://github.com/Azure/CLIPS/issues/356\n\n---------\n\nCo-authored-by: Maddison Das <maddisondas@microsoft.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>\nCo-authored-by: Maddison Das <272712104+MaddyMicrosoft@users.noreply.github.com>\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-08-27T15:30:54+10:00",
          "tree_id": "926f31614850231274f86ba2ff5817428a2a7220",
          "url": "https://github.com/Azure/azclips/commit/e8e0610acc531f93eb6e963667f52b0523f383bf"
        },
        "date": 1787810127154,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 360689290.26,
            "unit": "ns",
            "range": "± 8511174.876554536"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 347616353.95,
            "unit": "ns",
            "range": "± 12605531.140731266"
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
          "id": "2787390220ad1df54f4aeef45b295aae16f82fc7",
          "message": "{Misc} update min version of dotnet sdk (#767)\n\nUpdated Min version of .Net SDK for CVE mitigations",
          "timestamp": "2026-08-28T14:09:04+08:00",
          "tree_id": "640ed6d730606938671721f36bed21e1dcd842ea",
          "url": "https://github.com/Azure/azclips/commit/2787390220ad1df54f4aeef45b295aae16f82fc7"
        },
        "date": 1787898238243,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 362348588.46,
            "unit": "ns",
            "range": "± 14229533.296177544"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 351130698.14,
            "unit": "ns",
            "range": "± 18536450.822019085"
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
          "id": "c95435039be30271dee86a17416282b72ee92f61",
          "message": "{Benchmark} Emit telemetry for CLI benchmarks (#744)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- Have CLI benchmarks emit a sampled set of telemetry traces to be used\nin further aggregation in benchmark pipelines to show potential\nregressions.\n- Sampled telemetry emitted as `benchmarkMethod.jsonl` in\n`artifacts/Benchmarks/telmetry`\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-31T11:54:37+10:00",
          "tree_id": "4fc89f2c8363fcdb93cd2de48254ce0eef60cf3c",
          "url": "https://github.com/Azure/azclips/commit/c95435039be30271dee86a17416282b72ee92f61"
        },
        "date": 1788142439776,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363037944.75,
            "unit": "ns",
            "range": "± 23135270.13122157"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 350960432.4,
            "unit": "ns",
            "range": "± 10246210.33910499"
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
          "id": "347d9c301e6a90fbbe197d858513d966f6248a5f",
          "message": "[Login] Rename reserved bare-login profile from temp to default (#776)\n\n**Related command**\n\n`az login` / `Connect-AzAccount` (bare sign-in without `--out-profile` /\n`-OutProfile`). Also surfaced by `az profile list` / `az profile show` /\n`Get-AzProfile`.\n\n**Description**\n\nRenames the reserved profile that bare login creates from `temp` to\n`default`, per private-preview feedback (#490) that \"temp\" was confusing\nabout the profile's purpose and lifecycle (maintainer suggestion:\n\"Default makes more sense\").\n\n- `az login` / `Connect-AzAccount` without `--out-profile` now saves and\nactivates a reserved profile named `default` (previously `temp`).\n- `default` is reserved: it is rejected as an `--out-profile` /\n`-OutProfile` value, exactly as `temp` was.\n- Centralized rename: `ProfileNameValidator.ReservedTempName` →\n`ReservedDefaultName` (`\"temp\"` → `\"default\"`); the reserved-name error\nmessage derives from the constant.\n- No migration: any pre-existing `temp` profile on disk remains as an\nordinary, no-longer-reserved profile. This is acceptable for private\npreview.\n- Docs, wiki, and the openspec change are updated; the openspec\n`design.md` Decision 3 keeps its original \"why temp\" rationale with a\ndated `#490` reversal note for audit trail.\n\nNote: `default` reintroduces the \"is this a specific profile or the\nactive/default one?\" mental-model tension the original design cited.\nVerified there is no code collision — `Engine.ResolveProfileAsync`\ntracks the active profile via the separate `active-profile` pointer file\nand never treats `\"default\"` as a sentinel. The naming is the\nmaintainer's call.\n\n**Testing Guide**\n\n- `dotnet build azclips.slnx`\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~Profile\"`\n- `dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj`\n- Manual: `az login --subscription-id <sub>` (no `--out-profile`) → `az\nprofile list` shows an active profile named `default`; `az login\n--out-profile default` fails as reserved.\n\n**History Notes**\n\n[Login] `az login` / `Connect-AzAccount`: Bare sign-in now saves the\nreserved profile as `default` instead of `temp` (#490)\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\nCo-authored-by: Maddison Das <272712104+MaddyMicrosoft@users.noreply.github.com>",
          "timestamp": "2026-08-31T15:19:20+10:00",
          "tree_id": "95f1c33359c8bc47f1dc40a9f3a92e5f8f8e9414",
          "url": "https://github.com/Azure/azclips/commit/347d9c301e6a90fbbe197d858513d966f6248a5f"
        },
        "date": 1788154454910,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 368210083.76,
            "unit": "ns",
            "range": "± 7880329.7133423975"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 354687269.63,
            "unit": "ns",
            "range": "± 5542238.093728785"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "299377795+azure-client-tools-agent[bot]@users.noreply.github.com",
            "name": "azure-client-tools-agent[bot]",
            "username": "azure-client-tools-agent[bot]"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "6b96536b5f2213b1e6e7c5c4a9be4d95e5c444e0",
          "message": "{DevOps} Trigger stable release 0.1.2 (#781)\n\n**Description**\n\nPrepare stable release 0.1.2.\n\n- Updates src/CLI/Core.Cli.csproj\n- Updates the root CHANGELOG.md\n- Updates src/PS/Az/Az.psd1\n- Updates PowerShell module changelogs and manifests\n\n**After merge**\n\nThe release trigger validates the version-only change on main, tags the\nmerged commit as v0.1.2.<UTC yyyyMMddTHHmmssZ>, and starts the stable\nbuild, signing, and publishing flow.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-09-01T08:53:12+09:30",
          "tree_id": "34bd9a46b4869d828e47a751f7d92860f53f4ab9",
          "url": "https://github.com/Azure/azclips/commit/6b96536b5f2213b1e6e7c5c4a9be4d95e5c444e0"
        },
        "date": 1788219556532,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 464179850.82,
            "unit": "ns",
            "range": "± 300185489.2438034"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 364961873.45,
            "unit": "ns",
            "range": "± 84822506.59926738"
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
          "id": "8acf5923ad492eb7e2771ad3f2801ca5f8e8f5a8",
          "message": "[Identity] `az identity`: Add cli support for create, list, show, update, delete (#780)\n\nImplements https://github.com/Azure/CLIPS/issues/840\n\n## Summary\n\n- Added CLI support for `az identity` - \"create\", \"list\", \"show\",\n\"update\", \"delete\"\n- Added recording sanitizer coverage for identity-related values in\nsession recordings, including tenant ID, client ID, and subscription ID.\n- Added CLI help documentation for `az identity`.\n- Added unit and scenario coverage for the new identity command surface.\n- **Note: PowerShell cmdlet support and PowerShell help documentation\nwill be added in a follow-up PR.**\n\n## Validation\n\nUnit/scenario tests added, and live E2E validation passed using:\n\n```bash\n./tests/Scripts/CLI/IdentityLifecycle.sh\n```\n\n---\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-01T09:58:34+08:00",
          "tree_id": "65ec62cdea73fc2fd40ab7d1115805d504ce8287",
          "url": "https://github.com/Azure/azclips/commit/8acf5923ad492eb7e2771ad3f2801ca5f8e8f5a8"
        },
        "date": 1788228810641,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 373824010.34,
            "unit": "ns",
            "range": "± 6830986.603868375"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 360325308.18,
            "unit": "ns",
            "range": "± 5911494.475758365"
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
          "id": "8b82573f9e7a32a90e0fbc7307d636444b1efe12",
          "message": "{Telemetry} Emit real telemetry from PowerShell benchmarks (#757)\n\nStacked on #744 — review that one first.\n\n## What\n\nPowerShell micro-benchmarks previously suppressed telemetry via\n`StubTelemetryService`, so a PS benchmark told you *how long*\n`Get-AzResourceGroup` took but not *where the time went*. This makes\nthem emit real spans through the production pipeline, split by\nBenchmarkDotNet stage and sampled into one `.jsonl` per benchmark method\n— matching what the CLI benchmarks already produce.\n\n## The constraint\n\nThe file write must not land inside the measured region. Production\nPowerShell batches its export off-thread, so benchmarks preserve that:\nthe timed region pays only a flag check and a queue enqueue, and\n`[IterationCleanup]`'s container disposal drains the queue to disk —\nuntimed.\n\n## Production changes\n\n**`ModuleInitializer`** — `ITelemetrySender` moves from the override\nphase to the prepend phase, so a host (the benchmark) can substitute\none. Production PowerShell still resolves `DirectTelemetrySender`; two\ntests lock that contract.\n\n**`FileExporter`** — fixes a latent bug found while validating this\nwork. It enumerated the batch twice: once to name the file, once to\nwrite. A circular-buffer-backed `Batch<T>` dequeues as it is enumerated,\nso the first span of the first batch was silently dropped. Now a single\npass. This affects the CLI and TUI file-buffered paths too; they escaped\nit only because `SimpleActivityExportProcessor` delivers one span per\nbatch.\n\n## Benchmark-side\n\nFour `internal sealed` types in `tests/Benchmark/Telemetry/`, none\nreachable from `src/`. `StageLatch` samples the stage at span end — the\nonly moment the answer is knowable, since the engine's flag reads false\nin both `[IterationSetup]` and `[IterationCleanup]`.\n\nShared helpers extracted along the way: `BenchmarkTelemetryArtifacts`,\nand `DirectoryUtils.TryDeleteDirectory` in `Tests.Common`, which\nreplaced four duplicate implementations.\n\n## Validation\n\n- `dotnet build azclips.slnx` — 0 errors\n- Core.Telemetry.Tests 226/226, Cmdlets.Tests 100/100\n- Live `--product ALL` run at 5 iterations: every `.jsonl` holds exactly\n5 traces, 0 module-import spans, 5 activity roots. Exactly 5 — not 0\n(stage read outside the measured region) and not 10 (stage listener\nnever fired) — is the load-bearing assertion.\n- CLI output unchanged, confirming the shared-helper extraction did not\nregress it.\n\n## Note for reviewers\n\nPS benchmark means and `[MemoryDiagnoser]` numbers step up once,\nuniformly, because every iteration now creates real spans where the stub\ndid nothing. The regression gate will likely need a baseline reset on\nthe run this lands — the same one-time discontinuity the CLI change\ndocumented.\n\nDesign doc:\n`docs/superpowers/specs/2026-08-20-ps-benchmark-telemetry-design.md`\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-01T13:30:34+10:00",
          "tree_id": "daa95980df3896d7f183ebe0e6a8c802c1dbe670",
          "url": "https://github.com/Azure/azclips/commit/8b82573f9e7a32a90e0fbc7307d636444b1efe12"
        },
        "date": 1788234613954,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 359703785.49,
            "unit": "ns",
            "range": "± 5810872.403431928"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 347599254.86,
            "unit": "ns",
            "range": "± 5930613.311012457"
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
          "id": "3ab4578748e88131e3e67f608683eea6aacc8829",
          "message": "{Benchmark} Fix start up login benchmarks (#792)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-03T10:59:36+10:00",
          "tree_id": "5c32ee353a65d695383df725a7e82fe76584d7f5",
          "url": "https://github.com/Azure/azclips/commit/3ab4578748e88131e3e67f608683eea6aacc8829"
        },
        "date": 1788398233257,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 369630831.27,
            "unit": "ns",
            "range": "± 6587477.854283384"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 356875523.05,
            "unit": "ns",
            "range": "± 6499395.7014526175"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "xumi@microsoft.com",
            "name": "xuming-ms",
            "username": "xuming-ms"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "610257fb45020825ce93032690d49519dafa1ad8",
          "message": "[Login] Make managed identity authentication compatible with classic CLIPS (#806)\n\n**Related command**\n\n`az login`, `Connect-AzAccount`\n\n**Description**\nMake managed identity authentication compatible with classic Azure CLI\nand Azure PowerShell\n\n\n**PowerShell — `Connect-AzAccount`**\n\n| Parameter | Added aliases |\n| --- | --- |\n| `-Identity` | `MSI`, `ManagedIdentity`, `ManagedService` |\n| `-ClientId` | `AccountId` |\n\n**CLI — `az login`**\n\n| Parameter | Added aliases |\n| --- | --- |\n| `--identity` | `-i` |\n\n\n**Testing Guide**\n\n```bash\ndotnet build azclips.slnx\ndotnet test tests/Cli.Tests/Cli.Tests.csproj\ndotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj\ndotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj\ndotnet test tests/Scenario.Tests/Scenario.Tests.csproj\n```\n\n989 passed / 0 failed. New tests resolve the binder through DI rather\nthan constructing core services directly. Also verified manually against\nthe built client:\n\n\n**History Notes**\n\n[Login] `Connect-AzAccount`: Add `MSI`, `ManagedIdentity` and\n`ManagedService` aliases for `-Identity`, and `AccountId` for\n`-ClientId`\n[Login] `az login`: Add the `-i` alias for `--identity`\n[Login] BREAKING CHANGE: `az login`: Remove the `--subscription` and\n`-s` aliases, use `--subscription-id`\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-04T15:45:22+10:00",
          "tree_id": "5c8caf56ab6390b16b860656e18549aab78ca447",
          "url": "https://github.com/Azure/azclips/commit/610257fb45020825ce93032690d49519dafa1ad8"
        },
        "date": 1788502014654,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 368580284.34,
            "unit": "ns",
            "range": "± 6730635.068042605"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 353041874.79,
            "unit": "ns",
            "range": "± 7149988.780001899"
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
          "id": "f7d72b40d4694026552332cb40b37738078007f1",
          "message": "[Identity] `Az.ManagedServiceIdentity`: Add Get/New/Update/Remove-AzUserAssignedIdentity (#801)\n\n**Description**\nThis PR completes https://github.com/Azure/CLIPS/issues/840 to include\nPS Cmdlet\n-  Added Cmdlets.Identity PowerShell module with:\n   - New-AzUserAssignedIdentity\n   - Get-AzUserAssignedIdentity\n   - Update-AzUserAssignedIdentity\n   - Remove-AzUserAssignedIdentity\n- Added help documentation\n- Added test coverage\n- Refactored Extensions.Identity based on processor/orchestrator\nmigration guidance for customisations and spec alignment - got rid of\nany customisations in the processor and superfluous validations, also\nupdated to use core pagination for the list command\n\n\n**Testing Guide**\n- Unit tests added, and live E2E validation passed using:\n```\n./tests/Scripts/PS/IdentityLifecycle.ps1\n```\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-07T09:51:26+10:00",
          "tree_id": "b837313a6f05f20865704acc39353a9d5502e766",
          "url": "https://github.com/Azure/azclips/commit/f7d72b40d4694026552332cb40b37738078007f1"
        },
        "date": 1788739888881,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 356576973.15,
            "unit": "ns",
            "range": "± 5758701.54480727"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 348847578.9,
            "unit": "ns",
            "range": "± 13305554.141136833"
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
          "id": "b46e898b82ec48fc2ab31744907c5507ab2a6ea3",
          "message": "{Telemetry} Add separate trace for AI agent orchestration (#782)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\nAny command that errors out \n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n- This fixes the issue outlined in\nhttps://github.com/Azure/CLIPS/issues/602\n- Dispose of core command activity before AI agent executable is run\n- Instrument AI agent orchestration for tracability.\n- The relationship between traces is (AI agent trace (child)) ->\n(Command root trace (parent)) (singular direction).\n\nAs illustrated below, when a command is going through the AI agent to\npropose potential recover steps. The trace only captures the duration of\nthe excepted command\n\n<img width=\"3838\" height=\"1024\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/dc85c74f-4bd7-41e5-ae70-7ff15aa5ec7d\"\n/>\n\nThe entire duration of the AI agent loop is captured as a separate trace\ninstead, as illustrated below.\n\n<img width=\"3815\" height=\"1783\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/373a542e-3c63-46d2-a330-fbd4398cace3\"\n/>\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n1. Ensure `agent.enabled` is enabled using `az config set --key\nagent.enabled --value true`\n2. Run any command that will lead to an exception being generated i.e\n`az profile show`\n3. Observe the trace in app insights or jaeger\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-07T12:58:49+10:00",
          "tree_id": "adf9d3021f5db5686906812239967d287960e603",
          "url": "https://github.com/Azure/azclips/commit/b46e898b82ec48fc2ab31744907c5507ab2a6ea3"
        },
        "date": 1788753070834,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 370179978.95,
            "unit": "ns",
            "range": "± 8461303.389217718"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 351304307.09,
            "unit": "ns",
            "range": "± 7295149.158993211"
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
          "id": "b52c031228e57579b95524e46cbaac0c91f083d7",
          "message": "[Auth] Rename Az.Login module and Login extension to Az.Accounts (#804)\n\n**Related command**\n\nAz.Accounts (Connect-AzAccount, Disconnect-AzAccount, Get-AzProfile,\nSet-AzProfile, Remove-AzProfile); az login / az logout / az profile\n\n**Description**\n\nRenames the authentication surface from `Login` to `Accounts` so the\npreview matches the shipping Azure PowerShell name `Az.Accounts`. Two\nlogical steps (one commit each):\n\n1. PowerShell module `Az.Login` -> `Az.Accounts`: folder `src/PS/Login`\n-> `src/PS/Accounts`, `Login.psd1` -> `Accounts.psd1`,\nassembly/namespace `Microsoft.Azure.Azclips.Cmdlets.Login` ->\n`...Cmdlets.Accounts`, and the `Az` rollup plus\n`Compute`/`Config`/`Resources`/`MsGraph` `RequiredModules` references.\n2. Extension `Login` -> `Accounts`: folder `src/Extensions/Login` ->\n`src/Extensions/Accounts`, assembly/namespace\n`Microsoft.Azure.Azclips.Extensions.Login` -> `...Extensions.Accounts`,\nand `Constants.ExtensionName` (the short name the cmdlets hand to\n`LoadExtension`).\n\nUser/maintainer impact:\n- The imported module is now `Accounts` (`Import-Module Accounts.psd1`,\n`Get-Command -Module Accounts`).\n- The bundled extension short name is now `Accounts` (`az extension add\n--name Accounts`).\n- No behavior change to cmdlets or CLI verbs: `Connect-AzAccount` and\n`az login` / `az logout` / `az profile` are unchanged. Processor class\nnames (`LoginProcessor`, etc.) are also unchanged.\n\n**Testing Guide**\n\n    dotnet build azclips.slnx -c Debug\n    dotnet test tests/Cli.Tests/Cli.Tests.csproj -c Debug\ndotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj -c\nDebug\n    dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj -c Debug\n\nManual smoke:\n\n    ./artifacts/Debug/Core.Cli/az login --help\n    ./artifacts/Debug/Core.Cli/az profile list\npwsh -NoProfile -c \"Import-Module ./src/PS/Accounts/Accounts.psd1\n-Force; (Get-Command -Module Accounts).Name\"\n\nExpected: build clean; Cli.Tests 634 passed / 8 skipped,\nCore.Extensions.Tests 82 passed, Cmdlets.Tests 100 passed; `az\nlogin`/`az profile` resolve; the module exports the five cmdlets.\n\nNote for reviewers/local envs: any previously installed pre-rename\nextension at\n`~/.azclips/extensions/Microsoft.Azure.Azclips.Extensions.Login` must be\nremoved, otherwise it collides with the bundled `Accounts` extension\n(both claim the `login` verb) and every `az` command fails with\n`Duplicate leaf '(login, )'`.\n\n**History Notes**\n\n[PowerShell] `Az.Accounts`: Rename the `Az.Login` module to\n`Az.Accounts`\n[CLI] `az extension`: Rename the bundled Login extension to Accounts\n\n---\n\n- [x] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [x] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [x] I added or updated tests for behavior changes.\n- [x] I updated documentation when behavior, setup, support, or security\nguidance changed.\n\n---------\n\nCo-authored-by: Maddison Das <272712104+MaddyMicrosoft@users.noreply.github.com>\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-07T17:39:08+10:00",
          "tree_id": "fce52aa36cb05b9a03197ca15d97609039758f5b",
          "url": "https://github.com/Azure/azclips/commit/b52c031228e57579b95524e46cbaac0c91f083d7"
        },
        "date": 1788768171961,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 363171376.78,
            "unit": "ns",
            "range": "± 55070041.96366305"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 345887215.02,
            "unit": "ns",
            "range": "± 9144863.113542665"
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
          "id": "4096f675af7b68e540d54dfd9fc2db0dc982627e",
          "message": "{Telemetry} Aggregate benchmark traces into ASCII span-tree reports (#762)\n\nStacked on #757.\n\nThere has been feedback that although a regression is detected it\ndifficult for a team to make informed decision on whether there is a\ntrue regression, benchmark noise or there has been an acceptable\nbusiness logic change due to a lack of context.\n\nThis addresses it by sampling and emitting the trace tree as an ASCII\ntree in the CI/CD summary to aid in performance decision making.\n\nThe entire CI/CD pipeline along with a dedicated benchmark agent pool\nwill be migrated/constructed later in `.azure-pipeliens`\n\n## What it looks like\n\n```\n Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine\n COMPUTE · CLI\n\n                          main @ 285fabd · 3 days ago · 100 traces                            │         │                                   PR @ ceb088e · 100 traces\n         AVG  %ROOT                                                                           │    Δ%   │          AVG  %ROOT\n   10.312 ms   100%  ####################  CLI.Activity.Root                                  │   -7.8% │     9.506 ms   100%  ####################  CLI.Activity.Root\n    1.529 ms    15%  ###                   ├─ AzureCLI.Startup                                │   +0.4% │     1.535 ms    16%  ###                   ├─ AzureCLI.Startup\n    0.045 ms     0%                        │  ├─ AzureCLI.Startup.Container                   │  +34.0% │     0.061 ms     1%                        │  ├─ AzureCLI.Startup.Container\n    1.141 ms    11%  ##                    │  ├─ AzureCLI.Startup.Services                    │   -1.8% │     1.120 ms    12%  ##                    │  ├─ AzureCLI.Startup.Services\n    0.315 ms     3%                        │  └─ AzureCLI.Startup.Telemetry                   │   +7.3% │     0.338 ms     4%                        │  └─ AzureCLI.Startup.Telemetry\n    0.005 ms     0%                        ├─ AzureCLI.ParseGlobalArgs                        │  +27.9% │     0.007 ms     0%                        ├─ AzureCLI.ParseGlobalArgs\n    0.053 ms     1%                        ├─ AzureCLI.HandleGlobalArgs                       │  +18.3% │     0.063 ms     1%                        ├─ AzureCLI.HandleGlobalArgs\n    0.525 ms     5%  #                     ├─ AzureCLI.ParseCommandInvocation                 │   +4.3% │     0.548 ms     6%  #                     ├─ AzureCLI.ParseCommandInvocation\n    0.244 ms     2%                        │  └─ SessionCreated                               │   -3.1% │     0.237 ms     2%                        │  └─ SessionCreated\n    7.905 ms    77%  ###############       └─ AzureCLI.ProcessCommand                         │   -9.0% │     7.192 ms    76%  ###############       └─ AzureCLI.ProcessCommand\n    7.774 ms    75%  ###############          ├─ SessionCreated                               │   -8.4% │     7.121 ms    75%  ###############          ├─ SessionCreated\n    7.264 ms    70%  ##############           ├─ ProcessorProcessAsyncStart                   │   -8.6% │     6.641 ms    70%  ##############           ├─ ProcessorProcessAsyncStart\n    0.590 ms     6%  #                        ├─ AuthBeforeTokenAcquisition                   │   -1.6% │     0.581 ms     6%  #                        ├─ AuthBeforeTokenAcquisition\n    0.521 ms     5%  #                        ├─ MsalAuthService.AcquireTokenSilentlyAsync    │   -0.9% │     0.517 ms     5%  #                        ├─ MsalAuthService.AcquireTokenSilentlyAsync\n    0.496 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[1]                │  -19.0% │     0.402 ms     4%                           ├─ AuthBeforeTokenAcquisition[1]\n    0.429 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[1] │  -19.2% │     0.347 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[1]\n    0.486 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[2]                │  -21.9% │     0.379 ms     4%                           ├─ AuthBeforeTokenAcquisition[2]\n    0.421 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[2] │  -22.0% │     0.328 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[2]\n    0.478 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[3]                │  -28.4% │     0.342 ms     4%                           ├─ AuthBeforeTokenAcquisition[3]\n    0.415 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[3] │  -29.5% │     0.293 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[3]\n    0.481 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[4]                │  -25.4% │     0.359 ms     4%                           ├─ AuthBeforeTokenAcquisition[4]\n    0.421 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[4] │  -25.3% │     0.314 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[4]\n    0.482 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[5]                │  -28.6% │     0.344 ms     4%                           ├─ AuthBeforeTokenAcquisition[5]\n    0.412 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[5] │  -29.1% │     0.292 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[5]\n    0.497 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[6]                │  -23.9% │     0.378 ms     4%                           ├─ AuthBeforeTokenAcquisition[6]\n    0.433 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[6] │  -24.0% │     0.329 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[6]\n    0.478 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[7]                │  -27.3% │     0.348 ms     4%                           ├─ AuthBeforeTokenAcquisition[7]\n    0.414 ms     4%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[7] │  -27.9% │     0.299 ms     3%                           ├─ MsalAuthService.AcquireTokenSilentlyAsync[7]\n    0.475 ms     5%  #                        ├─ AuthBeforeTokenAcquisition[8]                │  -29.5% │     0.335 ms     4%                           ├─ AuthBeforeTokenAcquisition[8]\n```\n   \n\n## How it works\n\n- **Discovery** walks recursively for `telemetry/*.jsonl`, so one\ninvocation handles both the CI layout (`<EXT>/<PRODUCT>/telemetry/`) and\na local run's (`<timestamp>/telemetry/`).\n- **Aggregation** groups spans by `TraceId`, rebuilds each tree from\n`SpanId`/`ParentSpanId`, and merges across traces keyed by node path.\nSame-named siblings are indexed by start order (`SessionCreated`,\n`SessionCreated[1]`) so a path is unique within a trace — necessary\nbecause `SessionCreated`, `AuthBeforeTokenAcquisition`, and\n`ActionStart` are event-driven and can each fire more than once under\none parent.\n- **Coverage** shows `(n/N)` on any node that didn't appear in every\ntrace, so a truncated export or a conditional branch is visible rather\nthan silently averaged away.\n- **Publishing** happens in the existing `collect` job, which already\ndownloads every benchmark artifact. It writes the job summary and\nuploads the rendered comment body; `benchmark-comment.yml` gains a\n`trace-comment` job that posts it.\n\n## Fork support\n\nA fork PR's `GITHUB_TOKEN` is read-only, so `collect` can never post\nfrom there. The comment body is therefore rendered once in `collect`\n(under `contents: read`) and only *delivered* by the `workflow_run` job,\nwhich runs in the base repo with a writable token. Fork and same-repo\ncomments are byte-identical because they come from the same rendered\nartifact. No fork-controlled value reaches a shell or an eval.\n\n## Reading the output\n\n`%ROOT` is a node's duration divided by the root's — not a share of\ntotal. Sibling percentages don't sum to 100% wherever spans overlap.\nThis matters under `AzureCLI.ProcessCommand`, where `SessionCreated`,\n`ProcessorProcessAsyncStart`, `AuthBeforeTokenAcquisition`, and\n`MsalAuthService.AcquireTokenSilentlyAsync` are strictly concentric in\nwall-clock terms but recorded as siblings, because those spans are\nparked on a stack in `Engine.Subscriptions.cs` rather than scoped around\nthe work they measure. The aggregator reports the spans faithfully;\ncorrecting the shape is a separate change.\n\n## Scope\n\nNo product behaviour changes. The telemetry producer already emitted\neverything required.\n\n- `.github/scripts/aggregate-benchmark-traces.py` — new\n- `.github/workflows/benchmark.yml` — three steps in the `collect` job\n- `.github/workflows/benchmark-comment.yml` — one new job\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-09-08T11:41:07+10:00",
          "tree_id": "4a7af274e62b53d808a4e0ffb3646448c8739622",
          "url": "https://github.com/Azure/azclips/commit/4096f675af7b68e540d54dfd9fc2db0dc982627e"
        },
        "date": 1788834171796,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 417304908.91,
            "unit": "ns",
            "range": "± 14962226.342250897"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 403862274.14,
            "unit": "ns",
            "range": "± 15170578.870159233"
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
          "id": "3b430de9ed2bcbffaa13cca79a8a7b9b33f4c2b2",
          "message": "[Compute] `az vm start`, `Start-AzVM`: Support start vm commands (#823)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n`az vm start`\n`Start-AzVM`\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\nCLI:\n`--ids`: Dummy serial placeholder implementation 🟠 \n`--name`: ✅ \n`--resource-group`: ✅ \n`--no-wait`: blocked https://github.com/Azure/CLIPS/issues/883\n`--acquire-policy-token`: blocked\nhttps://github.com/Azure/CLIPS/issues/878\n`--change-reference`: blocked https://github.com/Azure/CLIPS/issues/878\n\nPS:\n`-Id`: ✅ \n`-Name`: ✅ \n`-ResourceGroup`: ✅ \n`-NoWait`: blocked https://github.com/Azure/CLIPS/issues/883\n`-Confirm`: ✅ \n`-WhatIf`: ✅ \n`-AsJob`: https://github.com/Azure/CLIPS/issues/876\n\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n`az vm start --name <vm-name> --resource-group <rg-name>`\n`Start-AzVM`\n`dotnet test tests/Scenario.Tests --filter\nFullyQualifiedName~VmLifecycleScenarioTests`\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-08T14:02:50+10:00",
          "tree_id": "90ec508d287c6ee5754150e2aa01992651a1af82",
          "url": "https://github.com/Azure/azclips/commit/3b430de9ed2bcbffaa13cca79a8a7b9b33f4c2b2"
        },
        "date": 1788842306489,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 362574365.06,
            "unit": "ns",
            "range": "± 6301351.422705977"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 348543154.09,
            "unit": "ns",
            "range": "± 5047028.32864172"
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
          "id": "8fa17f0631232c0065b381211e8e689e63a3f802",
          "message": "[Profile] `az profile show`: Support default active selection (#825)\n\nImplements feature: https://github.com/Azure/azclips/issues/630",
          "timestamp": "2026-09-08T13:30:50+08:00",
          "tree_id": "56240ea329ddb59e85ea77084c4bafca5fb7c515",
          "url": "https://github.com/Azure/azclips/commit/8fa17f0631232c0065b381211e8e689e63a3f802"
        },
        "date": 1788847034964,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Config",
            "value": 371873344.12,
            "unit": "ns",
            "range": "± 8538658.112678768"
          },
          {
            "name": "Benchmark.Startup.PsColdStartupBenchmarks.Login",
            "value": 357453187.37,
            "unit": "ns",
            "range": "± 6871924.232428711"
          }
        ]
      }
    ]
  }
}