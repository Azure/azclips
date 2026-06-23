window.BENCHMARK_DATA = {
  "lastUpdate": 1782200548213,
  "repoUrl": "https://github.com/Azure/cirrus",
  "entries": {
    "Cirrus CLI Benchmarks": [
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
          "id": "e2b98529ac515a4bc8ed9f83a3a6281a4cf25880",
          "message": "Implement benchmark workflows and update module configuration scripts (#227)\n\n1. Fix benchmarking failures from previous merges\n2. Add pipeline action to run benchmarks on every PR commit\n3. Add pipeline action to post comment on PRs initiated from fork repo.\n\nIssue for reference: https://github.com/Azure/CLIPS/issues/165\n\nThe following needs to be completed for the both workflows to be fully\nfunctional.\n1. Merge atleast `benchmark-comment.yml` into `main` branch.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-05-15T13:20:43+10:00",
          "tree_id": "f55762d1f006a44a0ca03c8b6182461149985ebf",
          "url": "https://github.com/Azure/cirrus/commit/e2b98529ac515a4bc8ed9f83a3a6281a4cf25880"
        },
        "date": 1778815379123,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7376886,
            "unit": "ns",
            "range": "± 165783.47744774408"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 8852455,
            "unit": "ns",
            "range": "± 213265.6764132475"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 8926206,
            "unit": "ns",
            "range": "± 109240.97327773433"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yabhu@microsoft.com",
            "name": "Yabo Hu",
            "username": "VeryEarly"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "185c465e63ab0a110ae8b8307d5dc8fe692d0a88",
          "message": "Fix build script to update path in artifacts instead of src  (#240)\n\nfix GHA bug:\n\nhttps://github.com/Azure/cirrus/actions/runs/25958312170/job/76308977496?pr=239\n- build time patch psd1 should target artifacts instead of src\n- test case should also target artifacts/debug instead of src",
          "timestamp": "2026-05-16T18:36:40+08:00",
          "tree_id": "73aaac3dc4c690ed52d3b4a65ea24c3189c03a02",
          "url": "https://github.com/Azure/cirrus/commit/185c465e63ab0a110ae8b8307d5dc8fe692d0a88"
        },
        "date": 1778927921229,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5479765.5,
            "unit": "ns",
            "range": "± 198045.43600732973"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 6055340.25,
            "unit": "ns",
            "range": "± 282680.9011003691"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5588866.75,
            "unit": "ns",
            "range": "± 57814.84356907546"
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
          "id": "17e5705c9453b08708a17ed8418ceff151e029b6",
          "message": "remove threshold failure limitation (#243)",
          "timestamp": "2026-05-16T10:50:59Z",
          "tree_id": "fb8d1dc1f6880881b200564a1b7bf7c03450a5de",
          "url": "https://github.com/Azure/cirrus/commit/17e5705c9453b08708a17ed8418ceff151e029b6"
        },
        "date": 1778928761976,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7584034.5,
            "unit": "ns",
            "range": "± 200991.24915445116"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 8859282.2,
            "unit": "ns",
            "range": "± 487100.4352961512"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 9490589.2,
            "unit": "ns",
            "range": "± 709189.3582984025"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yabhu@microsoft.com",
            "name": "Yabo Hu",
            "username": "VeryEarly"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a6ba6905e81bb01e95050295e1857d1ad1e063be",
          "message": "Complete create/get/delete for VM (#239)",
          "timestamp": "2026-05-16T20:59:37+10:00",
          "tree_id": "80d39316ba2a2a50fc0a4d3734b915996daf181d",
          "url": "https://github.com/Azure/cirrus/commit/a6ba6905e81bb01e95050295e1857d1ad1e063be"
        },
        "date": 1778929276146,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7534004.5,
            "unit": "ns",
            "range": "± 108397.3048450314"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 9179879.4,
            "unit": "ns",
            "range": "± 407633.6717762653"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 8522128.75,
            "unit": "ns",
            "range": "± 64237.06229986445"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yabhu@microsoft.com",
            "name": "Yabo Hu",
            "username": "VeryEarly"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "c606d4ea85b60eb0499811faa5b1310422fd9c5d",
          "message": "Redesign Engine+Session APIs (#246)",
          "timestamp": "2026-05-27T23:30:54+08:00",
          "tree_id": "f61ec60b453d7d6d52eafc8242eafa472422a201",
          "url": "https://github.com/Azure/cirrus/commit/c606d4ea85b60eb0499811faa5b1310422fd9c5d"
        },
        "date": 1779896032555,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7791437.5,
            "unit": "ns",
            "range": "± 325138.9059309885"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 9323050,
            "unit": "ns",
            "range": "± 102207.23307737733"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 8645959.4,
            "unit": "ns",
            "range": "± 545458.3199634047"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yabhu@microsoft.com",
            "name": "Yabo Hu",
            "username": "VeryEarly"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "f9e6bb4440b5e2aa3ac22e461d1a899796098dcb",
          "message": "Extension development guide for private preview (#256)",
          "timestamp": "2026-05-29T07:32:54+08:00",
          "tree_id": "2b11a73f2dc2b969186360fd4c6413ecf6af34e3",
          "url": "https://github.com/Azure/cirrus/commit/f9e6bb4440b5e2aa3ac22e461d1a899796098dcb"
        },
        "date": 1780011312623,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7240807.25,
            "unit": "ns",
            "range": "± 109695.5838897659"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 8607170.5,
            "unit": "ns",
            "range": "± 108001.24090259333"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 8689064.5,
            "unit": "ns",
            "range": "± 170811.7451260305"
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
          "id": "cfd10050458188c9bb8a3768c6033f40dcecbf35",
          "message": "feat(agent): port to Copilot SDK with tool-driven, host-agnostic agent (#241)\n\n## What\n\nPort the cirrus agent off the legacy MAF wrapper onto the Copilot SDK\nand\ngive it a real, tool-driven view of the system so it can handle most\nreal-world Azure scenarios end-to-end. Triggered from a classic CLI\nfailure (`Core.Cli`), the agent now spawns `Core.Tui.exe --agent-mode`\nand drives a fully streamed terminal session.\n\n## Highlights\n\n### Copilot SDK as the engine\nThe MAF-era `CloudAgent` / `AgentEngine` is gone. The new `ChatAgent`\nis slim Copilot SDK plumbing — it owns the session, the tool wiring,\nand the lifecycle, and leaves chat loops to whatever host hosts it.\n\n### Built-in SDK tools, out of the box\nBy leaving `AvailableTools = null` the agent gets the SDK's full\ntoolbox for free:\n\n- `bash` — shell execution (routed through cirrus's approval prompt)\n- `powershell` / `read_powershell` / `stop_powershell` — long-lived PS\nsessions\n- `view` / `edit` / `grep` / `glob` — file inspection and editing\n- `web_fetch` / `web_search` — look up Azure docs and unknown error\ncodes\n- `todo` — agent-side task tracking\n\n### Cirrus-specific tools\nOn top of the SDK toolbox we ship purpose-built tools for cirrus:\n\n- **`find_resources` / `get_resource_tree` / `get_resource_details`** —\n  read-only Azure resource graph backed by an in-process cache so the\n  agent can navigate subscriptions without burning ARM calls per turn.\n- **`list_commands` / `get_command`** — the agent discovers cirrus\n  commands and resolves their required parameters by calling tools\n  (backed by `HelpIndex.json` + `HelpManifest.json`) instead of reading\n  a hand-maintained list in the prompt.\n- **`find_available_locations`** — SkuNotAvailable self-recovery. When\n  ARM rejects `vm create` with `SkuNotAvailable`, the agent calls this\n  read-only ARM tool once (parallel `resourceSkus` lookups across 8\n  default candidate regions) and retries with `--location <first\n  available>`. Each entry distinguishes Location-block / Zone-block /\n  SkuNotListedInRegion / HTTP error so the agent can pick a region\n  with real zones instead of guessing. Auto-approved via the `find_`\n  read-only prefix — no extra user prompt.\n\n### Environment probes\n`EnvironmentProber` baselines OS / shell / azv2 / signed-in identity\nonce per session and feeds the snapshot into the system prompt, so the\nagent starts each conversation already knowing where it is and who\nit's acting as — no warm-up tool calls required.\n\n### Host-agnostic Hosting layer\nA small `Core.Agent.Hosting` package any host (today: classic\n`--agent-mode` console) can drop in:\n\n- `IAgentRenderer` + `ConsoleAgentRenderer` — render styled segments\n- `AgentEventStyle` — pure event → segment mapping\n- `AgentColorRole` — semantic colors (`Default`, `Muted`, `Warning`,\n  `Error`) so no `ConsoleColor` leaks into `Core.Agent` and future\n  TUI / web hosts can map roles to their own palettes\n- `SpinnerService` — animated 'thinking' indicator with Pause / Resume\n- `ConsoleApprovalPrompt` — single-reader y/n prompt that decouples\n  the SDK callback thread from stdin via a channel\n- `AbortKeyWatcher` — Ctrl-C aware cancellation surface\n\n### Approval prompt: 2-state with feedback\n`ConsoleApprovalPrompt.PumpOneAsync` now returns a typed\n`ApprovalOutcome(Handled, Approved, FeedbackPrompt)` instead of a\nplain bool. The prompt reads `[y]es / [n]o, tell me what to do\ndifferently:` and treats anything that isn't `y`/`yes` as a\nrejection — empty / `n` / `no` is a plain No, any other free text\nbecomes a redirect: the SDK tool call is rejected, the current turn\nis aborted, and the typed text is replayed as the next chat-loop\nprompt. So when the user types \"用 az 帮我删除 rg\" at a y/n prompt,\nthey get the new behavior in one keystroke instead of having to say\nNo and then re-type their instruction. Mirrors Claude Code's 2-option\n+ placeholder pattern; the `Outcome` record is shared with future\nTUI hosts so the host layer never has to know whether the answer\ncame from a radio button or a `Console.ReadLine` parse.\n\n### Streaming-first UX via `AgentEvent`\nEverything the agent does is projected onto a typed event stream\n(`LlmFirstToken`, `LlmTurnCompleted`, `ToolStarted` / `ToolCompleted` /\n`ToolFailed`, `SubAgentInvoke*`, `SessionErrored`, `HostLogEmitted`).\nHosts subscribe and render — TTFT, token counters, per-tool latency,\nsub-agent calls and errors all show up live, in a uniform shape, with\nno host-specific format strings.\n\n### Misc\n- `fix(resources)`: `GetResourceGroupProcessor` now calls\n  `EnsureSuccessStatusCode`, so `azv2 group show --name <missing>`\n  surfaces the 404 instead of silently returning a default record —\n  which is what makes the classic → agent fallback fire at all.\n\n## Out of scope\n\n- In-process TUI hosting of `ChatAgent` (today the trigger is still\n  classic → spawn `Core.Tui --agent-mode`).\n- Replacing `HelpIndex.json` / `HelpManifest.json` with a live\n  `IExtensionHelpProvider` lookup — waiting on #226 to merge.\n\n---------\n\nCo-authored-by: Claude Opus 4.7 <noreply@anthropic.com>",
          "timestamp": "2026-06-02T16:11:40+08:00",
          "tree_id": "8e43b39bbf7173758b37dde28f15f48b521f54eb",
          "url": "https://github.com/Azure/cirrus/commit/cfd10050458188c9bb8a3768c6033f40dcecbf35"
        },
        "date": 1780388005233,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 15191627,
            "unit": "ns",
            "range": "± 612028.6133847557"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 9999090.25,
            "unit": "ns",
            "range": "± 130496.18617511395"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 8391606.25,
            "unit": "ns",
            "range": "± 233040.16899149254"
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
          "id": "330bf977b4eb9ce72bafb59d500a1bcbadc719a5",
          "message": "[Telemetry] Move telemetry orchestration to host layer (#255)\n\nMove telemetry orchestration to be owned by the composition root of each\nrespective client. Instead of the `TelemetryService` understanding\ninternally the telemetry orchestration requirements of each respective\nclient.\n\nThe actual activity tagging of commands will be in a separate pull\nrequest.",
          "timestamp": "2026-06-02T21:57:46+08:00",
          "tree_id": "62b83f1e9c1dfa7272d918386d694aab2e143a8c",
          "url": "https://github.com/Azure/cirrus/commit/330bf977b4eb9ce72bafb59d500a1bcbadc719a5"
        },
        "date": 1780408790346,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7295913.25,
            "unit": "ns",
            "range": "± 364257.04566818883"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 8197932.2,
            "unit": "ns",
            "range": "± 295760.485375751"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 7770078.8,
            "unit": "ns",
            "range": "± 135362.1609727028"
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
          "id": "9fd0dfb8a8b59fb3e96e2e74897e4fbad9c17eac",
          "message": "[Telemetry] Fix mismatched activity hierarchy and missing service name (#259)\n\nThe PR fixes the following telemetry issues.\n\n1. `cloud_rolename` resolves to `unknown` when running PS cmdlets.\n<img width=\"2423\" height=\"752\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/0abca30d-d2d7-4e34-8486-0ccf6b1f52de\"\n/>\n\n2. Clients using `FileTelemetrySender` having a flat telemetry hierarchy\n**Before:**\n\n<img width=\"2439\" height=\"973\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/10ee760f-bee5-4e44-ae98-f6fa0b08b2dc\"\n/>\n\n**After:**\n\n<img width=\"2403\" height=\"891\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/97dad940-9706-44e3-935a-8cbd4be80bcd\"\n/>\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-04T10:34:04+10:00",
          "tree_id": "2e81bb4bb991e27eb82d898f7eff9eb9a04acd72",
          "url": "https://github.com/Azure/cirrus/commit/9fd0dfb8a8b59fb3e96e2e74897e4fbad9c17eac"
        },
        "date": 1780533374848,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 7701304.5,
            "unit": "ns",
            "range": "± 283113.50145421654"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 9104475,
            "unit": "ns",
            "range": "± 248030.1393026662"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 8269912.4,
            "unit": "ns",
            "range": "± 360431.3939424533"
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
          "id": "6a36352f49463eb100e06a01bcbf3a382ff8ff5e",
          "message": "Refactor Resources processors to 2-layer base with paginated list support (#258)\n\n## What\n\nRefactors the Resources extension processor base into a clean two-layer\ndesign and adds paginated list support.\n\n- **Two-layer `ResourcesProcessorBase`**: a non-generic transport base +\na generic `ResourcesProcessorBase<TResponse>` (subscription resolution +\ndeserialize/write pipeline). Runtime-context fields are `protected` (not\n`public`). LRO polling is intentionally deferred to the deployment PR,\nwhere it has a real consumer.\n- **Inlined pagination in `ListResourceGroupProcessor`**: unwraps the\nARM list envelope `{ value, nextLink }` and follows `nextLink` across\n**all** pages, with null/empty/invalid-JSON, cyclic-`nextLink`, and\ntruncated-page guards. (Earlier revisions introduced a generic\n`ResourcesListProcessorBase` + `IPagedResult`; these were removed in\nfavor of inlining, since list is the only consumer today.)\n- **`ListResourceGroupProcessor`** now fixes the existing bug where only\nthe first page was returned.\n- **Bounded error messages**: a failed-to-deserialize response body is\ntruncated in the exception message; the full body is preserved\nout-of-band on the `ResponseInvalidJson` event.\n\n## Why\nRemoves duplicated transport/deserialize logic across processors and\nfixes the list `nextLink` gap.\n\n## Surface changes\nCirrus is pre-release, so these intentional changes aren't treated as\nbreaking:\n- `Get-ResourceGroup` / `group show`: `Name` is now **required**.\n- `New-ResourceGroup` / `group create`: the unused `ResourceId`\nparameter was **removed** (no corresponding field in the ARM\ncreate/update API).\n\n## Testing\n- `dotnet build cirrus.slnx` — 0 warnings, 0 errors\n- `Cli.Tests` — 52 passed, 4 skipped, 0 failed\n- Live-verified `azv2 group list` / `show` / `create` against Azure\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-04T13:19:04+08:00",
          "tree_id": "87371e913b6dde300662703b49b33c6a71b17e11",
          "url": "https://github.com/Azure/cirrus/commit/6a36352f49463eb100e06a01bcbf3a382ff8ff5e"
        },
        "date": 1780550506541,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 12151760.25,
            "unit": "ns",
            "range": "± 252750.6545172864"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 12173614.8,
            "unit": "ns",
            "range": "± 177678.65435302013"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 11957321.8,
            "unit": "ns",
            "range": "± 425834.16553829965"
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
          "id": "d9e6d52c3d0badebcfb4e7ee6c2833695eac5fc7",
          "message": "{Utils} Implementation of `help` system (#226)\n\n## Summary\n\nEnd-to-end help system for both CLI and PowerShell surfaces. Authored\nmarkdown drives `azv2 --help`, `Get-Help <Cmdlet>`, a JSON help index\nfor tooling, and CI-enforced validation that catches stale examples on\nevery build.\n\n84 commits · 99 files changed · +11,429 / −164 lines.\n\n## Motivation\n\nBefore this PR there was no first-class help story:\n\n- CLI help was either missing or assembled ad-hoc from `[ProcessorInfo]`\nattributes.\n- PowerShell cmdlets had no MAML, so `Get-Help <Cmdlet> -Full` returned\nnothing useful.\n- Examples lived in scattered docs and drifted silently as flags were\nrenamed or commands moved.\n\nWe needed one authoring contract, two surfaces, and CI that hard-fails\nwhen an example references a flag that no longer exists.\n\n## What's delivered\n\n### Authoring corpus\n\n- **CLI** — `src/CLI/Help/<extension>/<command-key>.md` (capital H,\ndasherized keys, `index.md` for root).\n- **PowerShell** — `src/PS/<Module>/help/<Verb-Noun>.md`, current\nPlatyPS schema (`2024-05-01`).\n- Frontmatter-driven (`summary`, `status`, `schema`, `online version`,\n`relatedLinks`); fence language tag (`bash` vs `powershell`) is the\nsurface discriminator, suffix in the H3 is cosmetic.\n- Initial corpus written for `compute`, `config`, `extension`, `login`,\n`resources` (CLI) and `Compute`, `Config`, `Login`, `Resources`\n(PowerShell modules).\n\n### CLI runtime (`src/CLI/Help/`)\n\n- `MarkdownCommandHelpProvider` — filesystem read of\n`<AppContext.BaseDirectory>/help/<ext>/<key>.md` with a dev-loop\nfallback that walks up to `cirrus.slnx` and reads `src/CLI/Help/`\ndirectly. Lenient parser; never throws on malformed input.\n- `FrontmatterParser` — hand-rolled (~150 lines, zero NuGet surface).\n- `ExtensionIndexHelpAdapter` — bridges `IExtensionIndex` and the\nmarkdown corpus, filters examples to `!IsPowerShell` for the CLI\nsurface.\n- `RuntimeHelpRenderer` — tree-aware help (`azv2 --help`, `azv2 vm\n--help`, `azv2 vm show --help` each produce meaningfully different\noutput). Cold-path: no reflection, no processor instantiation.\n- `HelpArtifactWriter` + `ExtensionHelpManifestBuilder` — produce\n`artifacts/help/<ext>/HelpManifest.json` and\n`artifacts/help/HelpIndex.json` for external tooling.\n- Runtime help XML for extensions: see `RuntimeHelpContracts` and\n`RuntimeExtensionHelpException`.\n- `Core.Cli.csproj` copies `Help\\**\\*.md` as content to the build\noutput.\n\n### PowerShell pipeline (`build/`)\n\n- `build/New-PSHelpMarkdown.ps1` — PlatyPS scaffolder. Imports each\nmodule in an isolated child pwsh, runs `New-MarkdownCommandHelp`,\npost-processes with `Convert-PlatyPSPlaceholders` to:\n  - replace `{{ Fill in ... }}` with `TODO -` markers,\n- inject a fenced `powershell` example using each cmdlet's required\nparameters when PlatyPS produced a fenceless or empty body,\n- dedupe `[Online Version]()` entries under `## RELATED LINKS` (PlatyPS\n1.0.1 emits one per cmdlet in the module).\n- `build/Install-BuildDependencies.ps1` — pins\n`Microsoft.PowerShell.PlatyPS` to `1.0.1`.\n- `build/build.ps1 -GenerateHelpArtifacts` — orchestrates: build →\nvalidate → MAML → copy `dll-Help.xml` next to the manifest. Handles both\n`RootModule` and `NestedModules` shapes.\n\n### Validation tool (`tools/Help/Cirrus.Help.Tool/`)\n\nA new `dotnet run` tool that loads built extension DLLs and validates\nthe corpus. Errors are aggregated, grouped by `ProblemId`, and CI exits\nnon-zero if any error fires (warnings alone still exit 0).\n\n| Validator | Rules |\n|---|---|\n| `MarkdownStructureValidator` | Frontmatter present + valid; `summary`\n/ `schema` required; `status` is a known enum value; balanced fences; at\nleast one example; **fence allowed to start one or more blank lines\nafter the `### ` heading**. |\n| `HelpCompletenessValidator` | Every routed command in\n`IExtensionIndex` has a markdown file; orphan markdown warns. |\n| `ExampleSyntaxValidator` | CLI `azv2 …` lines tokenize, group\nresolves, command resolves, `--flag` exists, value type-checks against\n`ParameterMetadata.Type` / `AllowedValues`. |\n| `PowerShellExampleSyntaxValidator` | PSL1–PSL8 (see below). |\n\nPSL rules:\n\n- **PSL1** UnknownCmdlet — Verb-Noun isn't in any Cirrus cmdlet\nassembly. No early-out for non-Verb-Noun shapes — `azv2`, `dotnet`, etc.\nnow fire PSL1.\n- **PSL2** UnknownParameter — `-Foo` not declared and not an `[Alias]`.\n- **PSL3** MissingRequiredParameter — `Mandatory=true` in\n`AllParameterSets`. Splatted invocations skip the check.\n- **PSL4** DuplicateParameter — same param (alias-resolved) bound twice.\n- **PSL5** UnassignedVariable — `$x` read but never assigned (excludes\ndrive-qualified, splatted, automatic, foreach loop, `param()`, and\n`-OutVariable`/`-PipelineVariable`/etc. targets).\n- **PSL6** InvalidParameterValue — literal value bound to `Guid` or enum\ndoesn't parse. Placeholders pass through.\n- **PSL7** ExampleMustInvokeDocumentedCmdlet — every fence must invoke\nthe cmdlet whose name matches the filename.\n- **PSL8** NoExamples — cmdlet help file has no fenced PowerShell block\nunder `## EXAMPLES`.\n\nFence scoping: only `## EXAMPLES`, only `powershell`/`pwsh`/`ps`/empty\nlanguage tags. PlatyPS pseudocode in `## SYNTAX` / `## PARAMETERS` is\ndeliberately ignored.\n\n`Scaffolder` writes per-command stubs from `IExtensionIndex`\n(`--scaffold <extension>`), using `HelpCommandKey.ToHelpFileName` so\nmulti-word keys become `config-get.md` and an empty key becomes\n`index.md`.\n\n### Tests\n\n- New project `tools/Help/Cirrus.Help.Tool.Tests/` — 119 tests covering\nall validators, scaffolder, frontmatter parser, and command-key\nnormalization.\n- New runtime tests in `tests/Cli.Tests/` —\n`MarkdownCommandHelpProviderTests`, `RuntimeHelpRendererTests`,\n`HelpArtifactWriterTests`, `ExtensionHelpManifestBuilderTests`, plus\ngolden fixtures under `tests/Cli.Tests/Fixtures/Help/` (CLI runtime help\nfor the Login extension, manifest JSON, group/command/root help text).\n- Existing CLI test count: 92 passing.\n\n### Documentation\n\n- New design spec: `docs/spec/help-system.md` — architecture, two-corpus\nrationale, validator pipeline, design philosophy.\n- New author guide: `docs/howto/help.md` — quick-starts, file format,\nvalidation rules, troubleshooting per ProblemId.\n- `docs/spec/ARCHITECTURE.md`, `README.md` updated to match.\n\n### CI\n\n- `.github/workflows/build-and-test.yml` split into staged jobs; help\nvalidation runs as a dedicated step that fails the build on any\nstructural / completeness / example-syntax issue.\n\n## Notable design decisions\n\n- **Two corpora, not one.** CLI markdown lives separately from\nPowerShell markdown. The original \"single source\" goal was abandoned\nbecause PlatyPS's authoring shape (per-parameter YAML, per-set syntax\nheadings) doesn't fit the shorter CLI help shape, and forcing one would\nhave required a translation layer with its own bugs. Both corpora are\nvalidated by the same tool, share the frontmatter parser, and share\nvalidator infrastructure.\n- **Filesystem at runtime, not embedded resources.** Earlier iterations\nembedded markdown into extension DLLs. We switched to filesystem reads\n(CLI host content + dev-loop repo fallback) to keep extensions free of\nhelp payload and to enable edit-and-reload on the inner loop.\n- **Hard-fail validators, no allowlist.** Every defect is a CI error. If\nan example needs a placeholder, use the documented placeholder forms\n(`<rg>`, `$var`, all-zero GUID); don't add an exception file.\n- **Static analysis only.** PowerShell rules use\n`System.Management.Automation.Language.Parser.ParseInput` AST walks — no\nrunspace, no script execution.\n\n## Verification\n\n```pwsh\ndotnet build cirrus.slnx -nologo\ndotnet test tools/Help/Cirrus.Help.Tool.Tests --no-build  # 119 passed\ndotnet test tests/Cli.Tests --no-build                    # 92 passed, 4 skipped\ndotnet run --project tools/Help/Cirrus.Help.Tool --no-build # \"Help validation: no issues.\"\n```\n\nEnd-to-end smoke tests:\n\n```pwsh\n.\\build\\build.ps1 -GenerateHelpArtifacts -Configuration Debug\ndotnet .\\artifacts\\Debug\\Core.Cli\\azv2.dll vm --help\ndotnet .\\artifacts\\Debug\\Core.Cli\\azv2.dll vm show --help\nImport-Module .\\artifacts\\Debug\\PS\\Compute\\Compute.psd1 -Force\nGet-Help Get-VM -Full\nGet-Help Connect-CirrusAccount -Examples\n```\n\n## Out of scope\n\n- Help content quality (most files are TODO-stubbed; this PR delivers\nthe system, not the prose).\n- Localization (`Locale: en-US` carried as PlatyPS metadata; no\nsecond-locale pipeline yet).\n- Online version URLs (`HelpUri` attributes on cmdlets, MSLearn-linked\nexamples).\n- The CLI counterpart of PSL3/PSL4 (missing-required, duplicate-flag) on\n`ExampleSyntaxValidator` — still L1/L2/L3 only; tracked as future work.\n- A change in `ExtensionDispatcher.cs` was flagged in review and\nintentionally deferred to a separate PR — not a help-system concern.\n\n## How to review\n\n- **Spec first**: `docs/spec/help-system.md` — covers the whole system\nin one read.\n- **Author flow**: `docs/howto/help.md` — what a contributor needs to\ndo.\n- **Runtime**: `src/CLI/Help/MarkdownCommandHelpProvider.cs`,\n`src/CLI/Help/RuntimeHelpRenderer.cs`.\n- **Validators**: `tools/Help/Cirrus.Help.Tool/Validation/` — start with\n`ProblemIds.cs`, then each `*Validator.cs`.\n- **Build**: `build/build.ps1`, `build/New-PSHelpMarkdown.ps1`.",
          "timestamp": "2026-06-09T16:23:10+10:00",
          "tree_id": "478c34ea04c007ed60b76e89c721d37a444726fe",
          "url": "https://github.com/Azure/cirrus/commit/d9e6d52c3d0badebcfb4e7ee6c2833695eac5fc7"
        },
        "date": 1780986350236,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 11966916,
            "unit": "ns",
            "range": "± 100939.46896366488"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 12067392.6,
            "unit": "ns",
            "range": "± 355509.78292699624"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 11752058.6,
            "unit": "ns",
            "range": "± 159565.8322755846"
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
          "id": "dbb082fbbddd49fb539da7f2113b552b70260eb8",
          "message": "fix(runtime): make HTTP trace logging tolerate non-JSON response bodies (#266)\n\n## Summary\n\n`RuntimeExtensions.FormatString` previously called\n`JsonNode.Parse(content)!.ToString()` unconditionally on every traced\nHTTP request/response body. Because the engine's default tracing\nsubscribers (`Engine.Subscriptions.cs`) call `GetLog()` →\n`FormatString()`, and `RuntimeEventBus.Publish` does not swallow\nsubscriber exceptions, any non-JSON body (text/plain, application/xml,\nHTML error pages from gateways/proxies/WAF, octet-stream, etc.) threw\n`JsonException` and failed the entire command before the response could\nbe surfaced. This is latent for all commands and hit routinely by the\nnew `rest` passthrough.\n\n## Fix\n\nJSON pretty-printing is now best-effort with a raw-text fallback\n(try/catch); the tracer tolerates any body and never throws. Valid-JSON\noutput is byte-identical to before; the 10KB stream truncation marker is\npreserved. Scoped to `FormatString` only — the event bus, subscriptions,\nand rest extension are intentionally untouched.\n\n## Precedent\n\nMatches Azure PowerShell's `Invoke-AzRestMethod` / `PSHttpResponse`\nbehavior: parse JSON opportunistically, fall back to raw text, and never\nlet logging break a command.\n\n## Tests\n\n11 new `RuntimeExtensionsTests` cases in `Cli.Tests` (valid JSON\nunchanged; non-JSON text/xml/html returns raw and does not throw;\nnull/empty safe; >10KB truncation; `GetLog` over text/plain + text/html\ncompletes without throwing). Build 0/0; `Cli.Tests` 106 passed / 0\nfailed / 4 skipped.\n\n## Related\n\nThis addresses a Copilot review comment raised on PR #260 (the `rest`\npassthrough PR); that command is the most frequent trigger of this\nengine-level defect. Link: #260.\n\n## Follow-ups (non-blocking, from review)\n\nOptional defense-in-depth: make `RuntimeEventBus.Publish` resilient to\nsubscriber exceptions so no future tracer/subscriber can fail a command.",
          "timestamp": "2026-06-10T12:11:56+08:00",
          "tree_id": "58cc40eea3d91028d63be3b10518788a84addd97",
          "url": "https://github.com/Azure/cirrus/commit/dbb082fbbddd49fb539da7f2113b552b70260eb8"
        },
        "date": 1781064850487,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 16287876.6,
            "unit": "ns",
            "range": "± 4771408.023183964"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 12855929.8,
            "unit": "ns",
            "range": "± 375110.39193242835"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 12073433.75,
            "unit": "ns",
            "range": "± 123656.5308461978"
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
          "id": "3d9d7a38fb3062b2cc266a60f7e446c903992630",
          "message": "{Packaging} Setting the initial version to 0.1.0 (#265)\n\n## Summary\n- Set CLI assembly/package metadata to version 0.1.0\n- Set extension package versions to 0.1.0\n- Align PowerShell module and dependency versions to 0.1.0\n\n## Validation\n- Built changed CLI and extension projects\n- Ran `dotnet test tests\\Cli.Tests\\Cli.Tests.csproj --no-build --logger\n\"console;verbosity=minimal\" -p:UseTerminalLogger=false`\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-10T14:52:20+08:00",
          "tree_id": "4658cf54c399ed9f3d5c4a054036adf0b133e834",
          "url": "https://github.com/Azure/cirrus/commit/3d9d7a38fb3062b2cc266a60f7e446c903992630"
        },
        "date": 1781074450431,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 12028843,
            "unit": "ns",
            "range": "± 254569.27672966875"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 11951116.75,
            "unit": "ns",
            "range": "± 190146.7106129282"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 11652367,
            "unit": "ns",
            "range": "± 266538.726125617"
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
          "id": "38fe71aff5516bfd08e3c71ce862cb184f5ae2be",
          "message": "feat(resources): add resource group deployment create (ARM + Bicep, LRO wait) (#262)\n\n## Summary\nAdds the `New-ResourceGroupDeployment` PowerShell cmdlet and the\n`deployment group create` CLI command to the Resources extension,\ndeploying ARM templates and Bicep files to a resource group via the ARM\ndeployments API (`2025-04-01`).**This is PR 1 of 2** splitting the\noriginal #261 for easier review:\n- **PR 1 (this PR):** shared deployment foundation + `create`.\n- **PR 2 (follows after this merges):** `deployment group what-if`.\n## What's included\n- PowerShell: `New-ResourceGroupDeployment`; \n- CLI: `deployment group create` (new `Deployment` command group).\n- ARM template + Bicep/`.bicepparam` support (inline + linked\ntemplates/parameters, query-string handling).\n- Always waits for the deployment LRO to reach a terminal\n`provisioningState` (matching `az deployment group create`); a\nno-wait/async option was intentionally deferred to a follow-up per\nreviewer feedback.\n- Shared base classes (`ResourcesProcessorBase` LRO machinery,\n`DeploymentRequestProcessorBase`) and models reused by PR 2.- Manual\ntest scripts (CLI + PS) and Bicep fixtures under `tests/Scripts`.\n\n## Test Evidence (2026-06-08)\n\n**Environment** — subscription `282bb33f-067d-4179-a7a4-b833c13af735`,\nresource group `cirrus-test-group-1`, region `australiaeast`; branch\n`yangan/deployment-group-create-foundation`; head commit `602e97c8`;\ndate 2026-06-08 (UTC). Live runs were executed against the shared test\nsubscription using cached MSAL auth (verified valid via a read-only\n`group list`).\n\n### Build\n`dotnet build cirrus.slnx -c Debug` — **Build succeeded, 0 Warning(s) /\n0 Error(s)** (exit 0).\n\n### Offline reflection test\n(`tests/Scripts/PS/DeploymentParametersQueryString.ps1`)\nLoads the built `Microsoft.Azure.Cirrus.Extensions.Resources.dll` and\nasserts QueryString / ParametersLink / TemplateLink URL-building via\nreflection. **14/14 assertions PASS**, exit code **0**.\n\n```\n=== ParametersLink.Uri embedding ===            8/8 PASS\n=== TemplateLink path UNCHANGED ===             2/2 PASS\n=== TemplateLink.QueryString normalization ===  4/4 PASS\n\nAll assertions passed.\n```\n\n### Live CLI create — `deployment group create`\n(`tests/Scripts/CLI/DeploymentCreate.sh`)\n\n| # | Case | Result |\n|---|------|--------|\n| 1 | quickstart `--template-uri`, `--mode Incremental`\n(`cirrus-test-deployment-1`) | ✅ `provisioningState: Succeeded` |\n| 2 | Bicep `--template-file` + `.bicepparam` (`cirrus-test-bicep-…`) |\n✅ `provisioningState: Succeeded` |\n| 3 | **neg** malformed `.bicep` | ✅ correctly rejected — bicep compile\nerror |\n| 4 | **neg** `--mode Foo` | ✅ correctly rejected — invalid enum |\n| 5 | inline `--parameter` overlay + `--validation-level Provider`\n(`cirrus-test-inline-…`) | ✅ `provisioningState: Succeeded` |\n| 6 | standalone `--validation-level Provider` (`cirrus-test-vlevel-…`)\n| ✅ `provisioningState: Succeeded` |\n| 7 | `--rollback-on-error` (`cirrus-test-rollback-…`) | ✅\n`provisioningState: Succeeded` |\n| 8 | **neg** `--parameters-uri` + inline `--parameter` conflict | ✅\ncorrectly rejected |\n| 9 | LRO wait (always-wait, `cirrus-test-wait-…`) | ✅ polled to\nterminal `provisioningState: Succeeded` |\n\nNegative-case rejection messages:\n\n```\n[ERROR] Bicep compilation failed (exit code 1):\n        ...BCP007: This declaration type is not recognized...\n\n[ERROR] Failed to parse arguments for command 'group create': Invalid arguments:\n        Parameter 'Mode' value 'Foo' is not allowed. Allowed values: Incremental, Complete\n\n[ERROR] Inline parameters cannot be combined with a parameters URI; ARM does not allow\n        inline parameters together with a parameters link. Provide a parameters file\n        instead, or remove the parameters URI.\n```\n\n### Live PS create — `New-ResourceGroupDeployment`\n(`tests/Scripts/PS/DeploymentCreate.ps1`)\n\n| # | Case | Result |\n|---|------|--------|\n| 1 | quickstart `-TemplateUri`, `-Mode Incremental`\n(`cirrus-test-deployment-1`) | ✅ `provisioningState: Succeeded` |\n| 2 | Bicep `-TemplateFile` + `.bicepparam` (`cirrus-test-bicep-…`) | ✅\n`provisioningState: Succeeded` |\n| 3 | **neg** malformed `.bicep` | ✅ correctly rejected — bicep compile\nerror (BCP007) |\n| 4 | **neg** `-Mode Foo` | ✅ correctly rejected — invalid enum |\n| 5 | inline `-Parameter` overlay + `-ValidationLevel Provider`\n(`cirrus-test-inline-…`) | ✅ `provisioningState: Succeeded` |\n| 6 | standalone `-ValidationLevel Provider` (`cirrus-test-vlevel-…`) |\n✅ `provisioningState: Succeeded` |\n| 7 | `-RollbackOnError` (`cirrus-test-rollback-…`) | ✅\n`provisioningState: Succeeded` |\n| 8 | **neg** `-ParametersUri` + inline `-Parameter` conflict | ✅\ncorrectly rejected |\n| 9 | LRO wait (always-wait, `cirrus-test-wait-…`) | ✅ polled to\nterminal `provisioningState: Succeeded` |\n\nNegative-case rejection messages:\n\n```\nBicep compilation failed (exit code 1):\n  ...BCP007: This declaration type is not recognized...\n\nMode must be either 'Incremental' or 'Complete'.\n\nInline parameters cannot be combined with a parameters URI; ARM does not\nallow inline parameters together with a parameters link. Provide a parameters file instead...\n```\n\n### Verdict\n✅ Build clean; offline assertions 14/14; **all happy-path deployments\nreached `Succeeded`** (the command always waits for the deployment LRO\nto reach a terminal `provisioningState`), and **every negative case was\ncorrectly rejected** with a clear message on both the CLI and PowerShell\nsurfaces. No unexpected failures.\n\n\n\n## Known limitation: CLI examples use `group group create` (deferred\nfollow-up)\n\nThe CLI help examples in `src/CLI/Help/Docs/resources/group-create.md`\nare written as `azv2 group group create ...` rather than the nicer\naz-parity form `azv2 deployment group create ...`. This is a temporary\naccommodation for the help-validation tool, not a runtime limitation.\n\n- **At runtime, `azv2 deployment group create ...` already works.**\n`src/CLI/Parser/CommandGroupIndex.cs` maps both the `Group` and\n`Deployment` command groups to the Resources extension, so the command\ndispatches correctly either way.\n- **The help tool only accepts one group token per extension.**\n`tools/Help/Cirrus.Help.Tool/Program.cs` seeds its validation map from a\nhardcoded `cliGroupByExtension` dictionary that is `extension -> single\ngroup string` (Resources is registered only as `\"group\"`).\n`ExampleSyntaxValidator` then reads `tokens[1]` of each example and\nlooks it up in that map; an `azv2 deployment ...` example would miss and\nraise `CLI_L1_UnknownGroup`, turning CI red.\n- **Why we didn't fix it here:** making the help tool accept\n`deployment` as a second group for Resources requires changing\n`cliGroupByExtension` from `string` to `string[]` (or exposing the CLI\ngroups off `IExtensionIndex`). That file lives on `main`, outside this\nPR's branch, so it's tracked as a separate follow-up. Once that lands,\nthe examples in `group-create.md` can be switched to the `azv2\ndeployment group create` form.\n\nThis is the subject of the three open `group-create.md` review threads,\nintentionally left unresolved to keep the deferred decision visible.",
          "timestamp": "2026-06-10T22:45:11+08:00",
          "tree_id": "8bad0b7c24f17bf17e96a79f45e02fef0477b8b3",
          "url": "https://github.com/Azure/cirrus/commit/38fe71aff5516bfd08e3c71ce862cb184f5ae2be"
        },
        "date": 1781102897529,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 13718562,
            "unit": "ns",
            "range": "± 579866.9912617892"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 14061219.7,
            "unit": "ns",
            "range": "± 360439.7625230879"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 13850062.5,
            "unit": "ns",
            "range": "± 491958.0732318558"
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
          "id": "1a1a079820fa407bfa240c496ac605bf9fc5c75b",
          "message": "[Bugfix] Update New-ResourceGroup cmdlet use new telemetry (#275)\n\nFix failed New-ResourceGroup cmdlet build referencing old telemetry\nimplementation",
          "timestamp": "2026-06-11T17:52:23+10:00",
          "tree_id": "9e95ae10e346ff93c8a23a0e6fa96fc94d9f65c6",
          "url": "https://github.com/Azure/cirrus/commit/1a1a079820fa407bfa240c496ac605bf9fc5c75b"
        },
        "date": 1781164473478,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 13566880,
            "unit": "ns",
            "range": "± 363032.13006380945"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 13809071.4,
            "unit": "ns",
            "range": "± 293552.2135298932"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 13257253.3,
            "unit": "ns",
            "range": "± 349921.0912673027"
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
          "id": "f9c7b6cda2c849e33e60b19f2ea44b4a3bfa25dd",
          "message": "{Extensions} Add extension management functionality (#253)\n\n## Add Extension Management\nImplements registry-backed dispatch where the host CLI ships a\nbuild-time verbΓåÆpackageId hint table and routes installed-extension\nverbs through a self-describing `registry.json`. Replaces the prior\n\"extension verb == DLL file name\" coupling and lets one extension\ncontribute multiple top-level namespaces.\n\nArchitecture\n\n```mermaid\nflowchart TD\n    User[\"azv2 vm list\"] --> Orch[OrchestratorEngine]\n    Orch --> Default[DefaultCommandEngine]\n    Default --> Disp[ExtensionDispatcher]\n\n    Disp --> S1{\"1. Built-in seed<br/>extension, login, config\"}\n    Disp --> S2{\"2. Reconciled registry<br/>live entries only\"}\n    Disp --> S3{\"3. Hint table<br/>pre-install catalog\"}\n\n    S1 -->|hit| Run[Load and invoke]\n    S2 -->|hit| MV[\"MultiVersionExtensionLoader<br/>per-pkg ALC cache\"]\n    MV --> Run\n    S3 -->|hit| ENF[\"ExtensionNotFound<br/>plus install hint\"]\n    S3 -->|miss| Agent[AgentEngine fallback]\n\n    subgraph Build[\"Build time\"]\n      Targets[\"ExtensionHints.targets<br/>RoslynCodeTaskFactory\"]\n      Scan[\"Scan src/Extensions<br/>ExtensionVerb plus s_cliTable\"]\n      Hints[\"extension-hints.json<br/>embedded in azv2.dll\"]\n      Targets --> Scan --> Hints\n    end\n    Hints -.embedded.-> S3\n\n    subgraph Disk[\"~/.cirrus/extensions/\"]\n      Reg[\"registry.json<br/>schema v2\"]\n      Pkgs[\"packageId/version/*.dll\"]\n    end\n    Reg -.reconciled.-> S2\n    Pkgs -.exists check.-> Reg\n```\n\nInstall / dispatch flow\n\n```mermaid\nsequenceDiagram\n    participant U as User\n    participant Add as extension add\n    participant CG as CompatibilityGuard\n    participant VR as ExtensionVerbReader\n    participant LR as LeafReader\n    participant Plan as ExtensionAddPlanner\n    participant Reg as FileRegistryStore\n\n    U->>Add: extension add Compute\n    Add->>Add: ResolvePackageId (feed | LocalArtifactProbe on miss)\n    Add->>CG: CheckAssembly (MetadataLoadContext, no exec)\n    CG-->>Add: compatible / reason\n    Add->>VR: ReadVerbs\n    Add->>LR: Read s_cliTable leaf keys\n    Add->>Plan: PlanContributions(verbs, leaves, registry)\n    Plan-->>Add: contributions[] OR batched LeafConflict\n    Add->>Reg: persist (mtime CAS)\n``` \n \nBuild time\n`ExtensionHints.targets` for `ExtensionVerb` declarations and every key\nof each project's s_cliTable, emitting a single `extension-hints.json`\nembedded resource in azv2.dll.\nBuild fails if any s_cliTable key's first token is not declared via\n[ExtensionVerb] in the same project ΓÇö guarantees the hint table is\ncomplete and accurate at compile time.\n[ExtensionVerb] is AllowMultiple = true; a project may declare more than\none namespace.\nRuntime\nOrchestratorEngine ΓåÆ DefaultCommandEngine ΓåÆ ExtensionDispatcher\nroutes verbs through:\nBuilt-in seed (extension, login, config)\nReconciled view of ~/.cirrus/extensions/registry.json (on-disk DLL must\nstill exist)\nEmbedded hint table ΓåÆ ExtensionNotFound with install hint (and a\n--path suggestion when [LocalArtifactProbe] finds a matching dev-built\n.nupkg under [Debug]\nAI agent fallback (AgentEngine) for free-form input\nRegistryReconciler filters entries whose installed DLL no longer exists\n(Live vs Stale).\nFileExtensionRegistryStore has optimistic-concurrency on writes (mtime\nCAS) and load-time duplicate-contribution validation.\nMultiVersionExtensionLoader caches per-[(dir, dll)] AssemblyLoadContext;\nbypassable via assemblyLoading.bypassExtensionCache config or\nCIRRUS_NO_EXTENSION_CACHE=true (also set by the CLI's --no-cache flag).\n[CompatibilityGuard] reads [CirrusCompatibility(MinCoreVersion,\nMaxCoreVersion)] via MetadataLoadContext (no execution); enforced at\ninstall (hard fail), advisory at load.\n[ExtensionAddPlanner] (extracted for testability) batches all (verb,\npath) registry collisions into a single [LeafConflict]so operators get a\ncomplete diagnosis in one round-trip; self-upsert is allowed.\n\n---\n\n## Review-cycle changes\n\n**`--name` selector contract.** `extension\nadd/remove/show/update/pin/unpin --name <token>` accepts package\nidentifiers only — full id (`Microsoft.Azure.Cirrus.Extensions.Compute`)\nor short suffix (`Compute`). Verb and namespace tokens are rejected:\n`add` returns a `UsageError` with a `--name <Package>` suggestion; the\nothers return `ExtensionNotFound` with a `Did you mean '--name\n<Package>'?` hint when the token unambiguously identifies an installed\npackage. The unit of install/remove is a package, not a verb — Compute\nships both `vm` and `disk`, so accepting `--name disk` would silently\nuninstall the whole package. Implemented in `RegistryLookup.TryFind`\n(package-id / short-suffix only) plus\n`RegistryLookup.SuggestPackageForToken` for the diagnostic hint.\n\n**LocalArtifactProbe input guard.** Probe input is restricted to NuGet\npackage id grammar (`[A-Za-z0-9._-]`) before any IO — defense-in-depth\nagainst path-traversal in user-influenced `packageId` (dotted tokens\npass through `Add.ResolvePackageId` unchanged).",
          "timestamp": "2026-06-12T15:40:05+10:00",
          "tree_id": "862ea0daa3ab57fe7c482d73154351413a6c75ae",
          "url": "https://github.com/Azure/cirrus/commit/f9c7b6cda2c849e33e60b19f2ea44b4a3bfa25dd"
        },
        "date": 1781242923640,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 248670,
            "unit": "ns",
            "range": "± 64087.11962633365"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 227722.5,
            "unit": "ns",
            "range": "± 44668.5222015086"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 257967.4,
            "unit": "ns",
            "range": "± 55436.3044024401"
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
          "id": "d3dfbd4814ec1717cc74d1257a495bd6f2f7711a",
          "message": "Trigger stable release 0.1.1 (#278)\n\nUpdates src/CLI/Core.Cli.csproj to 0.1.1. After this PR merges to main,\ncirrus-trigger-stable-release will run again from the main version\nchange, tag the merged commit as v0.1.1, and start the stable\nbuild/sign/publish chain.\n\nCo-authored-by: Cirrus Release Automation <cirrus-release-automation@microsoft.com>",
          "timestamp": "2026-06-12T15:48:31+10:00",
          "tree_id": "e1e14c0e2f08def0b5cff51254c1bf06bb714d6e",
          "url": "https://github.com/Azure/cirrus/commit/d3dfbd4814ec1717cc74d1257a495bd6f2f7711a"
        },
        "date": 1781243434440,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 247798.5,
            "unit": "ns",
            "range": "± 3076.733170101031"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 151773.75,
            "unit": "ns",
            "range": "± 6661.639256469337"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 268792.9,
            "unit": "ns",
            "range": "± 26690.817902417304"
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
          "id": "cd061c2fdcc4043b86df13f373ec474ab8008c3a",
          "message": "Trigger stable release 0.1.2 (#280)\n\nUpdates src/CLI/Core.Cli.csproj to 0.1.2. After this PR merges to main,\ncirrus-trigger-stable-release will run again from the main version\nchange, tag the merged commit as v0.1.2, and start the stable\nbuild/sign/publish chain.\n\nCo-authored-by: Cirrus Release Automation <cirrus-release-automation@microsoft.com>",
          "timestamp": "2026-06-12T16:41:02+10:00",
          "tree_id": "364b38c3fcdc7c0d6dd7c2b300fb983efc386019",
          "url": "https://github.com/Azure/cirrus/commit/cd061c2fdcc4043b86df13f373ec474ab8008c3a"
        },
        "date": 1781246593583,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 164453.25,
            "unit": "ns",
            "range": "± 2776.4913560103155"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 161939.75,
            "unit": "ns",
            "range": "± 6944.232012973069"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 237165.5,
            "unit": "ns",
            "range": "± 8080.579744053022"
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
          "id": "35883fb273acb4af74b0b989a694bccdaf87abd4",
          "message": "agent: bring up builtin BYOM provider (Responses API via APIM) (#268)\n\nWires the agent's `builtin` provider end-to-end against managed\nGPT-5.4 for private-preview kickoff. MSAL ARM-scope token → APIM →\nAOAI cli-copilot. `github` provider path unchanged.\n\nSpike, not for merge.\n\n## Commits\n\n1. `agent: implement BYOM provider (builtin/github/custom)` —\n   AgentOptions adds `Provider` + nested `BuiltinProviderOptions`;\n   ChatAgent.CreateAsync switches on provider; custom throws\n   NotImplementedException.\n2. `agent: friendlier builtin-provider auth error message` —\n   actionable `azv2 login` hint + alternative `agent.provider github`\n   switch.\n3. `agent: switch builtin provider to AOAI Responses API` — WireApi\n   \"responses\", api-version 2025-04-01-preview, endpoint includes\n   /openai/.\n\nCompanion: APIM-side ADO PR #1867 (azure-client-tools-copilot).\n\n## Test plan\n\n- [x] `dotnet build cirrus.slnx` clean\n- [x] `azv2 login` + `azv2 vm makenew` → agent fallback streams\n      real GPT-5.4 response through APIM\n- [ ] `agent.provider github` switches back unchanged\n- [ ] `agent.provider custom` throws NotImplementedException",
          "timestamp": "2026-06-15T14:32:20+10:00",
          "tree_id": "5cab116006d63ef35c6b0c1e4241300a145f73e2",
          "url": "https://github.com/Azure/cirrus/commit/35883fb273acb4af74b0b989a694bccdaf87abd4"
        },
        "date": 1781498083735,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 234422.75,
            "unit": "ns",
            "range": "± 54672.357984481816"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 317016.5,
            "unit": "ns",
            "range": "± 43902.844452267556"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 167986.9,
            "unit": "ns",
            "range": "± 9460.887738473595"
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
          "id": "58298ebbc20086293eac8d2390b747da91b5265a",
          "message": "Trigger stable release 0.1.3 (#286)\n\nUpdates src/CLI/Core.Cli.csproj to 0.1.3 and prepends CHANGELOG.md with\nmerged PRs since the previous stable tag. After this PR merges to main,\ncirrus-trigger-stable-release will run again from the main version\nchange, tag the merged commit as v0.1.3, and start the stable\nbuild/sign/publish chain.\n\nCo-authored-by: Cirrus Release Automation <cirrus-release-automation@microsoft.com>",
          "timestamp": "2026-06-16T16:02:19+10:00",
          "tree_id": "16423eea4d4d7bf46cee809d689851494592c7aa",
          "url": "https://github.com/Azure/cirrus/commit/58298ebbc20086293eac8d2390b747da91b5265a"
        },
        "date": 1781589858732,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 265929.8,
            "unit": "ns",
            "range": "± 27938.41752318839"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 174916.3,
            "unit": "ns",
            "range": "± 16197.825616421484"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 154704.75,
            "unit": "ns",
            "range": "± 9422.169508664127"
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
          "id": "9090a98b392f94d0d00a1b086dd7c0e007785e4a",
          "message": "[Telemetry] Activity name and tag property whitelists (#284)\n\nThe goal is to filter out activities / properties that could potentially\nemit PII for the sake of private preview.\nFor more details see: https://github.com/Azure/CLIPS/issues/369\n\n- Implement `TelemetryActivityNameFilter` base processor for\n`TracerProvider` to filter out non-allowed Activities\n- Implement `TelemetryPropertyFilter` base processor for\n`TracerProvider` to filter out non-allowed property tags",
          "timestamp": "2026-06-16T16:03:33+10:00",
          "tree_id": "2dc3da28e081b948f5c52df058fffb24392f9298",
          "url": "https://github.com/Azure/cirrus/commit/9090a98b392f94d0d00a1b086dd7c0e007785e4a"
        },
        "date": 1781589942888,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 223114,
            "unit": "ns",
            "range": "± 28005.366330996876"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 259876.25,
            "unit": "ns",
            "range": "± 15540.632706446242"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 257281.5,
            "unit": "ns",
            "range": "± 28103.374655012518"
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
          "id": "70198be3e1c160d177b3cbf0a2af4bfad945e6a8",
          "message": "{Help} Improve help system validation rules and workflow.  (#283)\n\n## Summary\n\nTightens the help validation pipeline and improves the authoring\nworkflow on both the CLI and PowerShell surfaces. Adds 4 new PowerShell\nvalidators (PSL9–PSL12), refactors shared parsing into\n`MarkdownHelpParser` / `HelpPaths`, hardens the example placeholder\nrules, and removes hard-coded module lists from the PS scaffold script.\n\n## What's in this PR\n\n### New validation rules\n\n| Id | Validator | What it catches |\n|----|-----------|-----------------|\n| `PSL9_MissingHelpFile` | `PsCompletenessValidator` | A cmdlet declared\nin `<Module>.psd1`'s `CmdletsToExport` has no matching\n`help/<Verb-Noun>.md`. |\n| `PSL10_MissingHelpFolder` | `PsCompletenessValidator` | Manifest\ndeclares cmdlets but the module folder has no `help/` directory at all\n(supersedes the per-cmdlet PSL9 cascade). |\n| `PSL11_OrphanedHelpFile` | `PsCompletenessValidator` | A `.md` exists\nfor a cmdlet that's no longer in `CmdletsToExport` (warning, not error).\n|\n| `PSL12_FillPlaceholder` | `PowerShellExampleSyntaxValidator` | A\nPlatyPS `{{ Fill ... }}` marker remains in the markdown. The evergreen\n`{{Insert list of aliases}}` boilerplate is exempt. |\n\nThese mirror what `HelpCompletenessValidator` does for the CLI surface —\nwalk the contract (manifest exports), not just the filesystem. Catches\ndrift like stale `CmdletsToExport` entries (e.g. `New-VMOrchestrator`)\non day one instead of months later.\n\n### Shared parsing infrastructure\n\n- `MarkdownHelpParser` — single front-matter / section / fence\nextraction path, shared by `MarkdownStructureValidator` and\n`PowerShellExampleSyntaxValidator`. Replaces ad-hoc regexes that had\ndrifted between validators.\n- `HelpPaths` — canonical path constants + casing tests\n(`HelpPathsCasingTests`) so Windows file-name casing must match the\nexpected form.\n- `PlaceholderDetector` — centralized angle-bracket / TODO / `{{ Fill\n... }}` regex helpers consumed by both surfaces.\n\n### Authoring workflow improvements\n\n- **`build/New-PSHelpMarkdown.ps1`**: removed hard-coded\n`[ValidateSet('Compute','Config','Login','Resources')]`. Modules are now\ndiscovered from `src/PS/<Name>/<Name>.psd1` at runtime; an\n`ArgumentCompleter` restores tab-completion. Adding a new module folder\nrequires zero script edits, with fail-fast on unknown `-Module` input\nlisting what's available.\n- **`--scaffold <verb>`** behaviour clarified for split-verb extensions:\neach verb requires its own scaffold call (`Compute` owns `vm` + `disk` —\ntwo calls). Idempotent.\n\n### Help corpus authored\n\n- **Compute** (6 cmdlets): `Get-VM`, `Remove-VM`, `New-VM`,\n`New-NetworkInterface`, `New-NetworkSecurityGroup`,\n`New-VirtualNetwork`.\n- **Login** (3): `Connect-CirrusAccount` (browser + device-code),\n`Disconnect-CirrusAccount`, `Get-CirrusAccount`.\n- **Config** (5): `Get-CirrusConfig`, `Get-CirrusConfigInfo`,\n`Get-CirrusConfigList`, `Remove-CirrusConfig`, `Set-CirrusConfig`.\n- **Resources** (3): `Get-ResourceGroup`, `New-ResourceGroup`,\n`New-ResourceGroupDeployment`.\n- **CLI** `vm/` and `disk/` corpora authored against the new rules.\n- Pruned 2 stale `CmdletsToExport` entries from `Compute.psd1`\n(`New-NetworkInterfaceOrchestrator`, `New-VMOrchestrator`) that PSL9\nsurfaced.\n\n### Docs\n\n- [docs/howto/help.md](docs/howto/help.md): added PSL9–PSL12 to the\nrules list and Troubleshooting; documented the new dynamic `-Module`\ndiscovery; updated the build pipeline step list.\n- [docs/spec/help-system.md](docs/spec/help-system.md): updated the\narchitecture diagram and validator table from 4 to 5 validators;\nexpanded the PS row to mention `PS_TodoPlaceholder` /\n`PS_ExamplePlaceholder` / PSL12.\n\n## Validation\n\n- `dotnet test tools/Help/Cirrus.Help.Tool.Tests` — **157/157** pass\n(added 12 `PsCompletenessValidator` tests + 5 PSL12 tests + reorganized\nmarkdown/example tests).\n- `dotnet test tests/Cli.Tests` — all green (incl. new\n`HelpPathsCasingTests`).\n- `./build/build.ps1 -GenerateHelpArtifacts` — 0 errors, 0 warnings,\nMAML emitted for all 4 PS modules, 7 manifests + `HelpIndex.json`.\n\n## Out of scope (tracked as a follow-up issue)\n\nMulti-extension-same-verb collisions (`Ext-A` and `Ext-B` both declaring\n`[ExtensionVerb(\"vm\")]`). Today the scaffolder silently first-wins via\n`break;` and the validator's `indexesByCliGroup` dictionary silently\nlast-wins. Worth handling once the Codegen design spec lands.",
          "timestamp": "2026-06-17T12:15:50+10:00",
          "tree_id": "9ec70f5dfc66f0d9da0ddde16e83fb9d69168fd9",
          "url": "https://github.com/Azure/cirrus/commit/70198be3e1c160d177b3cbf0a2af4bfad945e6a8"
        },
        "date": 1781662672149,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 165485.5,
            "unit": "ns",
            "range": "± 2189.548279135828"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 190563.5,
            "unit": "ns",
            "range": "± 33986.72866723127"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 250042.7,
            "unit": "ns",
            "range": "± 66885.16082585136"
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
          "id": "3d4e22ec556bd9ff7024708a16c12f67aff71af4",
          "message": "{Core} Remove direct reference from Core.Agent.csproj to Core.Config.csproj (#292)\n\nAddresses architectural concerns from\nhttps://github.com/Azure/cirrus/pull/268\n\nhttps://github.com/Azure/cirrus/pull/268/changes#r3393152782",
          "timestamp": "2026-06-18T11:19:41+10:00",
          "tree_id": "b1d13a8ee93d5b43844cc6bbfb3d7be1cdbfc899",
          "url": "https://github.com/Azure/cirrus/commit/3d4e22ec556bd9ff7024708a16c12f67aff71af4"
        },
        "date": 1781745745062,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 120405,
            "unit": "ns",
            "range": "± 5628.549250621035"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 134497.75,
            "unit": "ns",
            "range": "± 4415.780668994631"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 170414.6,
            "unit": "ns",
            "range": "± 50155.73010933048"
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
          "id": "b4f4d6152d3d63130e71ae7ed6f95e33fbcf16ec",
          "message": "feat(resources): add resource group deployment what-if (ARM + Bicep, LRO) (#271)\n\nAdds `deployment group what-if` (`New-ResourceGroupDeploymentWhatIf` /\n`azv2 deployment group what-if`), the second half of the original #261\nsplit. Foundation PR #262 has merged; this is the follow-up.\n\n## What it does\n- ARM `POST .../deployments/{name}/whatIf?api-version=2025-04-01`, an\nLRO returning a preview change list.\n- Mirrors the create command's Processor/Orchestrator split.\n- Shared template/parameter builders were extracted into\n`DeploymentOrchestratorBase` (the create orchestrator was reparented\nonto it, behavior-preserving).\n\n## Surface\n- **PS cmdlet** `New-ResourceGroupDeploymentWhatIf` — no `ShouldProcess`\n(read-only preview).\n- **CLI** `deployment group what-if`.\n- **Params** mirror create (`template-uri`/`file`/`spec-id`,\n`query-string`, `parameters-uri`/`file`, `mode`, `parameter`) PLUS\nwhat-if-specific `-ResultFormat`/`--result-format`\n(`FullResourcePayloads` default / `ResourceIdOnly`) and\n`-ValidationLevel`/`--validation-level`.\n- **NO rollback** (not applicable to what-if).\n\n## Behavior\n- Always polls the what-if LRO to terminal state (Location-only —\nAzure-AsyncOperation returns only `{status}` without the change list).\n- A what-if that fails (terminal Failed LRO, or 200 with `result.error`)\nis surfaced as a thrown failure, never as \"no changes\".\n\n## Command surface\nExposed as `azv2 deployment group what-if` now that the upstream CLI\nconvention change registers `deployment` as a real command group. The\nhelp-tool multi-group limitation noted for foundation PR #262 has since\nbeen resolved upstream, so no `azv2 group group what-if` workaround is\nneeded. The CLI help doc lives at\n`src/CLI/Help/Docs/deployment/group-what-if.md` and its examples use the\nreal `azv2 deployment group what-if` form.\n\n## Updated after rebase onto main\nRebased onto latest `Azure/cirrus:main` (HEAD `cd061c2f`):\n- **Command surface** is now `azv2 deployment group what-if` now that\n`deployment` is a registered command group (the prior `azv2 group group\nwhat-if` workaround is gone; PowerShell\n`New-ResourceGroupDeploymentWhatIf` unchanged).\n- **Telemetry** migrated to `ITelemetryService.StartRootActivity()` +\n`TagInvocation(...)` (replacing the removed `StartPowerShellActivity`\nAPI), mirroring the create cmdlet.\n- **Re-validated on latest main**: build 0 errors (3 pre-existing\nunrelated `GHCP001` warnings in `Agent/ChatAgent.cs`, not our delta);\nCli.Tests 213 passed / 0 failed / 4 skipped; offline reflection passed;\nhelp-validate exit 0 \"no issues\" (`deployment group create` +\n`deployment group what-if` present).\n\n## Validation\n- Build: 0 warnings / 0 errors.\n- Cli.Tests: 106 passed / 0 failed / 4 skipped.\n- help-validate: exit 0 \"no issues\" (`group create` + `group what-if`\ndocs present, no `CLI_L1_UnknownGroup`).\n- Offline reflection: 14/14.\n- Live Azure smoke (RG `cirrus-test-group-1`, run pre-rebase — logic\nunchanged by the clean rebase) CLI+PS: happy paths returned change\nlists; malformed-template / invalid-result-format / API-error cases\nfailed as expected; the API-error case threw a flattened error (never\n\"no changes\"); 0 unexpected. What-if is read-only — no resources\ncreated.\n\n## Deferred / follow-up\n- The 200+Succeeded+`result.error` branch is covered by the live\nAPI-error script but not yet by a unit test (Cli.Tests has no\norchestrator-level harness; the response injection point is `internal`\nand there's no fake `IEventPublisher`/`ProcessorContext` fixture —\nbuilding one is out of scope here).",
          "timestamp": "2026-06-18T13:09:33+08:00",
          "tree_id": "ebddd04a1fd33a40b3edf91e56a374a9982cb66b",
          "url": "https://github.com/Azure/cirrus/commit/b4f4d6152d3d63130e71ae7ed6f95e33fbcf16ec"
        },
        "date": 1781759552690,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 286320.2,
            "unit": "ns",
            "range": "± 31047.084222193876"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 261828.8,
            "unit": "ns",
            "range": "± 12756.517773279666"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 209276.2,
            "unit": "ns",
            "range": "± 50169.05202114148"
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
          "id": "bd43775a0c844a500cc5dc8c677757c6f404cc18",
          "message": "feat(rest): add universal authenticated REST passthrough command (azv2 rest / Invoke-CirrusRestMethod) (#260)\n\n## Summary\n\nAdds a universal authenticated REST passthrough command to Cirrus:\n\n- New `rest` command (`azv2 rest`) and the `Invoke-CirrusRestMethod`\nPowerShell cmdlet, shipped as part of the **Resources** extension/module\n(see \"Architecture\" below).\n- Option-2 / `az rest` parity: **no** LRO polling, **no**\nauto-pagination, **ARM-only**.\n- Relative-path ARM prefixing (the `https://management.azure.com`\nendpoint is prepended) plus `{subscriptionId}` token substitution from\nthe auth context.\n- **UNTYPED** `RestResponse` output (status / headers / raw body) so\ncallers can hit any ARM API without a typed model.\n- **ByParameters URL builder** (`Invoke-AzRestMethod`-style): build the\nARM path piece-wise from subscription / resource group / provider /\ninterleaved resource-type+name segments / api-version, instead of\nsupplying a full `--uri`.\n- **Arbitrary query-string parameters** via `--uri-parameters` /\n`-UriParameters` (KEY=VALUE), appended to the final URL in either URI or\nByParameters mode.\n\n## Architecture — merged into Resources (review feedback)\n\nPer review feedback, the `rest` surface is **not** a standalone\nextension/module — it is consolidated into the existing **Resources**\nextension and PowerShell module:\n\n- The `Invoke-CirrusRestMethod` cmdlet ships in the **Resources**\nPowerShell module (`Cmdlets.Resources.dll`; added to `Resources.psd1`\n`CmdletsToExport`). The standalone `Rest` PS module (`Rest.psd1`) is\nremoved.\n- The `rest` CLI verb is registered in the Resources extension index\n(`src/Extensions/Resources/ExtensionIndex.cs`: `[ExtensionVerb(\"rest\")]`\n+ `s_cliTable[\"rest\"]`); the standalone `Extensions.Rest` project is\nremoved from `cirrus.slnx` and `Cli.Tests.csproj`.\n- `RestProcessor_Orchestrator` is a fully customized, standalone\n`IProcessor` (no base class) and lives under\n`src/Extensions/Resources/Orchestrators/`; `RestResponse` moved to\n`src/Extensions/Resources/Models/`.\n- The cmdlet stays thin: the `NormalizeKeyValue` input helper moved out\nof the cmdlet onto the orchestrator.\n- A help file for the relocated cmdlet was added\n(`src/PS/Resources/help/Invoke-CirrusRestMethod.md`) to satisfy the\nhelp-validate gate.\n\n**User-facing surface is unchanged** — `azv2 rest ...` and\n`Invoke-CirrusRestMethod` behave exactly as before; only their\npackaging/home moved. Note for anyone importing the old module:\n`Invoke-CirrusRestMethod` now comes from the `Resources` module, not a\nseparate `Rest` module.\n\n## Naming rationale — `Invoke-CirrusRestMethod` (reviewer input welcome)\n\nThe PowerShell cmdlet is named **`Invoke-CirrusRestMethod`**, mirroring\nAzure PowerShell's **`Invoke-AzRestMethod`** but substituting Cirrus's\nbrand prefix for `Az`. Reasoning:\n\n- **The bare `Invoke-RestMethod` is unavailable** — it's a built-in core\nPowerShell cmdlet, so an unprefixed name would shadow/collide with the\nshell's own command. This is the same reason Az ships\n`Invoke-AzRestMethod` rather than `Invoke-RestMethod`.\n- **We can't reuse `Invoke-AzRestMethod`** — the `Az` noun prefix is\nowned by the Azure PowerShell `Az.*` modules, and `Invoke-AzRestMethod`\nalready ships in `Az.Accounts`. Reusing it would collide whenever the Az\nmodules are loaded (very common) and misrepresent a Cirrus command as an\nAz-team one.\n- **Consistency with Cirrus's tool/meta cmdlets** — a REST passthrough\nis a tool-level command, and Cirrus's other tool cmdlets already carry\nthe `Cirrus` prefix (`Connect-CirrusAccount`, `Get-CirrusAccount`,\n`Set-CirrusConfig`). `Invoke-CirrusRestMethod` sits naturally alongside\nthem. (Cirrus's *resource* cmdlets — e.g. `New-ResourceGroup`, `New-VM`\n— deliberately drop the prefix for readability; this command is\nintentionally in the tool camp, not the resource camp.)\n\nAlternatives considered (open to reviewer preference):\n- `Invoke-CirrusRest` — shorter, still prefixed and in the tool camp;\nloses exact `Invoke-AzRestMethod` shape parity.\n- `Invoke-RestApi` — drops the brand prefix (moves it to the\nresource-cmdlet camp); no known collision today, but less consistent\nwith the other tool cmdlets and loses the Az-shape familiarity. (Note:\nPascalCase `Api`, not `API`.)\n\n**Reviewers: please weigh in on whether to keep\n`Invoke-CirrusRestMethod` (current), or prefer `Invoke-CirrusRest` /\n`Invoke-RestApi` / another name.** The CLI surface (`azv2 rest`) is\nunaffected by this choice.\n\n## New input modes\n\n### B. ByParameters URL builder\n\nSupply the ARM path piece-wise; the command assembles\n`/subscriptions/{sub}[/resourceGroups/{rg}][/providers/{ns}]/{type}/{name}.../?api-version=...`.\n`Name` must equal `ResourceType` or be exactly one fewer (a single\ntrailing type with no name performs a collection GET). `ApiVersion` is\nrequired in this mode.\n\n| CLI flag | PS parameter | Maps to |\n| --- | --- | --- |\n| `--subscription` | `-SubscriptionId` | `SubscriptionId` |\n| `--resource-group` | `-ResourceGroupName` | `ResourceGroupName` |\n| `--resource-provider` | `-ResourceProviderName` |\n`ResourceProviderName` |\n| `--resource-type` | `-ResourceType` | `ResourceType` (list) |\n| `--name` | `-Name` | `Name` (list) |\n| `--api-version` | `-ApiVersion` | `ApiVersion` |\n\n### C. URI query parameters\n\n| CLI flag | PS parameter | Maps to |\n| --- | --- | --- |\n| `--uri-parameters` | `-UriParameters` | `UriParameters` (KEY=VALUE\nlist) |\n\nThe piece-wise ByParameters inputs are mutually exclusive with `--uri` /\n`-Uri`; supplying both throws a deterministic validation error.\n`--uri-parameters` is allowed in either mode.\n\n## Security / behavior surface changes (please review)\n\n1. **Host + scheme allowlist.** Absolute URLs pointing at non-ARM hosts\nand any non-`https` scheme are now **REJECTED before any request is\nsent**, so the ARM bearer token is never leaked to an arbitrary\ndestination. The ARM token is only attached to allowlisted ARM hosts:\n`management.azure.com`, `management.usgovcloudapi.net`,\n`management.chinacloudapi.cn`, `management.microsoftazure.de`.\n2. **HTTP `>= 400` now causes a non-zero exit.** The full `RestResponse`\n(status/headers/body) is still shown, but the CLI exits non-zero and the\nPS cmdlet raises a terminating error so `set -e` / `$?` / try-catch can\ndetect it. Note: the pre-existing `CIRRUS_AGENT_DISABLED=1` flag may be\nneeded in CI to observe `$?`.\n3. **New CLI/PS surface** (`azv2 rest` + `Invoke-CirrusRestMethod`).\nCirrus is pre-release, so the new surface is additive.\n4. **`--uri` / `-Uri` is no longer required** (pre-release surface\nchange). It was previously the only way to specify the target; a request\ncan now instead be described entirely via the ByParameters inputs.\nExactly one of the two modes must be supplied.\n\n5. **`RestResponse.Json` is Content-Type gated.** After Fix 2,\n`RestResponse.Json` is only populated when the response `Content-Type`\nis JSON-like; a body that is valid JSON but served **without** a JSON\n`Content-Type` now surfaces only as raw `Content` (harmless against ARM,\nwhich always returns `application/json`).\n\n6. **ByParameters segment validation.** ByParameters mode now rejects\nempty/whitespace/slash-only `ResourceType`/`Name` segments with a\ndeterministic `InvalidOperationException` (previously such inputs could\nemit a malformed double-slash ARM path).\n\n## Deferred / future polish\n\n- PowerShell **parameter-set modeling** of the URI-vs-ByParameters modes\nis deferred as future UX polish. The current shape (all inputs\n`Mandatory=false` with processor-side mutual-exclusion validation) is\nfunctionally correct and rejects invalid combinations deterministically;\nmodeling distinct parameter sets would mainly improve tab-completion /\nhelp ergonomics.\n\n## Test Evidence (2026-06-18, post Rest→Resources merge)\n\n**Environment** — subscription `282bb33f-067d-4179-a7a4-b833c13af735`,\nregion `australiaeast`; branch `feature/rest-passthrough-command`; head\ncommit `bc63937a`; date 2026-06-18 (UTC). Live runs executed against the\nshared test subscription using cached MSAL auth (verified valid via a\nread-only `rest` ByParameters GET on `cirrus-test-group-1`, HTTP 200,\n`provisioningState: Succeeded`). `CIRRUS_AGENT_DISABLED=1` was set so\nthe post-failure agent-mode TUI does not swallow non-zero exits. After\nthe Rest→Resources merge a live Azure smoke test was re-run: both `azv2\nrest` (including both ByParameters modes) and `Invoke-CirrusRestMethod`\nresolved from and executed through the **Resources** extension/module\nand returned HTTP 200 against ARM (read-only GETs); `Get-Command\nInvoke-CirrusRestMethod` reports `Source = Resources`.\n\nThis run re-validates the three review-feedback fixes applied in\n`660eaab8`:\n\n1. **ByParameters segments are percent-encoded**\n(`BuildByParametersPath` uses `Uri.EscapeDataString` per id segment) —\nreserved/structural characters can no longer inject extra path/query\nstructure; ordinary values (GUIDs, `Microsoft.Storage`,\n`storageAccounts`, `cirrus-test-group-1`) are byte-for-byte unchanged,\nso cases 4 & 5 below still produce the same URLs and still return HTTP\n200.\n2. **Non-JSON passthrough responses no longer emit `ResponseInvalidJson`\nnoise** — the JSON parse is attempted only when the response\n`Content-Type` is JSON-like; non-JSON bodies surface as raw `Content`.\n3. **Allowlist rejection message lists hosts in deterministic\n(`OrdinalIgnoreCase`) order** — see negative case 11 below.\n\n### Build\n`dotnet build cirrus.slnx -c Debug` — **Build succeeded, 0 Warning(s) /\n0 Error(s)** (exit 0).\n\n### Cli.Tests\n`dotnet test tests/Cli.Tests/Cli.Tests.csproj` — **243 passed / 0 failed\n/ 4 skipped** (includes the RestProcessor tests, now part of the\nResources/Cli.Tests suite).\n\n### Live CLI — `azv2 rest` (`tests/Scripts/CLI/rest.sh`)\n\n| # | Case | Result |\n|---|------|--------|\n| 1 | relative ARM path + `{subscriptionId}` substitution | ✅ HTTP 200,\nJSON resource-group list (exit 0) |\n| 2 | absolute ARM URL passthrough (single RG) | ✅ HTTP 200, JSON (exit\n0) |\n| 3 | explicit header `Accept=application/json` | ✅ HTTP 200, JSON (exit\n0) |\n| 4 | ByParameters collection (`Microsoft.Storage` / `storageAccounts`,\napi-version `2021-09-01`) | ✅ HTTP 200 — builder assembled\n`…/providers/Microsoft.Storage/storageAccounts`, returned the\nsubscription-wide `value` array (exit 0); encoding fix leaves the URL\nunchanged |\n| 5 | ByParameters single resource group (`--resource-group\ncirrus-test-group-1`, api-version `2021-04-01`) | ✅ HTTP 200 — builder\nassembled `…/resourceGroups/cirrus-test-group-1`, returned the RG object\n(exit 0); encoding fix leaves the URL unchanged |\n| 6 | `--uri-parameters '$top=3'` | ✅ HTTP 200, JSON (exit 0) |\n| 7 | **neg** unsupported method `FOO` | ✅ rejected, exit 1 |\n| 8 | **neg** `--uri` + ByParameters mutual exclusivity | ✅ rejected\nbefore send, exit 1 |\n| 9 | **neg** ByParameters missing `--api-version` | ✅ rejected before\nsend, exit 1 |\n| 10 | **neg** HTTP 404 (`this-rg-does-not-exist-404`) | ✅ full\nRestResponse (status/headers/body) printed **and** exit 1 |\n| 11 | **neg** non-ARM host `https://example.com` | ✅ rejected **before\nany request is sent** (no token leak), exit 1 |\n| 12 | **neg** non-`https` ARM URL (`http://management.azure.com/…`) | ✅\nrejected before send, exit 1 |\n\nNegative-case rejection messages (verbatim):\n\n```\n# 7 unsupported method\n[ERROR] Unsupported HTTP method 'FOO'. Supported methods: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.\n\n# 8 Uri + ByParameters mutual exclusivity\n[ERROR] Specify the endpoint either as a full URL (Uri) OR as the piece-wise parameters (SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion), not both.\n\n# 9 ByParameters missing api-version\n[ERROR] ApiVersion is required when building an ARM URL from piece-wise parameters.\n\n# 10 HTTP 404 — printed AFTER the full RestResponse (statusCode 404 + headers + body) was emitted\n[ERROR] 404 Not Found from https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups/this-rg-does-not-exist-404?api-version=2021-04-01\n\n# 11 non-ARM host rejected before send (token never attached) — hosts now listed in deterministic order\n[ERROR] Host 'example.com' is not an allowed Azure Resource Manager endpoint. The rest command only targets ARM in this release. Allowed hosts: management.azure.com, management.chinacloudapi.cn, management.microsoftazure.de, management.usgovcloudapi.net.\n\n# 12 non-https ARM URL rejected before send\n[ERROR] Absolute URL 'http://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups?api-version=2021-04-01' must use https. The rest command only targets ARM over https in this release.\n```\n\n### Live PS — `Invoke-CirrusRestMethod` (`tests/Scripts/PS/rest.ps1`)\n\n| # | Case | Result |\n|---|------|--------|\n| 1 | relative ARM path + `{subscriptionId}` substitution | ✅ StatusCode\n200, parsed JSON (`$?` = true) |\n| 2 | absolute ARM URL passthrough (single RG) | ✅ StatusCode 200 (`$?`\n= true) |\n| 3 | headers hashtable `@{ Accept = 'application/json' }` | ✅\nStatusCode 200 (`$?` = true) |\n| 4 | ByParameters collection (`Microsoft.Storage` / `storageAccounts`,\napi-version `2021-09-01`) | ✅ StatusCode 200 — builder assembled\n`…/providers/Microsoft.Storage/storageAccounts`, returned the\nsubscription-wide `value` array (`$?` = true) |\n| 5 | ByParameters single resource group (`-ResourceGroupName\ncirrus-test-group-1`, api-version `2021-04-01`) | ✅ StatusCode 200 —\nbuilder assembled `…/resourceGroups/cirrus-test-group-1`, returned the\nRG object (`$?` = true) |\n| 6 | `-UriParameters @{ '$top' = '3' }` | ✅ StatusCode 200 (`$?` =\ntrue) |\n| 7 | **neg** unsupported method `FOO` | ✅\n`ParameterBindingValidationException` (ValidateSet), rejected |\n| 8 | **neg** `-Uri` + ByParameters mutual exclusivity | ✅\n`InvalidOperationException` before send |\n| 9 | **neg** ByParameters missing `-ApiVersion` | ✅\n`InvalidOperationException` before send |\n| 10 | **neg** HTTP 404 (`this-rg-does-not-exist-404`) | ✅ full\n`RestResponse` (StatusCode 404 + headers) emitted, **then** terminating\nerror (`$?` = false) |\n| 11 | **neg** non-ARM host `https://example.com` | ✅\n`InvalidOperationException` **before any request is sent** (no token\nleak); hosts listed in deterministic order |\n| 12 | **neg** non-`https` ARM URL (`http://management.azure.com/…`) | ✅\n`InvalidOperationException` before send |\n\nNegative-case rejection messages (verbatim):\n\n```\n# 7 unsupported method (ValidateSet)\nCannot validate argument on parameter 'Method'. The argument \"FOO\" does not belong to the set \"GET,POST,PUT,PATCH,DELETE,HEAD,OPTIONS\" specified by the ValidateSet attribute. Supply an argument that is in the set and then try the command again.\n\n# 8 Uri + ByParameters mutual exclusivity\nSpecify the endpoint either as a full URL (Uri) OR as the piece-wise parameters (SubscriptionId/ResourceGroupName/ResourceProviderName/ResourceType/Name/ApiVersion), not both.\n\n# 9 ByParameters missing api-version\nApiVersion is required when building an ARM URL from piece-wise parameters.\n\n# 10 HTTP 404 — RestResponse object emitted first, then this terminating error ($? = false)\n404 Not Found from https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups/this-rg-does-not-exist-404?api-version=2021-04-01\n\n# 11 non-ARM host rejected before send (token never attached) — hosts now listed in deterministic order\nHost 'example.com' is not an allowed Azure Resource Manager endpoint. The rest command only targets ARM in this release. Allowed hosts: management.azure.com, management.chinacloudapi.cn, management.microsoftazure.de, management.usgovcloudapi.net.\n\n# 12 non-https ARM URL rejected before send\nAbsolute URL 'http://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups?api-version=2021-04-01' must use https. The rest command only targets ARM over https in this release.\n```\n\n### Verdict\n✅ Build is clean (0/0) and Cli.Tests are green (243 passed / 0 failed /\n4 skipped). On **both** the CLI (`azv2 rest`) and PS\n(`Invoke-CirrusRestMethod`) surfaces every happy path — relative URI,\nabsolute URI, header-bearing, **both ByParameters modes**, and the\n`$top` query parameter — returned HTTP 200 with a parsed JSON body; the\nByParameters URLs (cases 4 & 5) are unchanged by the new per-segment\nencoding. Every security/validation negative was rejected **before any\nrequest was sent** (unsupported method, Uri↔ByParameters mutual\nexclusivity, missing api-version, non-ARM host, non-`https` scheme), and\nthe HTTP ≥ 400 case surfaced the full `RestResponse` body **and**\npropagated a non-zero exit / terminating error. The allowlist rejection\nmessage now lists the allowed hosts in deterministic order.",
          "timestamp": "2026-06-18T15:09:27+08:00",
          "tree_id": "a817e48e05c06f4eb2aeedabc4d6f7f871e6dbef",
          "url": "https://github.com/Azure/cirrus/commit/bd43775a0c844a500cc5dc8c677757c6f404cc18"
        },
        "date": 1781766690362,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 260971.4,
            "unit": "ns",
            "range": "± 17367.840490400642"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 262634.4,
            "unit": "ns",
            "range": "± 21063.619686559097"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 242391.5,
            "unit": "ns",
            "range": "± 15563.259844903956"
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
          "id": "55924c69d6da2b87f06035b05a27c90f403224f2",
          "message": "[Telemetry] Add subscriptionId prop to root activity (#290)\n\n- Add `subscriptionId` and `tenantId` to activity root \n- Rename TelemetryService method param names for clarity\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-19T11:02:51+10:00",
          "tree_id": "3a73c5fad609d09ea4f8d241be25dccfb776c3ee",
          "url": "https://github.com/Azure/cirrus/commit/55924c69d6da2b87f06035b05a27c90f403224f2"
        },
        "date": 1781831083095,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 243403.25,
            "unit": "ns",
            "range": "± 11318.880579957247"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 175687,
            "unit": "ns",
            "range": "± 3655.7540945747432"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 162039,
            "unit": "ns",
            "range": "± 9829.287054512142"
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
          "id": "2a3fa51f3774f4f301b041b7fcebe93f5901742a",
          "message": "{Core} Logging respects config values (CLI) (#300)\n\n## Summary\nMake CLI host logging respect configured log levels from\nconfig/environment, with `--debug` as a one-shot invocation override.\n\n## Problem\nCLI runtime logging streams were effectively gated by `--debug` only and\ndid not honor values set via:\n- `core.logging.level`\n- `CIRRUS_LOG_LEVEL`\n\n## What Changed\n- Added `CliLoggingPolicy` in `src/CLI/CliLoggingPolicy.cs`:\n  - Parses configured level (`debug|info|warn|error`).\n- Resolves effective level precedence: `--debug` override > env > global\nconfig > default.\n  - Applies severity emission matrix.\n- Updated `src/CLI/Program.cs`:\n  - Parse/strip `--debug` once at startup.\n  - Resolve effective log level via `IConfigurationService`.\n- Build `OutputStreams` from effective level policy (instead of fixed\nno-op streams).\n- Added unit tests in `tests/Cli.Tests/CliLoggingPolicyTests.cs`:\n  - Parse coverage and defaults.\n  - Emission matrix coverage.\n  - Precedence checks (env over config, debug override over both).\n- Documentation updates:\n  - `docs/howto/config.md`\n  - `docs/spec/config-implementation.md`\n\n## Effective Behavior\n- `error`: error only\n- `warn`: warn + error\n- `info`: info + warn + error\n- `debug`: verbose + debug + info + warn + error\n\nPrecedence:\n1. `--debug` (one-shot for invocation)\n2. `CIRRUS_LOG_LEVEL`\n3. `core.logging.level` (global config)\n4. default `info`\n\n## Validation\nAutomated:\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~CliLoggingPolicyTests\"`\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~ConfigurationServiceTests|FullyQualifiedName~ConfigCliTests|FullyQualifiedName~ProgramFallbackTests\"`\n\nManual:\n- Verified runtime logs at `debug` and suppression at `error`.\n- Verified env override (`CIRRUS_LOG_LEVEL=error`) over global config.\n- Verified `--debug` override over env/config for a single invocation.\n\n## Scope\n- CLI host behavior only (PowerShell parity is intentionally\nout-of-scope for this PR).",
          "timestamp": "2026-06-19T14:18:56+10:00",
          "tree_id": "81668a4b48a88d2a58070caaab86c13794d9f0e6",
          "url": "https://github.com/Azure/cirrus/commit/2a3fa51f3774f4f301b041b7fcebe93f5901742a"
        },
        "date": 1781842861695,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 891007.5,
            "unit": "ns",
            "range": "± 43134.77180883191"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 832910,
            "unit": "ns",
            "range": "± 64471.690108915245"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 824990.7,
            "unit": "ns",
            "range": "± 16039.149859016843"
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
          "id": "140ddf8fa61b1d5016358eeedb68998f42b7f4eb",
          "message": "{Core} Logging respects config values (Pwsh) (#302)\n\n{Core} Logging respects config values (PowerShell)\n\nImplements PowerShell-side log-level enforcement to match CLI behavior\nfrom #300.\n\n## Changes\n- Added shared \\CmdletOutputStreamsFactory\\ in \\src/PS/Common/\\ to gate\nstream output by configured log level.\n- Migrated all PowerShell cmdlets (Config, Login, Compute, Resources)\nfrom manual \\OutputStreams\\ instantiation to centralized factory.\n- Enforces effective log level resolution from config → environment →\ndefaults, with error logs always emitted.\n\n## Log Level Gating\n- **error**: error only\n- **warn**: warning + error\n- **info**: info + warning + error (default)\n- **debug**: debug + verbose + info + warning + error\n\n## Validation\n- ✅ Manual runbook: config-level changes suppress/enable\ndebug/verbose/info correctly\n- ✅ Env override: \\CIRRUS_LOG_LEVEL=debug\\ overrides stored config value\n- ✅ Cross-module: consistent behavior across Config, Login, Compute,\nResources cmdlets\n- ✅ Builds clean: all PS cmdlet projects compile without error\n\n## Related\nCloses #395. Companion to PR #300 (CLI-side logging policy).\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-19T18:01:41+10:00",
          "tree_id": "309bd212440db8289f0043253c1a906ee457d99e",
          "url": "https://github.com/Azure/cirrus/commit/140ddf8fa61b1d5016358eeedb68998f42b7f4eb"
        },
        "date": 1781856210972,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1139798.5,
            "unit": "ns",
            "range": "± 189452.4903733563"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 986946.2,
            "unit": "ns",
            "range": "± 112653.96211274595"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 952887.2,
            "unit": "ns",
            "range": "± 86166.9148786238"
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
          "id": "44830eaf314fa6d56bf68da182fcf1f242e8ce88",
          "message": "{Config} Add ability to retrieve sections (#306)\n\n## Summary\n- Allow config get --key <section> to return section/subsection values,\nnot only leaf keys.\n- Keep existing leaf-key behavior unchanged.\n- Support both CLI and PowerShell through shared command path.\n\n## Behavior\n- config get --key core returns flattened section entries in text mode.\n- config get --key core --json returns nested JSON for the section.\n- config get --key core.logging.level still returns a single key/value\nwith source.\n- Unknown keys continue to return not found.\n\n## Validation\n- dotnet build cirrus.slnx passes.\n- Manually verified CLI and PowerShell scenarios for section,\nsubsection, leaf, json, and not-found cases.",
          "timestamp": "2026-06-22T14:40:31+10:00",
          "tree_id": "1f98724cff0a40b7c3730cfd73faead220b30f32",
          "url": "https://github.com/Azure/cirrus/commit/44830eaf314fa6d56bf68da182fcf1f242e8ce88"
        },
        "date": 1782103346329,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 879186,
            "unit": "ns",
            "range": "± 46121.663015695056"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 873150.4,
            "unit": "ns",
            "range": "± 119631.96403679077"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 832099.6,
            "unit": "ns",
            "range": "± 70887.17420168476"
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
          "id": "5e3fd479b0357fd2775f1089ea61c453570eae27",
          "message": "[Benchmark] Stabilize benchmark results and override extension dispatcher. (#274)",
          "timestamp": "2026-06-22T15:46:48+10:00",
          "tree_id": "8da0b55ca4e2f90a8841b17fc5d18028b4bbbbac",
          "url": "https://github.com/Azure/cirrus/commit/5e3fd479b0357fd2775f1089ea61c453570eae27"
        },
        "date": 1782107506404,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1487631.6137697517,
            "unit": "ns",
            "range": "± 326236.69234131824"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1477453.6442502397,
            "unit": "ns",
            "range": "± 404738.865849579"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1413744.780116762,
            "unit": "ns",
            "range": "± 361538.421405212"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "yabhu@microsoft.com",
            "name": "Yabo Hu",
            "username": "VeryEarly"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "a208e07db7c22b77888e5e77ff7a01cb9acf7a92",
          "message": "Improve CommandMetadata, ParameterMetadata, DiscoverySession  (#307)\n\n- Introduced `InvocationName` in CommandMetadata, ParameterMetadata in\ncase we need to serialize `ExtensionIndex` in the future\n- Improved `DiscoverySession` to write `IDict<InvocationName,\nCommandMetadata>` instead of `List<CommandMetadata>`",
          "timestamp": "2026-06-22T22:12:07+10:00",
          "tree_id": "3892576741c0bb721da1f9ad4e253d231823d95c",
          "url": "https://github.com/Azure/cirrus/commit/a208e07db7c22b77888e5e77ff7a01cb9acf7a92"
        },
        "date": 1782130907607,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1271720.5054353676,
            "unit": "ns",
            "range": "± 347441.00629769644"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1226904.0232216034,
            "unit": "ns",
            "range": "± 335887.11654097424"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1264241.8603589584,
            "unit": "ns",
            "range": "± 354935.2732950573"
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
          "id": "ce4bc638e10f40d29515193aa26c1ddb76c7566c",
          "message": "Add shell tab-completion support (#272)\n\nCo-authored-by: Julie Zhu <105691024+yanzhudd@users.noreply.github.com>\nCo-authored-by: Julie <julie@Yemings-MacBook-Pro.local>",
          "timestamp": "2026-06-23T15:38:10+08:00",
          "tree_id": "2fe3ce80d92036f2f332ae10070b4b61417ddd05",
          "url": "https://github.com/Azure/cirrus/commit/ce4bc638e10f40d29515193aa26c1ddb76c7566c"
        },
        "date": 1782200547283,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1286511.658997578,
            "unit": "ns",
            "range": "± 323614.5383308275"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1240076.71128692,
            "unit": "ns",
            "range": "± 320736.91905457806"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1216471.2928533333,
            "unit": "ns",
            "range": "± 327282.4958593601"
          }
        ]
      }
    ]
  }
}