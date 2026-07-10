window.BENCHMARK_DATA = {
  "lastUpdate": 1783661230255,
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
      }
    ]
  }
}