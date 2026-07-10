window.BENCHMARK_DATA = {
  "lastUpdate": 1783667049884,
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
      }
    ]
  }
}