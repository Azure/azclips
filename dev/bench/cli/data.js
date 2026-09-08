window.BENCHMARK_DATA = {
  "lastUpdate": 1788842122539,
  "repoUrl": "https://github.com/Azure/azclips",
  "entries": {
    "Azclips Resources CLI Benchmarks": [
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
          "id": "401c2621a7f49caafbc26e093a609805ce7028b7",
          "message": "feat(agent): plan-mode — propose_plan + two-stage approval + structured rendering (#309)\n\n## What this PR adds\n\nBuilds on the agent already in main, adding **plan-mode**: when a CLI\ncommand fails, the agent proposes a structured, reviewable fix instead\nof just acting.\n\n1. **`propose_plan` + `submit_response` tools with a two-stage approval\ngate.** `propose_plan` carries a structured plan — `issues` (what went\nwrong), `steps` (the exact commands, destructive ones flagged),\n`placeholders` (secrets the user must fill in) and `defaults` (values\nthe agent chose). No command runs until a plan is approved;\n`submit_response` is the single point that ends the session. Both are\nvalidated in the pre-tool-use hook (deny + correction, never a stack\ntrace).\n\n2. **Structured terminal rendering of the plan.** The plan renders as\nstyled blocks (`What went wrong:`, `Plan:` with the commands, `Values to\nreview before running:`, `✓ Done`, and an `AI-generated content may be\nincorrect` disclaimer). One-shot runs in a quiet presentation that hides\nagent-internal event/token noise — only the spinner (showing the current\nstep) and the final result are shown.\n\n3. **Two interaction styles.** `oneShot` (propose a fix and exit,\naz-CLI-like) and `interactive` (stay in a chat loop, with an escape\nvalve), selected by `agent.sessionStyle`.\n\n4. **System prompts split per entrypoint.** `oneshot.md` / `inline.md` /\n`tui.md` under `Agent/prompts/` (replacing the single `agent.md`), with\nscope filtering and sign-in / SKU-availability guidance.\n\n5. **Default-off switch + agent config.** Agent fallback is gated on\n`agent.enabled` (default false — opt-in for Private Preview). Adds an\noptional JSONL session trace via `agent.trace.enabled` /\n`agent.trace.path` (off by default; prints the trace file path when\nenabled).\n\n<img width=\"1221\" height=\"198\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/9001b0ad-0c28-4adb-8ae2-e599ad135fae\"\n/>\n<img width=\"2539\" height=\"1077\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/97e3821c-154b-4d87-b61c-532274fc6ac5\"\n/>",
          "timestamp": "2026-06-24T12:35:31+10:00",
          "tree_id": "4d8809cc9b74310af7808b6d08c9ffad5ea47eee",
          "url": "https://github.com/Azure/cirrus/commit/401c2621a7f49caafbc26e093a609805ce7028b7"
        },
        "date": 1782268779806,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1393969.4349411398,
            "unit": "ns",
            "range": "± 359832.543958566"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1303499.9155310518,
            "unit": "ns",
            "range": "± 398996.20404848456"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1311260.6765899865,
            "unit": "ns",
            "range": "± 361561.4393172069"
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
          "id": "6b7d3ec50bdac64a3a61509391de903aa1685639",
          "message": "[Benchmark] Set benchmark workflow to use self-hosted runner (#312)\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-24T12:38:39+10:00",
          "tree_id": "b3f6b6f02907941261293c8614b4c8355779efd0",
          "url": "https://github.com/Azure/cirrus/commit/6b7d3ec50bdac64a3a61509391de903aa1685639"
        },
        "date": 1782268833017,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 954851.160230115,
            "unit": "ns",
            "range": "± 287360.1802251896"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 917168.7904871461,
            "unit": "ns",
            "range": "± 287073.97983409424"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 912642.6304521809,
            "unit": "ns",
            "range": "± 267162.22748073656"
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
          "id": "a855098670796cafc9dd258e51c58bea16138d1e",
          "message": "{misc} renaming CLI invocation from azv2 --> az (#269)\n\n## Summary\n- Rename the CLI executable and app execution alias from `azv2` to `az`\n- Update agent/TUI invocation handling, help validation, scaffolding,\ntests, and snapshots to use `az`\n- Refresh docs, wiki content, workflows, VS Code launch config, and help\nexamples for the new invocation\n\n## Validation\n- `dotnet build cirrus.slnx`\n- `dotnet test cirrus.slnx --no-build`\n- `.\\artifacts\\Debug\\Core.Cli\\az.exe --help`\n- `dotnet run --project\ntools\\Help\\Cirrus.Help.Tool\\Cirrus.Help.Tool.csproj -- --artifacts-root\nartifacts\\Debug --output-root artifacts\\help`\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-24T13:12:39+10:00",
          "tree_id": "afe7ebbe661796d66b54d64a7b2960bba5b3b72f",
          "url": "https://github.com/Azure/cirrus/commit/a855098670796cafc9dd258e51c58bea16138d1e"
        },
        "date": 1782270871462,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 943239.1162697618,
            "unit": "ns",
            "range": "± 283085.65937501384"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 925651.3873323995,
            "unit": "ns",
            "range": "± 272097.6318199296"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 908110.0238095238,
            "unit": "ns",
            "range": "± 263840.0404651693"
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
          "id": "22f32b17625933459bfffb8c63ff86e75d59a29f",
          "message": "Add source files (#315)",
          "timestamp": "2026-06-24T22:09:36+10:00",
          "tree_id": "2d5d60679fcacc1e301f3d141f4755d650fd09c3",
          "url": "https://github.com/Azure/azclips/commit/22f32b17625933459bfffb8c63ff86e75d59a29f"
        },
        "date": 1782303089229,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 954541.1256754053,
            "unit": "ns",
            "range": "± 277658.3199035541"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 931054.0089044522,
            "unit": "ns",
            "range": "± 284360.44036941126"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 919918.3864705294,
            "unit": "ns",
            "range": "± 271502.74461024575"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "wangalex@microsoft.com",
            "name": "Alex Wang",
            "username": "Alex-AZPS"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "8f92b95c0a178fd834745e5df505730f1f17de80",
          "message": "chore: add MIT license headers to source files (#317)\n\nAdds the standard two-line MIT header to every source file under src/\nand tests/ on top of latest main.\n\n- 406 source files total (.cs/.ps1/.psm1), excluding generated obj/bin\n- 335 files updated; 71 already had a header (skipped)\n- .cs uses // comments, .ps1/.psm1 use # comments\n\nDraft for content review (#8). Header text:\n\n    // Copyright (c) Microsoft Corporation.\n    // Licensed under the MIT License.\n\nCo-authored-by: copilot-swe-agent[bot] <198982749+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-24T10:00:20-07:00",
          "tree_id": "43ce7d8afc065cdf63d21c8b9ff3bc9161263127",
          "url": "https://github.com/Azure/azclips/commit/8f92b95c0a178fd834745e5df505730f1f17de80"
        },
        "date": 1782320536214,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 953452.5882529517,
            "unit": "ns",
            "range": "± 284116.37254852173"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 929160.6081432573,
            "unit": "ns",
            "range": "± 279335.0063475178"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 902370.8129251701,
            "unit": "ns",
            "range": "± 269932.83545513166"
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
          "id": "0efdcbc00aee2f645e2753e05a5c1ab8ee831877",
          "message": "Rename login extension PowerShell cmdlets (#319)\n\nThis change updates the login extension cmdlets to match `classic` Azure\nPowerShell cmdlet names.\n\n### Testing\n- Verified manual tests against all cmdlets to confirm interactive flow.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-25T11:09:31+08:00",
          "tree_id": "9bb9a3871bf09924cacbc0617d0b91a659338b57",
          "url": "https://github.com/Azure/azclips/commit/0efdcbc00aee2f645e2753e05a5c1ab8ee831877"
        },
        "date": 1782357084971,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 994789.006402561,
            "unit": "ns",
            "range": "± 289197.13063842925"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 971263.843937575,
            "unit": "ns",
            "range": "± 294604.280518428"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 962468.4697348675,
            "unit": "ns",
            "range": "± 286816.5426777434"
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
          "id": "ccf6a9ba1bd4be52ee5ab5114063a7d6e82044fa",
          "message": "{Core/Help/Extensions} move help/extension APIs runtime usage of `DiscoverySession` (#310)\n\n## Summary\nAlign extension discovery/registry behavior with the preferences-only\nmodel and resolve review feedback across loader, dispatch, pin/unpin,\ndocs, and remove-path robustness.\n\n## What changed\n- Extension loading: deterministic installed-version enumeration (one\nresolved version per package), improved prefix handling, and\nstream-based builtin root assembly load.\n- Dispatch/registry integration: discovery-backed leaf index now honors\nDI-provided registry stores; leaf de-dupe is case-insensitive.\n- Pin/unpin: seed missing registry entries with preference fields only\n(no runtime-derived contributions/namespaces persisted).\n- Discovery-backed registry: removed redundant nullable handling and\nadded fallback derivation of namespaces/verb from discovered command\nleaves when needed.\n- Remove command: strengthened structured error wrapping for additional\ndelete failure modes.\n- Docs/spec: updated extension/help/discovery docs to match current\nruntime behavior.\n\n## Validation\n- Targeted test runs passed for updated areas, including:\n  - `CliExtensionServiceRegistrationTests`\n  - `BuiltinExtensionLoaderTests`\n  - `ExtensionLoadContextDirectoryTests`\n  - `ExtensionPinTests`\n  - `ExtensionRemoveTests`\n\nAll non-self unresolved review threads on this PR have been addressed\nand resolved.",
          "timestamp": "2026-06-25T17:01:04+10:00",
          "tree_id": "6638532ad5c1979c829f8f087836fdbc423161ec",
          "url": "https://github.com/Azure/azclips/commit/ccf6a9ba1bd4be52ee5ab5114063a7d6e82044fa"
        },
        "date": 1782370990915,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1055459.1051420567,
            "unit": "ns",
            "range": "± 318170.8703134094"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1026607.2128851541,
            "unit": "ns",
            "range": "± 317430.10247270955"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1017589.9659897969,
            "unit": "ns",
            "range": "± 310985.25613211095"
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
          "id": "c80affb55b485531d20276bc88a4a63f48a8f986",
          "message": "{Core} Fix/cli log scope propagation (#313)\n\nAfter: https://github.com/Azure/cirrus/pull/300\n\nThis PR fixes CLI logging-scope propagation so help paths respect the\nsame effective logging policy as normal command execution.\n\nWhat changed:\n- regular CLI commands continue to respect user config and\n`CIRRUS_LOG_LEVEL`\n- CLI `help` now reuses the same logging streams/policy during\ndiscovery, so `az --help`, `az vm --help`, etc. emit logs consistently\nwhen the effective level allows it\n- PowerShell `Get-Help` is treated as out of scope because it is host\nhelp/metadata retrieval, not our cmdlet execution path\n\nNotes:\n- CLI help still uses a separate discovery/session path internally\n- this PR does not unify help and command execution architecture; it\nmakes the help path honor the same logging policy contract\n\nChecklist:\n- [x] Respects config level set in user config (command CLI)\n- [x] Respects config level set in user config (help CLI)\n- [x] Respects config level set in user config (command PWSH)\n- [x] Env vars override user config (command CLI)\n- [x] Env vars override user config (help CLI)\n- [x] Env vars override user config (command PWSH)\n- [x] `-debug` overrides all (command CLI)\n- [x] `-debug` overrides all (help CLI)\n- [x] `-debug` overrides all (command PWSH)\n- [x] Default logging to error",
          "timestamp": "2026-06-29T11:09:58+10:00",
          "tree_id": "8c96729ca4b8a0e634f3fbf428f3638111e2aa65",
          "url": "https://github.com/Azure/azclips/commit/c80affb55b485531d20276bc88a4a63f48a8f986"
        },
        "date": 1782695513742,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 985200.9147562318,
            "unit": "ns",
            "range": "± 305760.48130991985"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 964783.6848794156,
            "unit": "ns",
            "range": "± 333790.9138542703"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 963662.8103862318,
            "unit": "ns",
            "range": "± 294308.87062518916"
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
          "id": "65acccb8220cb55fc7aa7f5439b9915ebcc298b7",
          "message": "refactor(extensions): rename Cirrus to Azclips for Resources, Compute, ExtensionRuntime (#330)\n\nScoped Cirrus→Azclips rename of three extension projects, as a\nprerequisite for private-preview packaging. Project folder/.csproj file\nnames unchanged; CLI executable stays `az`.\n\n**Renamed identifiers**\n- `Microsoft.Azure.Cirrus.Extensions.Resources` →\n`Microsoft.Azure.Azclips.Extensions.Resources`\n- `Microsoft.Azure.Cirrus.Extensions.Compute` →\n`Microsoft.Azure.Azclips.Extensions.Compute`\n- `Microsoft.Azure.Cirrus.ExtensionRuntime` →\n`Microsoft.Azure.Azclips.ExtensionRuntime`\n\n**Compatibility:** the rest of the repo keeps its `Cirrus` naming and\nstill builds — only minimal references TO the renamed assemblies were\nre-pointed (`Core.Engine/Engine.Subscriptions.cs` using, Core.Runtime\nusings, `PS/{Resources,Compute}.psd1` dll paths, packaging nupkg globs).\nNo compat shim needed: in-tree consumers rebuild against the new name;\nRest/Help reach the assemblies via path-based\nProjectReference/reflection. Host dispatch already supports both\n`azclips` and `cirrus` prefixes (Constants.cs / ExtensionHints.targets),\nso verb→packageId→assembly resolution works unchanged.\n\n**Breaking surface (acceptable pre-release):** package-id/assembly-name\nchange for the three assemblies.\n\n**Validation:** `dotnet build cirrus.slnx` clean; Cli.Tests 281 passed/4\nskipped; Core.Extensions.Tests 56 passed. Live Azure smoke (`az group\nlist/show/create`, `az rest` GET/DELETE round-trip, `az vm list`)\nconfirmed all three renamed extensions load & dispatch; PS validated via\nreflection (`Microsoft.Azure.Azclips.Extensions.Resources` loads, 5\nprocessors resolve). New repeatable smoke scripts:\n`tests/Scripts/CLI/AzclipsRenameSmoke.sh`,\n`tests/Scripts/PS/AzclipsRenameSmoke.ps1`.\n\n**Follow-ups (non-blocking, out of scope):**\n- `devops/scripts/packaging/common/inject_builtin_extensions.{sh,ps1}`\nhardcode the old `Microsoft.Azure.Cirrus.Extensions.` prefix for the\nlean profile — pre-existing, not triggered here (trio aren't built-ins);\nresolve when `extension`/`config`/`login` are renamed.\n- Docs/wiki prose and XML-doc `<c>` examples left as-is (optional\ncosmetic).",
          "timestamp": "2026-06-30T10:19:20+10:00",
          "tree_id": "267051c59a3b4d6e6d55a0920fa526aa486b9808",
          "url": "https://github.com/Azure/azclips/commit/65acccb8220cb55fc7aa7f5439b9915ebcc298b7"
        },
        "date": 1782778879731,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 990327.7899378632,
            "unit": "ns",
            "range": "± 305225.5305515031"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 958804.0695347674,
            "unit": "ns",
            "range": "± 300191.1966985131"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 959621.3766629989,
            "unit": "ns",
            "range": "± 291320.6555816142"
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
          "id": "809e425b209e82d6bd6ca7c7a48564ea1cc6ab5b",
          "message": "[Benchmark] Remove API benchmark references in documentation and charts (#332)\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-06-30T11:18:50+10:00",
          "tree_id": "c3b9d1d261c4786565523432157eeaab58c79ccb",
          "url": "https://github.com/Azure/azclips/commit/809e425b209e82d6bd6ca7c7a48564ea1cc6ab5b"
        },
        "date": 1782782444875,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 981380.8405565008,
            "unit": "ns",
            "range": "± 315302.64332059934"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 965500.5187593797,
            "unit": "ns",
            "range": "± 314057.22627090645"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 962827.4700880352,
            "unit": "ns",
            "range": "± 301266.71641868853"
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
          "id": "4e7e1c2ca8f3e138bcfcc1b32b22ffb884e1ba81",
          "message": "Clips 432 update module naming (#334)\n\n## Summary\n\nThis PR updates Config PowerShell cmdlet naming to the Az naming pattern\nwhile preserving CLI config verb behavior.\n\n## Delivered\n\n- Renamed Config PowerShell cmdlets to:\n  - Get-AzConfig\n  - Set-AzConfig\n  - Remove-AzConfig\n  - Get-AzConfigList\n  - Get-AzConfigInfo\n- Updated Config extension metadata mappings for PowerShell command\nnames.\n- Updated Config module exports (`CmdletsToExport`) to the new Az cmdlet\nnames.\n- Updated Config PowerShell help markdown filenames/content to match new\ncmdlet names.\n- Updated config-related tests and documentation references from\n`*CirrusConfig*` to `*AzConfig*` where applicable.\n- Manually aligned agent help snapshot files for Config PowerShell\nnames:\n  - Agent/Tools/Help/HelpIndex.json\n  - Agent/Tools/Help/HelpManifest.json\n- Updated extension help docs verbiage to neutral CLI wording only (no\nAPI/package identity changes).\n\n## Validation\n\nLocal validation performed:\n\n- PowerShell cmdlet regression runbook completed (import/export, CRUD,\nJSON, WhatIf, unknown key validation, env precedence).\n- CLI parity sanity checks completed (`az config\nset/get/list/info/delete`) with expected behavior.\n- Local help generation executed (`build/build.ps1 -Configuration Debug\n-GenerateHelpArtifacts`), and generated Config MAML reflects\n`Get-AzConfig*` names.\n\n## Not In Scope / Pending\n\n- Agent help artifact lifecycle/design remains pending as a follow-up\narchitecture change.\n- This PR keeps extension/package/runtime binary identities unchanged\n(e.g., `Microsoft.Azure.Cirrus.*`).\n\n## Notes\n\n- Config MAML output under `src/PS/Config/en-US/` is generated/ignored\nin source control.",
          "timestamp": "2026-06-30T13:39:20+10:00",
          "tree_id": "12cf0f503312ddc11b0dcea00411234d7be6e2dd",
          "url": "https://github.com/Azure/azclips/commit/4e7e1c2ca8f3e138bcfcc1b32b22ffb884e1ba81"
        },
        "date": 1782790878176,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 998051.3578289144,
            "unit": "ns",
            "range": "± 323056.1808553954"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 971936.2774109644,
            "unit": "ns",
            "range": "± 310430.65449584386"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 961605.9523261631,
            "unit": "ns",
            "range": "± 317681.828153435"
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
          "id": "8f411a0f51f435858899bfaf818a310c7e997fcc",
          "message": "Update references to AzclipsServiceProvider (#339)\n\nRename service provider to `AzclipsServiceProvider`",
          "timestamp": "2026-07-01T16:52:02+10:00",
          "tree_id": "5d5f3e08b59af05ef6692b537e010ff5f0fab19b",
          "url": "https://github.com/Azure/azclips/commit/8f411a0f51f435858899bfaf818a310c7e997fcc"
        },
        "date": 1782888840531,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1001900.7730092037,
            "unit": "ns",
            "range": "± 305424.7214313026"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 965712.1516606642,
            "unit": "ns",
            "range": "± 318804.3309167145"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 966081.0515206082,
            "unit": "ns",
            "range": "± 318026.11406262673"
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
          "id": "07a42b3362275f35515e755bd917f00c72b33a85",
          "message": "{Core.Configuration} Update dependency versions (#343)\n\n## Overview\nAlign Core.Configuration package versions with the repo's net10 baseline\nby upgrading Microsoft.Extensions.* references from 9.0.0 to 10.0.0.\n\n## What Changed\n- Updated the following in\nsrc/Core/Core.Configuration/Core.Configuration.csproj:\n  - Microsoft.Extensions.Configuration: 9.0.0 -> 10.0.0\n  - Microsoft.Extensions.Configuration.Binder: 9.0.0 -> 10.0.0\n  - Microsoft.Extensions.Configuration.Json: 9.0.0 -> 10.0.0\n- Microsoft.Extensions.DependencyInjection.Abstractions: 9.0.0 -> 10.0.0\n\n## Why\nCore.Configuration was the only Core project still pinned to 9.x while\nthe repo targets net10 and sibling Core projects already use 10.0.0\nMicrosoft.Extensions dependencies.\n\n## Validation\n- dotnet build cirrus.slnx\n- dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter\n\"FullyQualifiedName~Config\" (42 passed)\n- Manual PowerShell regression: Get-AzConfig / Set-AzConfig /\nRemove-AzConfig (including -WhatIf and confirmation)\n- Manual CLI regression: az config list/get/set/delete flows",
          "timestamp": "2026-07-01T15:47:44+08:00",
          "tree_id": "875458f2e3bd119fc327bf5d011a5b576dcb9849",
          "url": "https://github.com/Azure/azclips/commit/07a42b3362275f35515e755bd917f00c72b33a85"
        },
        "date": 1782892185739,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 981203.1022715901,
            "unit": "ns",
            "range": "± 306285.04002268636"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 951833.856942777,
            "unit": "ns",
            "range": "± 309480.15313557774"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 952335.944961473,
            "unit": "ns",
            "range": "± 303773.43473078066"
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
          "id": "f60c1316ee19021c1b69eca2e159a6033dcace3f",
          "message": "refactor(agent): rename cirrus to azclips across agent-owned code (#348)\n\n## Summary\n\nRenames `cirrus` → `azclips` across code owned by the Agent squad, as\npart\nof the repo-wide rebrand. This PR only touches Agent-owned code; the\ncore-layer paths and PowerShell cmdlet renames are being handled\nseparately by the owning squads.\n\nThe changes fall into two groups:\n\n**1. Identifier / path / text rename** (`cirrus` → `Azclips` /\n`azclips`):\n\n- Assembly and trace identifiers (`cirrus-agent` → `azclips-agent`,\n  `Cirrus.Agent` → `Azclips.Agent`)\n- Agent-owned user paths (`~/.cirrus/arm-cache/` →\n`~/.azclips/arm-cache/`;\n  Copilot SDK config dir `.cirrus` → `.azclips`)\n- LLM-facing text — the three prompts (`oneshot.md` / `inline.md` /\n  `tui.md`) now say \"Azclips\" in the title and self-intro, and use\n  `azclips` in error text (e.g. `the command isn't in azclips`)\n- Tool descriptions (`HelpTools`, `HelpIndex.json`, `HelpManifest.json`)\n- Environment probe field `CirrusVersion` → `AzclipsVersion`\n- Builtin-provider APIM route `.../cirrus/openai/` →\n`.../azclips/openai/`\n  (**⚠️ needs matching APIM redeploy**, see azure-client-tools-copilot\n  PR #1867)\n- New `agent.provider` row in `docs/howto/config.md` and matching\n  `ConfigurationSchema.cs` description\n\n**2. Hard-rule sentence added to all three prompts** so the LLM doesn't\napply its classic Azure CLI training knowledge to our `az`:\n\n> This `az` is Azclips — Microsoft's next-gen Azure CLI, NOT the classic\n> Azure CLI (azure-cli). Your training knowledge about classic `az`\n> (parameter names, defaults, which commands exist) may be wrong here.\n> Verify every command with `get_command()`; never assume classic-CLI\n> syntax.",
          "timestamp": "2026-07-02T11:07:42+10:00",
          "tree_id": "ffaa1804b30fc83a87d220c12a6a8d4435599567",
          "url": "https://github.com/Azure/azclips/commit/f60c1316ee19021c1b69eca2e159a6033dcace3f"
        },
        "date": 1782954575902,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 997685.4916458229,
            "unit": "ns",
            "range": "± 302113.05112099997"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 973143.7739095638,
            "unit": "ns",
            "range": "± 318856.7331623335"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 961784.3901560624,
            "unit": "ns",
            "range": "± 315853.3148462651"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "11371776+isra-fel@users.noreply.github.com",
            "name": "Yeming Liu",
            "username": "isra-fel"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "3a1ff62a9ba0084f39f969bd0fcb689df1d58d3c",
          "message": "Profile Management (#345)\n\nDid a bunch of refactoring and renaming in this PR. **The bigger ones\nare:**\n\n1. Remove the concept of \"auth context\" and replace it with \"profile\".\nIt's still plugged in to the engine's PrcoessorContext so the impact to\nextensions is minimum.\n2. Introduce the first global parameter \"profile\"\n([src/Core/Core.Engine/ExecutionSession.cs](https://github.com/Azure/azclips/pull/345/changes#diff-7e093324d974634575c98157b1331962bfa392fe085ff8b44fabc144a7026b2aR243)).\nIdea is engine will extract it before handing over to individual\nprocessrors.\n\n**On top of that:**\n1. Added CRUD commands for profile, including `--out-profile` parameter\nof `az login` that allows you to create profiles when login.\n2. Added `az profile set` command to switch the active profile\n3. Added `--profile` global parameter to override profile for one single\ncommand.\n\n**Smaller changes:**\n1. Made it possible to run \"az logout\" instead of \"az login logout\"\nthanks to @DanielMicrosoft !\n2. Checked in the design, spec, and skills of [openspec\ntool](https://github.com/Fission-AI/OpenSpec).\n\n**Todo (in follow up PRs)**\n1. wire up profile with resources and compute so that instead of being\nmandatory, \"--subscription-id\" will default to what's in profile.\n5. benchmarking\n6. telemetry of auth type\n7. Extend the global parameter solution to be generic enough for any\nglobal parameters such as --debug\n\n\n\n\n\n\n\nFOllowing is generated by AI.\n---------------\nThis pull request introduces a comprehensive set of prompt files that\ndefine and document the workflows for several key OpenSpec operations.\nThese markdown prompts provide detailed, step-by-step guidance and\nguardrails for proposing new changes, applying tasks, archiving\ncompleted changes, syncing delta specs, and entering explore mode. The\nprompts are designed to clarify user and agent responsibilities, enforce\nbest practices, and ensure consistency across experimental workflows.\n\n**New OpenSpec workflow prompt files:**\n\n*Change proposal and implementation:*\n- Added `.github/prompts/opsx-propose.prompt.md`: Defines the process\nfor proposing a new change, including artifact creation order,\ndependency handling, and user interaction for unclear contexts.\n- Added `.github/prompts/opsx-apply.prompt.md`: Details the steps for\nimplementing tasks from an OpenSpec change, including task selection,\nprogress tracking, and handling of ambiguous or blocked tasks.\n\n*Change completion and archiving:*\n- Added `.github/prompts/opsx-archive.prompt.md`: Outlines the procedure\nfor archiving a completed change, with checks for artifact and task\ncompletion, delta spec sync assessment, and user confirmation for\nincomplete items.\n\n*Spec synchronization:*\n- Added `.github/prompts/opsx-sync.prompt.md`: Specifies the\nagent-driven workflow for syncing delta specs to main specs, including\nintelligent merging logic and output summaries.\n\n*Exploration and discovery:*\n- Added `.github/prompts/opsx-explore.prompt.md`: Introduces an \"explore\nmode\" for deep investigation, idea development, and requirements\nclarification, with explicit guardrails against implementation actions.",
          "timestamp": "2026-07-02T15:00:13+10:00",
          "tree_id": "c822af1da9410ea0e2e3922ab04071ab8c2f3a4e",
          "url": "https://github.com/Azure/azclips/commit/3a1ff62a9ba0084f39f969bd0fcb689df1d58d3c"
        },
        "date": 1782968578065,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 989148.4935454818,
            "unit": "ns",
            "range": "± 304250.0536095005"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 963576.5094028208,
            "unit": "ns",
            "range": "± 315944.7269578828"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 962941.9419325797,
            "unit": "ns",
            "range": "± 306425.74728714547"
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
          "id": "306335a984a2c77f523ca80d83c5020205a1b9e0",
          "message": "[Telemetry] Update product references to Azclips (#347)\n\nUpdate product references to `azclips` naming scheme",
          "timestamp": "2026-07-02T15:11:02+10:00",
          "tree_id": "b8d4f3997c810767fe22ab7d20ea6f0ab77ead76",
          "url": "https://github.com/Azure/azclips/commit/306335a984a2c77f523ca80d83c5020205a1b9e0"
        },
        "date": 1782969177857,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 990959.2105473832,
            "unit": "ns",
            "range": "± 309223.8420776027"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 958028.4078881427,
            "unit": "ns",
            "range": "± 296003.65334993566"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 964724.9735841505,
            "unit": "ns",
            "range": "± 303251.95769869507"
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
          "id": "c887db8e32c01e01827031796b7803df553aaa1f",
          "message": "{Extensions/Config} Change ext/config housing from `.cirrus` to `.azclips` (#349)\n\n## Summary\n\nSwitch default user-scoped extension/config/auth paths from `.cirrus` to\n`.azclips` (hard cutover).\n\n## What Changed\n\n- Added shared home-path helper (`HomePaths`) and replaced scattered\ndefault-path literals.\n- Extension defaults now resolve to `~/.azclips/extensions`\n(install/discovery/registry defaults).\n- Config/auth/trace/cache defaults moved to `.azclips`-based paths.\n- Renamed extension module identity from\n`Microsoft.Azure.Cirrus.Extensions.Extension` to\n`Microsoft.Azure.Azclips.Extensions.Extension`.\n- Updated in-scope docs/wiki references from `.cirrus` to `.azclips`.\n\n## Non-Goals\n\n- No migration from existing `~/.cirrus/*` state.\n- No dual-read/fallback behavior.\n\n## Validation\n\n- Automated: extension/config/agent targeted test slices passed.\n- Manual: isolated runtime validation from `C:/temp/clips-test`\nconfirmed `az extension add --path ...` installs under\n`~/.azclips/extensions` and commands resolve from that location.\n\n## Deferred Follow-up\n\n- Coupling registry default-path derivation directly to\n`assemblyLoading.versionedInstallationDir` overrides.\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-02T15:13:50+10:00",
          "tree_id": "f8e3036eb07affaec2fbd255505ed76928af040e",
          "url": "https://github.com/Azure/azclips/commit/c887db8e32c01e01827031796b7803df553aaa1f"
        },
        "date": 1782969444095,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1004556.4822893736,
            "unit": "ns",
            "range": "± 315731.4592751774"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 975919.6433573429,
            "unit": "ns",
            "range": "± 318775.36664201424"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 983982.718302812,
            "unit": "ns",
            "range": "± 304934.1717821689"
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
          "id": "9b0c9975e52c1cbdf320bd25fe86f0b702c3b128",
          "message": "Update references solution references to azclips.slnx (#353)\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-02T14:40:04+08:00",
          "tree_id": "68018e48dbcff7df164eb65e8a3c07aebbdb96ad",
          "url": "https://github.com/Azure/azclips/commit/9b0c9975e52c1cbdf320bd25fe86f0b702c3b128"
        },
        "date": 1782974530940,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1001805.5434217108,
            "unit": "ns",
            "range": "± 314222.4359754322"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 964545.1259503801,
            "unit": "ns",
            "range": "± 311271.7714205085"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 973128.3201280512,
            "unit": "ns",
            "range": "± 303657.08445360896"
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
          "id": "8adcc5bb87b20fb41e813c85688129bf358c47b3",
          "message": "Update root branding references to Azclips (#357)\n\n## Summary\n- Update root-level documentation branding from Cirrus to Azclips\n- Update workflow display/output names to Azclips\n- Remove obsolete legacy-name and AZv2 reference guard workflows\n\n## Validation\n- `dotnet build azclips.slnx`\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-02T17:36:11+10:00",
          "tree_id": "93971f01756ed4c27bce28521c1de5955a0731c7",
          "url": "https://github.com/Azure/azclips/commit/8adcc5bb87b20fb41e813c85688129bf358c47b3"
        },
        "date": 1782977886783,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1003594.7282184656,
            "unit": "ns",
            "range": "± 314709.5159519599"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 975616.8819409704,
            "unit": "ns",
            "range": "± 301578.8753855822"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 975042.0506253126,
            "unit": "ns",
            "range": "± 302166.58255797497"
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
          "id": "1a2eea436822b9d3787252d6a2783d4c06764773",
          "message": "feat(completions): switch to dynamic runtime discovery (#340)\n\nShell completion scripts now invoke 'az --completions [group] [sub]' at\ntab time instead of reading a static JSON file. This ensures completions\nalways match whichever az binary is on PATH.\n\n- Refactored az.completions.ps1, .bash, .zsh to dynamic mode\n- Removed JSON generation step from build/build.ps1\n- Updated README to reflect new architecture\n- Resolver walks up to repo root to find artifacts/*/Core.Cli/az.exe\n\n---------\n\nCo-authored-by: Julie Zhu <105691024+yanzhudd@users.noreply.github.com>",
          "timestamp": "2026-07-02T22:07:21+10:00",
          "tree_id": "7e60adfc708b3a99235bce702fb53674d938d3b3",
          "url": "https://github.com/Azure/azclips/commit/1a2eea436822b9d3787252d6a2783d4c06764773"
        },
        "date": 1782994177888,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 988151.5639947964,
            "unit": "ns",
            "range": "± 308086.98982445255"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 973881.8281984596,
            "unit": "ns",
            "range": "± 322257.72871519567"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 966701.9544726836,
            "unit": "ns",
            "range": "± 310291.3821997783"
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
          "id": "4409cbd1324c7716337eb9baab21a317df3ccb40",
          "message": "[Benchmark] Update product references to azclips (#350)\n\nUpdate product references to `azclips`.",
          "timestamp": "2026-07-03T09:24:09+10:00",
          "tree_id": "85c82e18273a56554039a03f9ea4647901b6f5da",
          "url": "https://github.com/Azure/azclips/commit/4409cbd1324c7716337eb9baab21a317df3ccb40"
        },
        "date": 1783034768070,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1004170.4153492096,
            "unit": "ns",
            "range": "± 310031.21424109593"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 964690.4996998799,
            "unit": "ns",
            "range": "± 312666.2253199042"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 967461.0639255702,
            "unit": "ns",
            "range": "± 309974.8775634121"
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
          "id": "f4fe46ee722a53732d3fa008589c23ee759664ad",
          "message": "{Help/Config} Update internals from `Cirrus` to `AzClips` (#354)\n\n## Summary\n- Renamed scoped Help/Config/Extension-management internals from\n`Cirrus` to `AzClips`.\n- Completed help tooling rename to `tools/Help/Azclips.Help.Tool` (+\ntests) and updated solution/build/workflow references.\n- Finalized `Invoke-AzclipsRestMethod` hard-cut rename across cmdlet\nexport, extension metadata, and help markdown.\n- Resolved help validation regressions (`login/show.md` orphan,\n`Invoke-CirrusRestMethod.md` mismatch).\n- Fixed help tool runtime loader conflict by deduplicating assembly\nloads by identity.\n- Migrated PowerShell cmdlet implementation namespaces/usings in\n`src/PS/**/Cmdlets` from `Microsoft.Azure.Cirrus.Cmdlets.*` to\n`Microsoft.Azure.Azclips.Cmdlets.*`.\n\n## Key Compatibility Decisions\n- `Invoke-CirrusRestMethod` compatibility alias was **not** retained\n(intentional hard cutoff).\n\n## Validation\n- `dotnet build azclips.slnx` succeeded.\n- `dotnet test tests/Cli.Tests/Cli.Tests.csproj` succeeded.\n- `dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj` succeeded.\n- `dotnet test\ntools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj`\nsucceeded.\n- `dotnet run --project\ntools/Help/Azclips.Help.Tool/Azclips.Help.Tool.csproj --configuration\nDebug -- --artifacts-root artifacts/Debug --output-root artifacts/help`\nsucceeded (`Help validation: no issues`).",
          "timestamp": "2026-07-03T09:38:54+10:00",
          "tree_id": "a6c71bcea53c9c70fad23a7823adb53d61598d2a",
          "url": "https://github.com/Azure/azclips/commit/f4fe46ee722a53732d3fa008589c23ee759664ad"
        },
        "date": 1783035670281,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1001379.0852511507,
            "unit": "ns",
            "range": "± 326357.4974230405"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 978143.1548774387,
            "unit": "ns",
            "range": "± 323564.80797810643"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 974053.5937562537,
            "unit": "ns",
            "range": "± 297907.90939761535"
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
          "id": "771945186cd60bba8c6b0c4f7e5390d7b4229a90",
          "message": "{Refactor} Cirrus reference sweep.  (#361)\n\n## Summary\nThis PR completes a repository-wide Cirrus-to-Azclips reference sweep\nacross targeted source, tests, scripts, and help tooling.\n\n## What Changed\n- Migrated remaining Cirrus identifiers, comments, docs, and test\nliterals to Azclips equivalents.\n- Removed legacy Cirrus extension package prefix handling in CLI/Core\npaths and aligned tests accordingly.\n- Renamed fixture/help/cmdlet file and project names that still carried\nCirrus naming.\n- Updated deployment and REST test scripts to Azclips command/name\nusage.\n- Completed final filename sweep for covered directories.\n\n## Validation\n- Targeted Release builds and test runs were executed during each\nmigration step.\n- Final content and filename sweeps for covered directories report zero\nremaining Cirrus references.",
          "timestamp": "2026-07-03T10:56:26+10:00",
          "tree_id": "52cffd022a65a3683e380f6cc37fc07228d45433",
          "url": "https://github.com/Azure/azclips/commit/771945186cd60bba8c6b0c4f7e5390d7b4229a90"
        },
        "date": 1783040299854,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 967157.7692846423,
            "unit": "ns",
            "range": "± 288802.0554822457"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 960184.6502601041,
            "unit": "ns",
            "range": "± 308450.04343325394"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 947124.6064851881,
            "unit": "ns",
            "range": "± 292468.08033741993"
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
          "id": "fe041d873eaa80de250686efc5591f13aec9f2d1",
          "message": "[Autocomplete] Remove completion caching from bash and PowerShell scripts (#366)\n\nStandardize tab completion behavior between supported shells\n(Powershell, bash, zsh) to always make a hard query for autocompletion\nbehaviour.\n\n- Remove top level group caching for bash\n- Remove top level and sub command caching for Powershell\n\nCo-authored-by: Copilot App <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-03T14:02:50+10:00",
          "tree_id": "874956da7a3d42854429460d36c025897abfeaa0",
          "url": "https://github.com/Azure/azclips/commit/fe041d873eaa80de250686efc5591f13aec9f2d1"
        },
        "date": 1783051494053,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 976984.4972989196,
            "unit": "ns",
            "range": "± 304889.55972016347"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 946239.7997998999,
            "unit": "ns",
            "range": "± 288158.7535398737"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 957171.9013408045,
            "unit": "ns",
            "range": "± 299496.0651528612"
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
          "id": "e4dfe45d3635b76834b16286dfa1ed7174b7b4c1",
          "message": "feat(http): stamp User-Agent header on every outgoing ARM request (#337)\n\n- Add UserAgentStep to IHttpPipelineService interface with no-op default\n- CLI: AZURECLI/{version} dotnet/{runtime}, version from assembly\nmetadata\n- PS: AzurePowershell/v{version} PSVersion/v{psVersion}, version from\nown assembly\n- Wire UserAgentStep into Engine.CreateProcessorContext HTTP pipeline\n- Register CLIHttpPipelineService and PSHttpPipelineService via DI\noverrides",
          "timestamp": "2026-07-03T15:14:06+10:00",
          "tree_id": "b4417df7c28f9b6dc65ffee2f4f9b58cf32edf90",
          "url": "https://github.com/Azure/azclips/commit/e4dfe45d3635b76834b16286dfa1ed7174b7b4c1"
        },
        "date": 1783055763788,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 980229.1384830899,
            "unit": "ns",
            "range": "± 308007.42560315604"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 962949.5599359616,
            "unit": "ns",
            "range": "± 308064.1218003432"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 946984.7702391674,
            "unit": "ns",
            "range": "± 304891.80315719516"
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
          "id": "b9825821b0cb37f399afb0ca358b446fdcfe9420",
          "message": "{DevOps} Enable azclips release pipelines (#367)\n\n## Summary\n- Port azclips Azure Pipelines and devops release scripts for build,\npackage, signing, release, NOTICE, and PowerShell module flows.\n- Add release metadata needed by stable release automation, including Az\nrollup manifest and module changelogs.\n- Enable Release delay-signing/public-key friend assemblies so ESRP\nstrong-name signing can process first-party payloads.\n- Fix common packaging restore to use the azclips solution name.\n\n## Local validation\n- Bash syntax check for devops shell scripts.\n- PowerShell parser check for build/devops scripts.\n- `git diff --check`.\n- `build/build.ps1 -Configuration Release -GenerateHelpArtifacts`.\n- Packed Az PowerShell modules and validated import/help from the local\ngallery.\n- Built Linux/macOS/Windows x64/arm64 lean/full package artifacts\nlocally.\n- Ran Linux tarball runtime smoke test with bundled and optional\nextensions.\n- Created component archives including `azcli-extensions-0.1.0.tar.gz`\nand `azps-cmdlets-0.1.0.tar.gz`.\n- Verified 19 first-party DLLs inside PowerShell module nupkgs carry\nstrong-name token `31bf3856ad364e35`.\n\n## Notes\n- ESRP/AuthentiCode signing itself was not run locally; signing jobs use\n`windows-latest`, while local Linux PowerShell does not provide\n`Get-AuthenticodeSignature`.",
          "timestamp": "2026-07-03T16:22:15+10:00",
          "tree_id": "0074534c7b557459217687294cd967b1114c443d",
          "url": "https://github.com/Azure/azclips/commit/b9825821b0cb37f399afb0ca358b446fdcfe9420"
        },
        "date": 1783059850557,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 986564.1731038623,
            "unit": "ns",
            "range": "± 301496.5279686316"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 956896.281312525,
            "unit": "ns",
            "range": "± 299273.9247947115"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 948451.1577946768,
            "unit": "ns",
            "range": "± 289620.07570295926"
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
          "id": "3ffe63a8ea2ac1c478e02ab9463760eb2d7fc4ea",
          "message": "[Autocomplete] Add configuration to enable/disable autocompletion for shells. (#368)\n\nAdd config for user to enable/disable autocompletion for azclips\nsupported shells.\n1. Add `completion.enabled` config value to be set by `az config set ...\n` commands\n2. Wire in config value read in CompletionGenerator to return early is\nautocomplete is disabled.\n3. autocompletion defaults to `enabled`\n\nValidated with the following:\n\n1. New shell session\n2. source the scripts into the profile session i.e source\n./bin/az.completions.bash or . ./bin/az.completions.ps1\n3. az config set --key completion.enabled --value true / false in the\nsession\n4. then test by pressing tab on the az executable\n\n---------\n\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-03T16:30:59+10:00",
          "tree_id": "6da72ec092adab6fe51e46edac6b8a43800207ed",
          "url": "https://github.com/Azure/azclips/commit/3ffe63a8ea2ac1c478e02ab9463760eb2d7fc4ea"
        },
        "date": 1783060373039,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 984802.9106053027,
            "unit": "ns",
            "range": "± 312406.4706442713"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 945100.7760208166,
            "unit": "ns",
            "range": "± 298298.1249937122"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 940588.7739095638,
            "unit": "ns",
            "range": "± 297155.7495642027"
          }
        ]
      },
      {
        "commit": {
          "author": {
            "email": "198982749+Copilot@users.noreply.github.com",
            "name": "Copilot",
            "username": "Copilot"
          },
          "committer": {
            "email": "noreply@github.com",
            "name": "GitHub",
            "username": "web-flow"
          },
          "distinct": true,
          "id": "d9ef8536f950fbc0a66b35010c26506b2aea865e",
          "message": "Document CommandMetadata properties with inline XML docs (#341)\n\nAdds XML documentation to the `CommandMetadata` record in\n`src/Core/Core.Abstractions/Models/CommandMetadata.cs`, which previously\nhad no property-level docs. Examples and semantics are drawn from real\nusages (`ExtensionIndex` declarations, `DiscoverySession`,\n`ExecutionSession`) and the intent behind [PR\n#307](https://github.com/Azure/azclips/pull/307).\n\n### Changes\n- **Record summary** — describes `CommandMetadata` as the per-command\ndescriptor declared in an extension's `ExtensionIndex` and consumed by\nthe engine (discover/bind/execute) and CLI (help/completions).\n- **Per-property `<param>` docs with examples** for `Name`,\n`InvocationName`, `Aliases`, `FullName`, `AssemblyName`,\n`ProcessorType`, `Parameters`, and `ParameterAliases`.\n- **`Name` vs `InvocationName` distinction** — `Name` is the\ninternal/diagnostic processor type name; `InvocationName` is the\nclient-facing invocation token and the unique command-table key\n(discovery emits a `Dictionary<InvocationName, CommandMetadata>`), kept\nexplicit to allow future `ExtensionIndex` serialization.\n\n```csharp\n/// <param name=\"InvocationName\">\n/// The client-facing invocation token that end users type to run the command, and the unique key under\n/// which the command is registered in the discovered command table...\n/// Example: \"New-ResourceGroup\" (PowerShell verb-noun) or \"Get-ResourceGroup_List\".\n/// </param>\n```\n\nDocumentation-only change; no runtime behavior is affected.\n\n---------\n\nCo-authored-by: copilot-swe-agent[bot] <198982749+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-06T05:25:46Z",
          "tree_id": "3c8c57ede7e5c381931a8ca96a1dee86a71fb332",
          "url": "https://github.com/Azure/azclips/commit/d9ef8536f950fbc0a66b35010c26506b2aea865e"
        },
        "date": 1783315682552,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 982796.6137682609,
            "unit": "ns",
            "range": "± 303337.08455692156"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 956766.6248124063,
            "unit": "ns",
            "range": "± 306756.8771145662"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 932056.1395558224,
            "unit": "ns",
            "range": "± 289407.0138620043"
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
          "id": "d1c6cb4e487f7ac7d79007179818fecd45925ad9",
          "message": "[Benchmark] Stage individual PS manifests during benchmarks (#378)\n\nReworks PSManifestStager so a PS benchmark stages only the module it\nexercises (plus that module's RequiredModules dependency closure) rather\nthan every manifest under src/PS. Running a filtered benchmark (e.g.\njust the Resources benchmarks) now stages just Resources + Login, not\nall 5 modules.\n\nAlso fixes current benchmark pipeline failing.",
          "timestamp": "2026-07-06T16:53:36+10:00",
          "tree_id": "5bebfabaf91e030cc74c728b54b663d7c193e8ef",
          "url": "https://github.com/Azure/azclips/commit/d1c6cb4e487f7ac7d79007179818fecd45925ad9"
        },
        "date": 1783320932916,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 967859.9573201241,
            "unit": "ns",
            "range": "± 296085.4996639198"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 955606.2735641384,
            "unit": "ns",
            "range": "± 302117.7586418257"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 951854.4327798339,
            "unit": "ns",
            "range": "± 287211.4193102153"
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
          "id": "644ee1aacf399c278fbcb4f27e199938890a8fec",
          "message": "feat(completions): skip completion for non-cirrus az binaries (#371)",
          "timestamp": "2026-07-07T00:40:46+10:00",
          "tree_id": "4583a09fc5ba164feb3ff9a1ef955554abcb44e2",
          "url": "https://github.com/Azure/azclips/commit/644ee1aacf399c278fbcb4f27e199938890a8fec"
        },
        "date": 1783348961929,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 970377.375,
            "unit": "ns",
            "range": "± 311079.54096209194"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 967380.7140498349,
            "unit": "ns",
            "range": "± 299137.1517683894"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 953970.9932946357,
            "unit": "ns",
            "range": "± 292850.60027754906"
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
          "id": "44df115e0ba9b22cbb97b12b103615b9613f55e9",
          "message": "{DevOps} Fix GitHub service connection variable (#382)\n\nUpdates the Azure Pipelines shared variables so\n`github_service_connection` uses the configured GitHub service\nconnection name `Azure` instead of `github.com_Azure`.\n\nValidation:\n- Confirmed the branch is based on latest `origin/main`.\n- Confirmed the diff from `origin/main` is limited to\n`.azure-pipelines/templates/variables.yml`.",
          "timestamp": "2026-07-07T09:50:42+10:00",
          "tree_id": "82a0441767a9fc24d23cbb3b3e22da9b51a4aab1",
          "url": "https://github.com/Azure/azclips/commit/44df115e0ba9b22cbb97b12b103615b9613f55e9"
        },
        "date": 1783381955511,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 990227.8925462732,
            "unit": "ns",
            "range": "± 321212.15028415126"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 957464.007853927,
            "unit": "ns",
            "range": "± 294372.8656618687"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 947309.9825930372,
            "unit": "ns",
            "range": "± 299761.45902326785"
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
          "id": "83bc7cebb415bde0a4b8d91ebd77d0827a83b6ad",
          "message": "Trigger stable release 0.0.1 (#385)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.1, aggregates module ChangeLog.md\nentries into CHANGELOG.md and PowerShell module manifests, and refreshes\nroot NOTICE from repository dependency metadata. After this PR merges to\nmain, azclips-trigger-stable-release will run again from the main\nversion change, tag the merged commit as v0.0.1.<UTC yyyyMMddTHHmmssZ>,\nand start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-07T10:38:22+10:00",
          "tree_id": "1765eb455343a4f5368fb095c5206f5ba2c234a7",
          "url": "https://github.com/Azure/azclips/commit/83bc7cebb415bde0a4b8d91ebd77d0827a83b6ad"
        },
        "date": 1783384824875,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 998890.669152237,
            "unit": "ns",
            "range": "± 314033.39202873554"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 956608.9541403825,
            "unit": "ns",
            "range": "± 305453.54270000587"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 949919.5314157079,
            "unit": "ns",
            "range": "± 301254.2915608297"
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
          "id": "91cf737fcfd40804d3ffd5e8dec59b3258e6ee58",
          "message": "fix ps path for bare az.exe (#387)",
          "timestamp": "2026-07-07T12:27:48+10:00",
          "tree_id": "6a7786f4345a43f2ccf800afc3fe991a96988fd6",
          "url": "https://github.com/Azure/azclips/commit/91cf737fcfd40804d3ffd5e8dec59b3258e6ee58"
        },
        "date": 1783391382620,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 981351.1189713828,
            "unit": "ns",
            "range": "± 312254.7505484194"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 947951.003102482,
            "unit": "ns",
            "range": "± 295802.7362799463"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 957947.2902451225,
            "unit": "ns",
            "range": "± 304684.6393161992"
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
          "id": "6cf403c123d51b062f2fe45d35ee8bb2d2c79547",
          "message": "feat(agent): package agent + expose 4 tool commands + refactor ChatAgent (#380)\n\n## Summary\n\nMerges three related workstreams on one branch, all Agent-owned, rebased\nonto latest main:\n\n**1. Decouple agent from TUI + package as `az` tool** (v3-#11)\n- Move `AgentModeEntry` into `Agent/`; CLI now spawns\n`azclips-agent.exe` instead of hosting the agent in-process.\n- Extract `Core.Agent.Tools` class library (assembly\n`Azclips.Agent.Tools`) so the standalone agent and the `az agent`\nextension host share one tools implementation.\n- Scaffold `Agent.Commands` extension: `AgentExtensionIndex` + 4\nprocessors registering `agent tool env` / `agent tool help` / `agent\ntool resource find` / `agent tool resource sku`.\n- Register agent as built-in verb in `BuiltInExtensions.Map`.\n- Packaging: wire the agent exe + extension into azclips zip / tarball\nfor Windows, macOS, Linux.\n- Add 5 command help markdown files.\n\n**2. Expose 4 agent tools as `az agent tool ...` commands** (v3-#21,\npartial)\n- `env` / `help` / `resource find` / `resource sku` — each command's\nJSON output is byte-identical to the corresponding LLM tool return, so\nthe commands double as test entry points and as skill definitions for\nCopilot CLI / Claude Code.\n\n**3. Refactor `ChatAgent.cs`** (R1)\nSplit 1056-line `ChatAgent.cs` into single-purpose files while keeping\nthe public surface identical:\n\n| Extracted | New file | ChatAgent size after |\n|---|---|---|\n| SessionState class | `Agent/SessionState.cs` | 1014 |\n| Failed-command help injection | `Agent/Plan/FailedCommandHelp.cs` |\n941 |\n| Args normalization | `Agent/Common/ArgsNormalizer.cs` | (helper only)\n|\n| propose_plan / submit_response arg parsers |\n`Agent/Plan/PlanArgsParser.cs` | 707 |\n| PreToolUse hook + helpers | `Agent/Hooks/PreToolUseHook.cs` | 413 |\n| Builtin provider wiring | `Agent/Providers/BuiltinProviderConfig.cs` |\n347 |\n\n`ChatAgent.cs`: **1056 → 347 lines** (-67%). Everything left is session\nlifecycle.\n\nAlso fixed a latent `HelpToolsTests` path bug (`Tools/Help/` → `Help/`)\nexposed by the tools-library extraction.",
          "timestamp": "2026-07-07T13:19:52+10:00",
          "tree_id": "178aa868cc86cf75c0a301947e25a24f68139977",
          "url": "https://github.com/Azure/azclips/commit/6cf403c123d51b062f2fe45d35ee8bb2d2c79547"
        },
        "date": 1783394513298,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 974252.9783870322,
            "unit": "ns",
            "range": "± 309516.67769257555"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 966103.8603080924,
            "unit": "ns",
            "range": "± 299142.27365146356"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 953412.422345642,
            "unit": "ns",
            "range": "± 299922.94951401517"
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
          "id": "3a03324449b2f2357507ba3f81bcd01d2e5c10e0",
          "message": "az version implementation (#388) (#389)\n\nRelated command\n`az version`\n\nDescription\nThis PR adds az version command to the AZ CLI, which lets you display\nthe AZ CLI version and environment information\n\nSamples\n\nwindows-x64\n`az version`\n\n```\n{\n  \"azCli\": \"0.1.3\",\n  \"runtime\": {\n    \"dotNet\": \".NET 10.0.9\",\n    \"os\": \"Microsoft Windows 10.0.26200\",\n    \"architecture\": \"X64\"\n  },\n  \"paths\": {\n    \"installLocation\": \"C:\\\\Dev\\\\repos\\\\msarfraz\\\\cirrus\\\\artifacts\\\\Debug\\\\Core.Cli\",\n    \"configLocation\": \"C:\\\\Users\\\\msarfraz\\\\.azclips\"\n  }\n}\n```\nlinux-x64\n`az version`\n\n```\n{\n  \"azCli\": \"0.1.3\",\n  \"runtime\": {\n    \"dotNet\": \".NET 10.0.9\",\n    \"os\": \"Ubuntu 24.04.4 LTS\",\n    \"architecture\": \"X64\"\n  },\n  \"paths\": {\n    \"installLocation\": \"/app/Core.Cli\",\n    \"configLocation\": \"/root/.azclips\"\n  }\n}\n```\nlinux-arm64\n`az version`\n\n```\n{\n  \"azCli\": \"0.1.3\",\n  \"runtime\": {\n    \"dotNet\": \".NET 10.0.9\",\n    \"os\": \"Ubuntu 24.04.4 LTS\",\n    \"architecture\": \"Arm64\"\n  },\n  \"paths\": {\n    \"installLocation\": \"/app/Core.Cli\",\n    \"configLocation\": \"/root/.azclips\"\n  }\n}\n```\n`az --version` provides the same above information in Text format\n\n```\nAzCli             0.1.0\n\nRuntime\n    .NET:         .NET 10.0.9\n    OS:           Microsoft Windows 10.0.26200\n    Architecture: X64\n\nPaths\n    Install:      C:\\Dev\\repos\\msarfraz\\cirrus\\artifacts\\Debug\\Core.Cli\n    Config:       C:\\Users\\msarfraz\\.azclips\n```\n\n---------\n\nCo-authored-by: Mansoor Sarfraz <msarfraz+microsoft@microsoft.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-07T16:38:47+10:00",
          "tree_id": "bb41c85f0bb78fefdc0990c9fd61bef45885f37f",
          "url": "https://github.com/Azure/azclips/commit/3a03324449b2f2357507ba3f81bcd01d2e5c10e0"
        },
        "date": 1783406441809,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 988084.3526057818,
            "unit": "ns",
            "range": "± 298856.00740591367"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 959394.9172086043,
            "unit": "ns",
            "range": "± 312016.95730833616"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 949288.443577431,
            "unit": "ns",
            "range": "± 298352.54439627304"
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
          "id": "5a58415db8ba1871e75cf11668f8525ee2892231",
          "message": "[Login] Add managed identity login support (#381)\n\n## Summary\n\nAdds managed identity (MI) authentication to the login flow, supporting\nboth system-assigned and user-assigned identities via MSAL, wired\nthrough the CLI login processor, PowerShell cmdlets, and the token\ncache.\n\n## What's included\n\n- **Managed identity auth**: `ManagedIdentityAuthenticator`,\n`ManagedIdentityCredential`, and `AzureCredentialFactory` acquire tokens\nfor system-assigned and user-assigned (client-id / object-id /\nresource-id) identities.\n- **Login wiring**: `LoginProcessor` gains `--identity`, `--client-id`,\n`--object-id`, `--resource-id`; MI login does not require\n`--subscription-id` (a default subscription is auto-discovered from the\ntoken when omitted).\n- **Subscription discovery**: `SubscriptionService` /\n`ISubscriptionService` list subscriptions for a token so login can pick\na sensible default.\n- **Profile persistence**: MI identity selector is encoded/decoded via\n`ManagedIdentityIdCodec` and stored on the profile.\n- **Enum consolidation**: removed the duplicate `LoginMode` enum in\nfavor of the existing `AuthMethod`.\n- **Validation cleanup**: consolidated the subscription-id requirement\ninto `ValidateArguments` (skipped for MI and for `--profile` re-auth).\n\n## Testing\n\n- New unit tests: `ManagedIdentityCredentialTests`,\n`ManagedIdentityIdCodecTests`, `ConnectAccountCmdletTests`.\n- New E2E tests: `ManagedIdentityE2ETests` exercise the real `az` CLI\nsubprocess against a mock IMDS server (`MockImdsServer`).\n- Full suite green: 0 failures across all projects, including the MI E2E\ntests.\n\n---------\n\nCo-authored-by: Ming Xu <xumi@microsoft.com>\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-07T18:15:26+10:00",
          "tree_id": "fff724310b9819c48cb4352cbc9065b4fd2a27d1",
          "url": "https://github.com/Azure/azclips/commit/5a58415db8ba1871e75cf11668f8525ee2892231"
        },
        "date": 1783412245093,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 969003.3817290374,
            "unit": "ns",
            "range": "± 309317.65519531246"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 964225.0294176505,
            "unit": "ns",
            "range": "± 307308.5733708852"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 944247.6410487341,
            "unit": "ns",
            "range": "± 292732.8795150072"
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
          "id": "3d113413b80a496e1e6aa6537d3e01690c470099",
          "message": "{DevOps} Ship CLI completion scripts in installer archives (#391)\n\n## Summary\n- Publish non-C# CLI completion assets into `completions/` from\n`Core.Cli.csproj`\n- Require Linux/macOS tarballs and Windows zip archives to include the\ncompletion scripts under `completions/`\n\n## Validation\n- `dotnet publish src/CLI/Core.Cli.csproj -c Release -r linux-x64\n--self-contained true -o /tmp/azclips-publish-completions`\n- Verified published output contains:\n  - `completions/az.completions.ps1`\n  - `completions/az.completions.bash`\n  - `completions/az.completions.zsh`\n- `bash -n\ndevops/scripts/packaging/linux/tarball/test_linux_tarball_bundled_plus_ext.sh`\n- `bash -n\ndevops/scripts/packaging/macos/tarball/test_macos_tarball_bundled_plus_ext.sh`\n- PowerShell parse check for\n`devops/scripts/packaging/windows/zip/test_windows_zip_bundled_plus_ext.ps1`\n- `git diff --check`\n- `./devops/scripts/packaging/common/build_common.sh`\n- `./devops/scripts/packaging/linux/tarball/build_linux_tarball.sh`\n- `SKIP_RUNTIME=true\n./devops/scripts/packaging/linux/tarball/test_linux_tarball_bundled_plus_ext.sh`",
          "timestamp": "2026-07-08T09:39:03+10:00",
          "tree_id": "387c23808111858965100f1faaa23c6cf32c5e96",
          "url": "https://github.com/Azure/azclips/commit/3d113413b80a496e1e6aa6537d3e01690c470099"
        },
        "date": 1783467664835,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 982442.3677470988,
            "unit": "ns",
            "range": "± 307925.8892103974"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 949886.2754203363,
            "unit": "ns",
            "range": "± 295638.76274627633"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 939636.0356142457,
            "unit": "ns",
            "range": "± 290255.0587168732"
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
          "id": "a1e703ae4519309349576da32c399b1a9c0e2f64",
          "message": "Trigger stable release 0.0.2 (#400)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.2, aggregates module ChangeLog.md\nentries into CHANGELOG.md and PowerShell module manifests, and refreshes\nroot NOTICE from repository dependency metadata. After this PR merges to\nmain, azclips-trigger-stable-release will run again from the main\nversion change, tag the merged commit as v0.0.2.<UTC yyyyMMddTHHmmssZ>,\nand start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-08T09:49:43+10:00",
          "tree_id": "7bda6ae1d537869eb7d2ff79b6172ce97c60773a",
          "url": "https://github.com/Azure/azclips/commit/a1e703ae4519309349576da32c399b1a9c0e2f64"
        },
        "date": 1783468302338,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1004404.9139655862,
            "unit": "ns",
            "range": "± 314241.91003367305"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 984803.5909363745,
            "unit": "ns",
            "range": "± 312412.3417256173"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 952690.6223611806,
            "unit": "ns",
            "range": "± 303957.1129808395"
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
          "id": "467e53604bf63d4388c6eb42dbe0b8bfb06b1269",
          "message": "{Extensions} Handle precedence checks, verbiage cleanup, and builtIn management (#370)\n\n## Summary\n- Unifies extension command metadata on `[ExtensionVerb]` and removes\nredundant `GetVerbs()` contract usage.\n- Adds deterministic same-identity extension selection across install\nlocations.\n- Enforces cross-identity duplicate-command collisions as hard-fail\nafter same-identity selection.\n- Improves selection diagnostics readability in `--debug` output.\n\n## Behavior Changes\n- Precedence for same extension identity:\n  1. Pinned version wins.\n  2. Otherwise higher semantic version wins.\n  3. Equal package+version uses deterministic equivalent selection.\n- Collision policy:\n- Same identity across locations is resolved by precedence (not treated\nas collision).\n- Different identities owning the same command now hard-fail with\ncollision error.\n- Assembly loading:\n- Honors configured builtin/versioned installation directory overrides.\n- Selection behavior is stable regardless of directory enumeration\norder.\n\n## Validation\n- Automated coverage for precedence cases P1-P10.\n- Automated coverage for same-identity dedup vs cross-identity\nhard-fail.\n- Local/manual validation of duplicate-command rejection via `extension\nadd --path`.\n\n## Deferred Follow-ups (Not in this PR)\n- Add path-level candidate provenance in selection diagnostics.\n- Final built-in command-map strategy/docs decision.",
          "timestamp": "2026-07-08T17:15:22+10:00",
          "tree_id": "0eb29ecc268497597ee880fa6f0bf814490f821d",
          "url": "https://github.com/Azure/azclips/commit/467e53604bf63d4388c6eb42dbe0b8bfb06b1269"
        },
        "date": 1783495062867,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 995533.3816289774,
            "unit": "ns",
            "range": "± 307066.4637188987"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 977505.332582808,
            "unit": "ns",
            "range": "± 310923.6057953501"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 965470.8209104552,
            "unit": "ns",
            "range": "± 313010.0800028695"
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
          "id": "b08c328a8e35b4b7773ad80bbe129db84a1ba606",
          "message": "Updated autocompletion scripts to reference env variable and caching for perf tuning  (#394)\n\nreplacing #390 to pass CI\n\n---------\n\nCo-authored-by: Teresa Ritorto <tritorto@microsoft.com>\nCo-authored-by: Teresa Ritorto <tritorto@gmail.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-08T19:26:56+10:00",
          "tree_id": "b140e12dc578c46e54c5c197d9b87d6cc9cf5a07",
          "url": "https://github.com/Azure/azclips/commit/b08c328a8e35b4b7773ad80bbe129db84a1ba606"
        },
        "date": 1783502947341,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1013117.4143314651,
            "unit": "ns",
            "range": "± 308724.60513253626"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 991976.3580290145,
            "unit": "ns",
            "range": "± 304879.1649970919"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 969758.4368184092,
            "unit": "ns",
            "range": "± 300720.0464951309"
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
          "id": "a905624032aba2cf72c6fadc3a3fa4e6d33533c7",
          "message": "Trigger stable release 0.0.3 (#416)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.3, aggregates module ChangeLog.md\nentries into CHANGELOG.md and PowerShell module manifests, and refreshes\nroot NOTICE from repository dependency metadata. After this PR merges to\nmain, azclips-trigger-stable-release will run again from the main\nversion change, tag the merged commit as v0.0.3.<UTC yyyyMMddTHHmmssZ>,\nand start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-09T10:35:31+10:00",
          "tree_id": "35c6547ed5929e6c61dcdf89db1b321bbff98c8c",
          "url": "https://github.com/Azure/azclips/commit/a905624032aba2cf72c6fadc3a3fa4e6d33533c7"
        },
        "date": 1783557458027,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1051198.2338935575,
            "unit": "ns",
            "range": "± 338312.3689948265"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1036641.0658197459,
            "unit": "ns",
            "range": "± 324197.84139424656"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1020146.408244947,
            "unit": "ns",
            "range": "± 317904.5788398728"
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
          "id": "4f7a52cc5e0eef8fd0290a15c46d8511ec60a47c",
          "message": "fix: auto-detect azclips binary via JSON validation, remove AZCLIPS_B… (#411)\n\n…INARY env var\n\n- Remove _az_resolve_exe / _az_is_azclips in all shells\n- Detect azclips by validating az --completions output as JSON\n- Cache detection result per exe path (not global flag)\n- Resolve exe from user-typed command (words[1] / COMP_WORDS[0])\n- Add CWD fallback for bare az.exe (Windows user expectation)\n- Normalize backslashes to forward slashes (WSL compat)\n- Bash: complete -D handler for path-qualified commands\n- Zsh: compdef -p pattern for path-qualified commands\n- Update README: remove AZCLIPS_BINARY references",
          "timestamp": "2026-07-09T16:53:12+10:00",
          "tree_id": "2818e47bca44889b3ce749c76077e6372c91df69",
          "url": "https://github.com/Azure/azclips/commit/4f7a52cc5e0eef8fd0290a15c46d8511ec60a47c"
        },
        "date": 1783580120383,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1085105.1970985492,
            "unit": "ns",
            "range": "± 313811.85855891893"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1049258.7905743446,
            "unit": "ns",
            "range": "± 313815.5348695541"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1033845.61096658,
            "unit": "ns",
            "range": "± 328231.13068269345"
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
          "id": "bad2693f3b8c3c7853ecab600b9aed1f69608569",
          "message": "fixed console output of device code flow (#424)\n\nFixed issue where MSAL Device code output was no longer showing as we\nwere calling Log() which is tied to the LogLevel configured. This output\nalways needs to be shown to this user.\n\n### Testing\n<img width=\"1872\" height=\"416\" alt=\"image\"\nsrc=\"https://github.com/user-attachments/assets/4af7f351-a2cc-40fa-aa79-a741c7d2d391\"\n/>",
          "timestamp": "2026-07-09T14:56:18+08:00",
          "tree_id": "a636abb6442b3fd685d0d00a76ce4f589bcd96dc",
          "url": "https://github.com/Azure/azclips/commit/bad2693f3b8c3c7853ecab600b9aed1f69608569"
        },
        "date": 1783580294861,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1000491.1006003001,
            "unit": "ns",
            "range": "± 318105.0548555275"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 973651.5701420852,
            "unit": "ns",
            "range": "± 303948.2032014291"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 964051.1027013507,
            "unit": "ns",
            "range": "± 301439.0180100221"
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
          "id": "d41a3bd033af9e68a0b465be7391c0da3e6aa010",
          "message": "feat(agent): cache refresh command, resource tree/show, ARG pagination fixes, packaging size reduction (#395)\n\n## Summary\n\n- Add `az agent tool cache refresh` — synchronously refreshes the\non-disk ARM cache used by resource commands. Prior to this, the other\nresource commands only kicked a background refresh and returned empty on\ncold-start machines.\n- Add `az agent tool resource tree` — returns the resource hierarchy\nunder a node (tenant / subscription / resource group / resource).\n- Add `az agent tool resource show` — returns details for one resource\nfrom the cache, with optional `--live` flag for a real-time ARM GET.\n- Add background ARM cache refresher, triggered by resource commands.\nPer-tenant cache layout under `~/.azclips/arm-cache/<tenantId>/` with\nTTL (1 day for resource groups, 8 hours for resources).\n- Agent-side help and env tools now shell out to `az agent tool help` /\n`az agent tool env` subprocesses; the CLI-side processors are the single\nsource of truth. Removes duplicate JSON fixtures previously baked into\nthe agent build.\n- `az agent tool help` walks all installed extensions (built-in +\nuser-added via `az extension add`) via `IAssemblyLoadingService` and\naggregates their command tables at runtime; no cached catalog to\ninvalidate.\n- Inject a classic-Azure-CLI boundary reminder into the agent's\nsession-start context (mirrors the existing prompt-level guidance so it\nlands in a spot the model doesn't skim).\n\n## Bug fixes\n\n- ARG runaway pagination: request body was missing the `subscriptions`\narray. Without it, ARG returned cross-tenant data with an unbounded\n`$skipToken` chain (observed 692k+ rows for a 15k-resource tenant, hung\nfor 12 min). Now loads the subscription list first and scopes every\nsubsequent query to it — full refresh completes in ~22s.\n- Silent request-body corruption in the ARG client: body was built with\nanonymous types assigned to `object`-typed locals. `System.Text.Json`\nserializes those by static type, dropping every property. The pagination\ncursor was never actually sent to the server. Fixed by switching to\n`Dictionary<string, object>`.\n- `CachedSubscription.IsDefault` deserialization: ARG returns\n`isDefault=0` as an integer; `System.Text.Json` won't coerce int to\nbool, so the entire subscriptions array failed to load. Cache was\nsilently empty, and any `resource tree/show` against a cached\nsubscription returned \"not found\". Removed the field.\n- ARG refresh marker vs failure: the refresher used to stamp a fresh\n`cachedAt` before the ARG query. If the query threw (network / auth /\nAPI drift), the caller would see the stale payload but future\n`NeedsTtlRefresh()` checks would treat the file as fresh and skip\nrefresh for another full TTL window (up to 8 h for resources).\nQuery-then-write now; the per-file `.lock` still coalesces peer\nrefreshers.\n- Cache readers on Windows opened files with default `FileShare.Read`,\nwhich blocked the refresher's atomic `File.Move(overwrite:true)` with\n\"access denied\". Now use `FileShare.ReadWrite | FileShare.Delete` to\nrestore POSIX-like replace-while-open semantics.\n- `ArmCacheRefresher` no longer sends an unscoped ARG query when the\nsubscription list is empty — that would re-trigger the same cross-tenant\nrunaway the `subscriptions` array is meant to prevent.\n- `FailedCommandHelp` now inspects the `error` field of the\n`get_command` JSON payload instead of substring-matching `\"not_found\"`,\nso any error variant (spawn failure, timeout, missing name) skips help\ninjection uniformly.\n\n## Packaging\n\nWindows zip size drops from ~175 MB to ~127 MB (compressed).\n\nHybrid layout instead of a plain flatten — the launcher files sit next\nto `az.exe` (so `azclips-agent.exe` shares the .NET runtime pack and\n`hostpolicy.dll` during native bootstrap), and every agent-only asset\nmoves under the Agent extension directory:\n\n```\n<publish root>/\n  az.exe                                              CLI\n  azclips-agent.exe / .dll / .deps.json / .runtimeconfig.json     launcher siblings\n  System.*.dll, hostpolicy.dll, coreclr.dll, ...      shared runtime pack (one copy)\n  Extensions/\n    Microsoft.Azure.Azclips.Extensions.Agent/\n      Microsoft.Azure.Azclips.Extensions.Agent.dll    extension code\n      prompts/                                        agent-only assets\n      deps/                                           NuGet-style probe layout\n        github.copilot.sdk/<ver>/lib/net10.0/*.dll\n        github.copilot.sdk/<ver>/runtimes/<rid>/native/copilot[.exe]\n        <other agent-only nugets>/<ver>/lib/<tfm>/*.dll\n```\n\n- Agent daemon (`azclips-agent.exe`) still publishes `--self-contained`,\nbut the post-publish `stage_agent` step\n(`devops/scripts/packaging/common/stage_agent.ps1` + `.sh`) splits the\noutput: launcher goes to the CLI root, prompts + agent-only NuGet dlls +\ncopilot native go under the Agent extension dir in NuGet-style layout.\nRuntime pack files byte-identical to the CLI root are deleted; anything\nunclassified fails packaging loudly rather than silently.\n- `Agent/runtimeconfig.template.json` adds `additionalProbingPaths:\n[\"Extensions/Microsoft.Azure.Azclips.Extensions.Agent/deps\"]` so the\nmanaged loader finds the NuGet-style dep tree. The CLI spawns the agent\nwith `WorkingDirectory = <agent dir>` so the relative path resolves\nagainst the install root.\n- Remove 4 orphan `PackageReference` entries —\n`Microsoft.Identity.Client` (x2) and `Azure.Identity` (x2). Neither is\nused in production agent code; the agent goes through `IAuthService` in\n`Core.Authentication`. The only file referencing them was already\nexcluded from compilation.\n\n## Agent latency & prompt optimization\n\nTrimmed the agent's effective system prompt ~75% and restructured the\nerror-recovery prompts, cutting time-to-plan substantially with no loss\nin plan quality.\n\n**System prompt trimmed via SDK section customization.** The agent\npreviously *appended* our prompt to the Copilot SDK's default system\nmessage, so every turn paid for ~8k tokens of generic coding-assistant\ninstructions (tone, code-change rules, per-tool guides for tools this\nagent never calls, repo custom-instructions). We now use\n`SystemMessageMode.Customize`: our prompt *replaces* the `identity`\nsection (so it leads), and sections azclips doesn't use are removed.\nRemoved for all modes: `tone`, `code_change_rules`,\n`custom_instructions`. Removed for one-shot only: `tool_instructions`,\n`runtime_instructions` (a single-shot plan needs neither the per-tool\nguides nor the multi-turn runtime scaffolding; interactive keeps them\nsince it runs tools across turns). Kept: `tool_efficiency`,\n`environment_context`, `guidelines`, `safety`. Net: 42.8k → ~10.7k\nchars.\n\n**Prompt restructure (oneshot.md / inline.md).** Rewrote both to a\ntag-wrapped structure (`<identity>` / `<hard_rules>` / `<scope>` /\n`<error_recovery_workflow>` with `<assess>` / `<propose_plan>` /\n`<submit_plan>`) so cross-references like \"go straight to\n`<propose_plan>`\" are precise jump targets. Added a Fast-path /\nGather-path split: a self-explanatory argument fix skips lookups and\nproposes a plan directly, while the multi-command case does a single\nparallel batch of `get_command` / `find_resources` /\n`find_available_locations` instead of probing one command per turn.\nEmphasis trimmed to the speed-critical Fast/Gather distinction.\n\n**Login-help pre-fetch.** When the env snapshot reports signed-out, the\nsession-start context also pre-fetches `login` help, so a plan needing\n`az login` doesn't burn a turn looking it up (mirrors the existing\nfailed-command pre-fetch).\n\n**Interactive execution + close fixes.** After the user approves a plan,\nthe host now tells the model to carry it out (an `allow` alone didn't\nsignal approval, so it often stopped instead of executing).\n`submit_response` no longer re-renders the plan in interactive mode (the\ncommands already ran and their output is on screen); it shows a terse \"✓\nDone\". The disclaimer moved to the end of `RenderPlan` so it travels\nwith the plan in both modes.\n\nMeasured locally (Debug): `vm create` 78s → 35s, `profile show` 38s →\n26s; plan quality unchanged across sampled runs.",
          "timestamp": "2026-07-09T19:35:11+10:00",
          "tree_id": "1c401ca77723bf018dad44619a0321c7a323aad3",
          "url": "https://github.com/Azure/azclips/commit/d41a3bd033af9e68a0b465be7391c0da3e6aa010"
        },
        "date": 1783589832598,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 992921.9380504404,
            "unit": "ns",
            "range": "± 303659.1402514619"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 973485.377063119,
            "unit": "ns",
            "range": "± 321936.4736864149"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 970729.1007503752,
            "unit": "ns",
            "range": "± 308352.329085768"
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
          "id": "bc736172fff18ad88bef91230de5104ce721cfbb",
          "message": "Trigger stable release 0.0.4 (#431)\n\nUpdates src/CLI/Core.Cli.csproj to 0.0.4 and aggregates module\nChangeLog.md entries into CHANGELOG.md and PowerShell module manifests.\nAfter this PR merges to main, azclips-trigger-stable-release will run\nagain from the main version change, tag the merged commit as v0.0.4.<UTC\nyyyyMMddTHHmmssZ>, and start the stable build/sign/publish chain.\n\nCo-authored-by: Azclips Release Automation <azclips-release-automation@microsoft.com>",
          "timestamp": "2026-07-10T09:18:05+10:00",
          "tree_id": "552dca97dcca43be7a84c1859890a091672d565f",
          "url": "https://github.com/Azure/azclips/commit/bc736172fff18ad88bef91230de5104ce721cfbb"
        },
        "date": 1783639231354,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1062895.4603722605,
            "unit": "ns",
            "range": "± 339056.3004238054"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1031126.4764858915,
            "unit": "ns",
            "range": "± 311108.4132312102"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1027718.1431715857,
            "unit": "ns",
            "range": "± 319380.7456557981"
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
          "id": "a908745199336a41b4c39d9b40711d8b9fed1f47",
          "message": "fix(cli): use relaxed JSON escaping for console output (#434)\n\n## Summary\n\nCLI JSON output was rendered with System.Text.Json's default (HTML-safe)\nencoder, which escapes double quotes and non-ASCII characters as\n`\\uXXXX`. This made the `rest` command's raw `content` body nearly\nunreadable — every quote in the returned JSON showed as `\\u0022`.\n\nThis switches the CLI output formatter to\n`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` so embedded JSON strings\nand non-ASCII text render verbatim in the terminal.\n\n## Root cause\n\n`CliOutputFormatter.s_jsonOptions` did not set an `Encoder`, so it used\nthe default HTML-safe encoder. The `rest` response surfaces the raw HTTP\nbody as a string property (`RestResponse.Content`); when that object is\nserialized for console display, every `\"` inside the body is emitted as\n`\\u0022`.\n\n## Fix\n\nSet `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` on the CLI\noutput serializer. This is the standard choice for terminal output —\n\"unsafe\" only means HTML-sensitive characters (`<`, `>`, `&`, `'`) are\nnot escaped, which is irrelevant for a console (the output is not\nembedded in HTML).\n\nOnly the CLI is affected. The PowerShell cmdlets emit the live\n`RestResponse` object via `WriteObject`, so they never pass through this\nserializer and were never affected.\n\n## Before / after\n\n`az rest --method GET --uri\n\"/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01\"`\n\nBefore:\n```\n\"content\": \"{\\u0022value\\u0022:[{\\u0022id\\u0022:\\u0022/subscriptions/...\n```\n\nAfter:\n```\n\"content\": \"{\\\"value\\\":[{\\\"id\\\":\\\"/subscriptions/...\n```\n\n## Testing\n\n- Added two regression tests to `CliOutputFormatterTests`: one asserting\nembedded JSON quotes are not `\\u0022`-escaped, one asserting non-ASCII\ntext is emitted verbatim.\n- All 18 `CliOutputFormatterTests` pass.\n\n## Notes\n\nRequires rebuilding/reinstalling the CLI package for end users to pick\nup the change.",
          "timestamp": "2026-07-10T13:16:44+10:00",
          "tree_id": "1881f74d6e902bb44844895c148f10e2fe80ff8d",
          "url": "https://github.com/Azure/azclips/commit/a908745199336a41b4c39d9b40711d8b9fed1f47"
        },
        "date": 1783653539767,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1068802.2958887666,
            "unit": "ns",
            "range": "± 333973.65135891485"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1024362.8424212106,
            "unit": "ns",
            "range": "± 326292.90966212255"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1020505.319227691,
            "unit": "ns",
            "range": "± 324582.748200413"
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
          "id": "962b6bc594aeb8b1a6e7e2cc27b47235f3119c72",
          "message": "[Benchmark] Benchmark start up time (#401)\n\n## What\nMeasures how long `az` and `pwsh` take to start up (process-create →\ncommand ready),\nseparately from command execution. Adds warm/cold benchmarks, richer\nreporting, and\nCI dashboards to track startup regressions.\n\nLoop for context: [Measuring start up\ntime](https://microsoft.sharepoint.com/:fl:/g/contentstorage/CSP_2e24e7fe-1ac4-440f-bfce-2f4be2ac6e50/IQDPWhjRrPT5QpMalKvnTZ5hAWWJkEXTX3Pw5_zDdkRmRt8?e=OCVdLa&nav=cz0lMkZjb250ZW50c3RvcmFnZSUyRkNTUF8yZTI0ZTdmZS0xYWM0LTQ0MGYtYmZjZS0yZjRiZTJhYzZlNTAmZD1iJTIxX3Vja0xzUWFEMFNfemk5TDRxeHVVQzFBYVZFbklmeE11dWIyS1dMa0RwWkJNRjBzMThzbVJZMTQwM01YclJWUSZmPTAxWTZETVdLNlBMSU1ORExIVTdGQkpHR1VVVlBUVTNIVEImYz0lMkYmYT1Mb29wQXBwJnA9JTQwZmx1aWR4JTJGbG9vcC1wYWdlLWNvbnRhaW5lcg%3D%3D)\n\n## How it works\nA new `ProcessorAcquired` engine event fires once the command is\nresolved but before\nit runs. When `AZCLIPS_BENCH_RESOLVE_ONLY=true`, the engine exits right\nthere — timing\nstartup only, with no network or auth side effects. The flag defaults\noff, so\nproduction behavior is unchanged.\n\n## Changes\n- **Engine seam**: `ProcessorAcquired` event +\n`BenchmarkOptions.ResolveOnly`, wired\nvia config and the `AZCLIPS_BENCH_RESOLVE_ONLY` env var.\n`Environment.Exit(0)` only\n  when the flag is set.\n- **Harness** (`tests/Benchmark/Startup/`): spawns real `az`/`pwsh` per\niteration for\n  the `config`, `login`, and `extension` built-ins.\n  - Warm — page cache hot.\n- Cold — page cache evicted first (Linux `drop_caches`; Windows\nstandby-list purge,\nneeds an elevated terminal). Cold is skipped with a reason when eviction\nisn't\n    available, never faked as cold.\n- **Reporting**: adds `Mode` (warm/cold) + `Extension` dimensions to\ncharts and\n  App Insights; warm/cold shown in separate sections.\n- **CI**: new `--product STARTUP` step publishing four dashboards — warm\n(tracked,\n  115% alert) and cold (informational).\n- **Tests**: new `Core.Engine.Tests` verifying `ProcessorAcquired` fires\nonce, before\n  execution, in both CLI and PowerShell paths.\n\n---------\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>\nCo-authored-by: Copilot Autofix powered by AI <175728472+Copilot@users.noreply.github.com>",
          "timestamp": "2026-07-10T15:16:33+10:00",
          "tree_id": "345b6594b7ab67f144d89b8775255a2ab69203c0",
          "url": "https://github.com/Azure/azclips/commit/962b6bc594aeb8b1a6e7e2cc27b47235f3119c72"
        },
        "date": 1783661218646,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1112079.485788631,
            "unit": "ns",
            "range": "± 363259.89055842825"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1083435.7345938375,
            "unit": "ns",
            "range": "± 348630.02888765"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1095212.219331599,
            "unit": "ns",
            "range": "± 362335.3256155452"
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
        "date": 1783667039238,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1049819.4952971784,
            "unit": "ns",
            "range": "± 334472.19374695263"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1028171.8809166417,
            "unit": "ns",
            "range": "± 324482.6878951703"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1009762.8988595438,
            "unit": "ns",
            "range": "± 312764.5967738507"
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
        "date": 1783901802430,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1047376.0613183955,
            "unit": "ns",
            "range": "± 349809.5653763815"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1019410.1130678407,
            "unit": "ns",
            "range": "± 333434.84496140253"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1009250.9634853941,
            "unit": "ns",
            "range": "± 333345.52298382105"
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
        "date": 1783904084136,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1034626.598168718,
            "unit": "ns",
            "range": "± 324925.5533334572"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1010151.902131492,
            "unit": "ns",
            "range": "± 327747.2508762157"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1003964.6029411765,
            "unit": "ns",
            "range": "± 332077.60472953"
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
        "date": 1783904301543,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1035413.2083458421,
            "unit": "ns",
            "range": "± 347443.51001271664"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1022459.5537413603,
            "unit": "ns",
            "range": "± 344966.93597580754"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1013307.9496798719,
            "unit": "ns",
            "range": "± 330456.8699792223"
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
        "date": 1783917235638,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1083423.9522975273,
            "unit": "ns",
            "range": "± 349537.36086543556"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1055346.099839856,
            "unit": "ns",
            "range": "± 346314.2357507668"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1053316.9408112168,
            "unit": "ns",
            "range": "± 373111.8136853345"
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
        "date": 1783922091155,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1111184.0478627332,
            "unit": "ns",
            "range": "± 358632.97080968926"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1097956.3139255703,
            "unit": "ns",
            "range": "± 351498.6970841115"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1078206.6900950475,
            "unit": "ns",
            "range": "± 359453.1405889522"
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
        "date": 1783923399401,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1058369.0943755005,
            "unit": "ns",
            "range": "± 345026.15304626495"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1055823.49939964,
            "unit": "ns",
            "range": "± 344555.5395253525"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1033092.9090636255,
            "unit": "ns",
            "range": "± 337836.9822837946"
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
        "date": 1784010444302,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1043957.6032016008,
            "unit": "ns",
            "range": "± 323447.29371827457"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1020152.7080832333,
            "unit": "ns",
            "range": "± 326993.67549729353"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1006848.7807684611,
            "unit": "ns",
            "range": "± 329245.238215152"
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
        "date": 1784092339184,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1037980.5415707853,
            "unit": "ns",
            "range": "± 334836.20125694573"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1035365.299689907,
            "unit": "ns",
            "range": "± 335730.52508237294"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1011382.6575630252,
            "unit": "ns",
            "range": "± 342669.2087139228"
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
        "date": 1784167768139,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1110618.402081457,
            "unit": "ns",
            "range": "± 362760.1408979522"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1080751.7058117436,
            "unit": "ns",
            "range": "± 344205.60869422444"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1061881.7111555778,
            "unit": "ns",
            "range": "± 336667.30308531615"
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
        "date": 1784168856798,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1059376.0523209283,
            "unit": "ns",
            "range": "± 337367.20815943286"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1025135.4582666133,
            "unit": "ns",
            "range": "± 340471.6208360266"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1025922.6530224179,
            "unit": "ns",
            "range": "± 324424.28365267254"
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
        "date": 1784173259539,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1064899.5927370947,
            "unit": "ns",
            "range": "± 348305.86773793335"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1036851.2889600641,
            "unit": "ns",
            "range": "± 332005.5665509354"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1035230.2168084042,
            "unit": "ns",
            "range": "± 344766.8767275087"
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
        "date": 1784187199508,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1072065.041725035,
            "unit": "ns",
            "range": "± 353678.7120219865"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1058289.4555777889,
            "unit": "ns",
            "range": "± 340609.7426637266"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1046658.6322793677,
            "unit": "ns",
            "range": "± 343407.74948503764"
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
        "date": 1784187716674,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1046388.9238695478,
            "unit": "ns",
            "range": "± 354498.62591268815"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1039362.9230538323,
            "unit": "ns",
            "range": "± 347306.74922139227"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1014663.337835134,
            "unit": "ns",
            "range": "± 310546.7164827658"
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
        "date": 1784202723042,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1106979.568068068,
            "unit": "ns",
            "range": "± 362737.6880913603"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1093708.2100050026,
            "unit": "ns",
            "range": "± 331095.3198459667"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1081338.6711513454,
            "unit": "ns",
            "range": "± 357410.7658823"
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
        "date": 1784255740090,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1059498.8048524263,
            "unit": "ns",
            "range": "± 355666.8212099406"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1032781.2231892757,
            "unit": "ns",
            "range": "± 340336.8584032934"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1023525.5718287315,
            "unit": "ns",
            "range": "± 323603.08226167096"
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
        "date": 1784256010500,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1041771.1522609043,
            "unit": "ns",
            "range": "± 336386.29645385535"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1029365.6458583433,
            "unit": "ns",
            "range": "± 340579.67660167464"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1005943.7824129652,
            "unit": "ns",
            "range": "± 324841.7248258055"
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
        "date": 1784258034135,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1054204.4953981594,
            "unit": "ns",
            "range": "± 342643.314935352"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1040922.5758303321,
            "unit": "ns",
            "range": "± 347496.493666673"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1003406.6866433216,
            "unit": "ns",
            "range": "± 327807.9993495919"
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
        "date": 1784259645448,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1053519.4863918351,
            "unit": "ns",
            "range": "± 334588.57123681856"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1029183.3233763635,
            "unit": "ns",
            "range": "± 329026.97580101265"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1006427.0840752226,
            "unit": "ns",
            "range": "± 326203.5936600631"
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
        "date": 1784261259623,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1037504.8290316126,
            "unit": "ns",
            "range": "± 327758.9098510961"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1022823.2690383268,
            "unit": "ns",
            "range": "± 344275.0116958312"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1005379.7427213606,
            "unit": "ns",
            "range": "± 323598.7203048542"
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
        "date": 1784345501366,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1047410.0325130052,
            "unit": "ns",
            "range": "± 356044.36759058526"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1030718.2530012005,
            "unit": "ns",
            "range": "± 331622.20142916316"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1007428.9825930372,
            "unit": "ns",
            "range": "± 324368.6260408455"
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
        "date": 1784368504760,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1081556.0332232562,
            "unit": "ns",
            "range": "± 347719.6290309445"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1055411.3042042041,
            "unit": "ns",
            "range": "± 353583.9919250747"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1043178.6670668267,
            "unit": "ns",
            "range": "± 350079.0924925452"
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
        "date": 1784526581879,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1052076.4343737494,
            "unit": "ns",
            "range": "± 346041.4743843057"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1029027.6649154408,
            "unit": "ns",
            "range": "± 332394.418608368"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1015980.6259377813,
            "unit": "ns",
            "range": "± 341895.9795958508"
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
        "date": 1784589558597,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1040677.1466613275,
            "unit": "ns",
            "range": "± 344028.159845577"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1023940.5156109276,
            "unit": "ns",
            "range": "± 322528.4786837393"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1008361.3449224612,
            "unit": "ns",
            "range": "± 326793.96270467096"
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
        "date": 1784590756763,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1033381.3619343212,
            "unit": "ns",
            "range": "± 329676.0927995781"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1008316.4783913565,
            "unit": "ns",
            "range": "± 347400.6123717899"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1009176.646852797,
            "unit": "ns",
            "range": "± 314232.71016240335"
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
        "date": 1784693068076,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 835577.0851642814,
            "unit": "ns",
            "range": "± 16800.92049956533"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 810524.5883303048,
            "unit": "ns",
            "range": "± 17112.949123345865"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 800103.5640508372,
            "unit": "ns",
            "range": "± 19470.161898725266"
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
        "date": 1784694237319,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 879176.6392085605,
            "unit": "ns",
            "range": "± 23426.675170697294"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 870180.8102336825,
            "unit": "ns",
            "range": "± 28761.3198477415"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 839794.5101680846,
            "unit": "ns",
            "range": "± 26276.754892119854"
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
        "date": 1784696294941,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 860287.067806841,
            "unit": "ns",
            "range": "± 19567.696344667107"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 827893.7860455737,
            "unit": "ns",
            "range": "± 21001.939263749915"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 810774.0933333334,
            "unit": "ns",
            "range": "± 20626.993927027404"
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
        "date": 1784783680223,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 864998.4707121364,
            "unit": "ns",
            "range": "± 36562.19892591938"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 821154.0435308344,
            "unit": "ns",
            "range": "± 18648.969068452127"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 810266.4406161329,
            "unit": "ns",
            "range": "± 20195.62568679986"
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
        "date": 1784794084305,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 839448.4297409327,
            "unit": "ns",
            "range": "± 18154.212220828213"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 824100.4279910624,
            "unit": "ns",
            "range": "± 16963.781948797372"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 803071.9656635023,
            "unit": "ns",
            "range": "± 17495.928920879785"
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
        "date": 1785116462568,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 853278.0722043443,
            "unit": "ns",
            "range": "± 21057.439308547662"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 826888.8661829907,
            "unit": "ns",
            "range": "± 21012.950304472135"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 809304.3041195477,
            "unit": "ns",
            "range": "± 18727.549367583808"
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
        "date": 1785123356943,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 854392.2392988178,
            "unit": "ns",
            "range": "± 17255.289006335533"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 827164.8379226564,
            "unit": "ns",
            "range": "± 17455.698735335773"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 814877.9220072551,
            "unit": "ns",
            "range": "± 18182.649229811817"
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
        "date": 1785125727261,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 904128.81202931,
            "unit": "ns",
            "range": "± 20834.466425358"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 867636.4335074327,
            "unit": "ns",
            "range": "± 26328.736737593546"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 860603.0852947093,
            "unit": "ns",
            "range": "± 23911.12908196965"
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
        "date": 1785201477600,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 858930.4541800643,
            "unit": "ns",
            "range": "± 20615.874736212525"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 831162.1659053157,
            "unit": "ns",
            "range": "± 17045.230478320726"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 835485.9119838872,
            "unit": "ns",
            "range": "± 22159.48327410021"
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
        "date": 1785228582743,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 850214.9639018455,
            "unit": "ns",
            "range": "± 18141.76704023915"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 834042.5944737912,
            "unit": "ns",
            "range": "± 18156.119707382677"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 819218.7655618432,
            "unit": "ns",
            "range": "± 19687.318332816907"
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
        "date": 1785383809187,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 858341.3104985813,
            "unit": "ns",
            "range": "± 18541.45961027032"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 837825.8861476239,
            "unit": "ns",
            "range": "± 17981.447730199252"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 819789.3460606061,
            "unit": "ns",
            "range": "± 18618.96851783563"
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
        "date": 1785389219785,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 898696.2905011069,
            "unit": "ns",
            "range": "± 28336.935864063184"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 877219.4998990918,
            "unit": "ns",
            "range": "± 29017.069885913337"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 864072.7718592965,
            "unit": "ns",
            "range": "± 29323.94366006148"
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
        "date": 1785392858058,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 852518.2539256199,
            "unit": "ns",
            "range": "± 15005.938497402753"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 828990.6788868351,
            "unit": "ns",
            "range": "± 15338.115985271714"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 825983.2179074447,
            "unit": "ns",
            "range": "± 22033.142551769175"
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
        "date": 1785452037165,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 915804.054700162,
            "unit": "ns",
            "range": "± 25817.030930725934"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 866298.4874873864,
            "unit": "ns",
            "range": "± 24825.271844734398"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 849576.9673781715,
            "unit": "ns",
            "range": "± 24338.113656314366"
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
        "date": 1785731680094,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 895073.4106776181,
            "unit": "ns",
            "range": "± 25646.458774944644"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 856659.9103861243,
            "unit": "ns",
            "range": "± 18716.51296059696"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 846319.2149783372,
            "unit": "ns",
            "range": "± 19625.924719159782"
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
        "date": 1785802470583,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 849572.7458552365,
            "unit": "ns",
            "range": "± 19622.54322834169"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 843977.4526273077,
            "unit": "ns",
            "range": "± 21477.678519842477"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 844317.5311228641,
            "unit": "ns",
            "range": "± 16905.37141144038"
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
        "date": 1785803948697,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 874186.7095366715,
            "unit": "ns",
            "range": "± 20847.025854265106"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 867836.7727828746,
            "unit": "ns",
            "range": "± 17700.528386961018"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 848148.3912022862,
            "unit": "ns",
            "range": "± 23246.27400662089"
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
        "date": 1785808493236,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 860605.7154373237,
            "unit": "ns",
            "range": "± 20485.240470676497"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 837847.7471636953,
            "unit": "ns",
            "range": "± 21677.837574137106"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 824250.4409987918,
            "unit": "ns",
            "range": "± 20707.00779838993"
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
        "date": 1785818162966,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 916010.8980523433,
            "unit": "ns",
            "range": "± 28207.78505352044"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 883325.3327287384,
            "unit": "ns",
            "range": "± 26851.46109392159"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 871468.7738764045,
            "unit": "ns",
            "range": "± 27604.389276329213"
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
        "date": 1785819622265,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 905740.127023151,
            "unit": "ns",
            "range": "± 19971.482280527"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 884082.2329638752,
            "unit": "ns",
            "range": "± 19933.35829101234"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 864191.4221995927,
            "unit": "ns",
            "range": "± 21006.00589756761"
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
        "date": 1785904858277,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 882667.7790414297,
            "unit": "ns",
            "range": "± 22777.2413605482"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 862659.9963547995,
            "unit": "ns",
            "range": "± 20033.57346326429"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 835240.3188139059,
            "unit": "ns",
            "range": "± 16832.487313101687"
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
          "id": "ecc27f0c7cd5bf371e1ce102450d6070d21ddaf2",
          "message": "{Help} Validate in CI for PS Help drift (#655)\n\nFixes https://github.com/azure/azclips/issues/652\n\n## What\n\n- **CI gate** — new `build/Test-PSHelpMarkdown.ps1` diffs every\n`src/PS/<Module>/help/*.md` against live cmdlet reflection via PlatyPS\n1.0.1 `Test-` / `Import-` / `Update-` / `Compare-CommandHelp`. Fires\n`PSL13_SchemaFailure` (unparseable md) and `PSL14_ParameterDrift`\n(missing/orphan params, wrong `IsMandatory`, wrong SyntaxParameters\norder, missing Outputs). Wired into the existing `help-validate` job.\n- **Remediation** — `pwsh -File build/New-PSHelpMarkdown.ps1 -Module\n<name> -Refresh` regenerates any drifted `.md` in place. Gate error\nfooter points at this exact command.\n- **Content backfill** — every PS cmdlet now declares\n`[OutputType(...)]`; every `.md` refreshed. Closes #652\n(Connect-AzAccount had 6 undocumented parameters and `-SubscriptionId`\nmis-declared as required).\n- **Smoke fixtures** — `tests/Scripts/PS/HelpDriftFixtures.ps1` builds a\nscratch cmdlet + module in a temp dir and asserts on gate output across\n5 scenarios (clean, missing param, orphan param, required flipped,\nschema corrupt). Runs before the real-repo check in CI so a gate\nregression fails cleanly.\n\n## Notes on observable changes\n\n- Optional `-Profile` / `-SubscriptionId` parameters reordered to the\nend of declaration on VM and Resource cmdlets so the reflected SYNTAX\nmatches the docs' \"mandatory first\" convention. **No impact on\nnamed-parameter binding**; only affects `Get-Command | Select\nParameterSets` enumeration order.\n- Cmdlet `OutputType` now surfaces via reflection where it previously\ndefaulted to `System.Object`. Scripts that inspect `(Get-Command\n<name>).OutputType` will now see the real type name.\n- No public CLI surface touched.\n\n## History Notes\n\n- [Help] `Get-Help Connect-AzAccount` now documents all 11 parameters\nwith `-SubscriptionId` correctly marked optional (closes #652).\n- [Help] Every PS cmdlet advertises a real output type via\n`[OutputType]`; `Get-Help -Full` shows accurate `OUTPUTS` sections\ninstead of `System.Object`.\n\n## Validation\n\n- `pwsh -File build/Test-PSHelpMarkdown.ps1` — exit 0 across all 4\nmodules (Config, Login, Compute, Resources).\n- `pwsh -NoProfile -File tests/Scripts/PS/HelpDriftFixtures.ps1` — all 5\nscenarios green.\n- `dotnet run Azclips.Help.Tool` — no issues.\n- `Get-Help Connect-AzAccount -Full` spot-check: all 11 parameters,\ncorrect required flags, `System.Void` OUTPUTS with prose.",
          "timestamp": "2026-08-06T17:14:44+10:00",
          "tree_id": "39a74ce077bec2727e15c48fc8a3f495598ee509",
          "url": "https://github.com/Azure/azclips/commit/ecc27f0c7cd5bf371e1ce102450d6070d21ddaf2"
        },
        "date": 1786001341594,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 906443.6556825137,
            "unit": "ns",
            "range": "± 17668.810177548672"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 877217.1784274194,
            "unit": "ns",
            "range": "± 20631.504022892743"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 853226.3625766871,
            "unit": "ns",
            "range": "± 17869.20462129012"
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
        "date": 1786065480035,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 930530.6629100422,
            "unit": "ns",
            "range": "± 27124.484091431546"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 886324.8936729331,
            "unit": "ns",
            "range": "± 27454.28573240325"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 891872.6744138214,
            "unit": "ns",
            "range": "± 28336.325463284393"
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
        "date": 1786087087163,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 927826.1437638525,
            "unit": "ns",
            "range": "± 28315.896630606607"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 898456.4604918363,
            "unit": "ns",
            "range": "± 28844.027310078738"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 894458.3367305752,
            "unit": "ns",
            "range": "± 28199.402393487013"
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
          "id": "ea1f8af50aeb699f539d95b56d2131216c06ef49",
          "message": "{Resources} Surface provisioningState from group create (#675)\n\nFix [#33832](https://github.com/Azure/azure-cli/issues/33832)\n\n`az group create` and `New-AzResourceGroup` never reported the\nprovisioning state: ARM returns it nested under properties, but the\n`ResourceGroup` model declared it as a flat property, so it silently\ndropped during deserialisation and the emitted object always carried an\nempty string. Found by replaying the recorded real ARM response in the\nscenario tests.\n\nThe model now nests it (`ResourceGroupProperties.ProvisioningState`),\nmatching the shape `DeploymentExtended` already uses. Properties is\nignored when null, so the PUT body for creates is unchanged apart from\ndropping the meaningless flat `provisioningState` field it used to send.\nThe group create scenario now asserts `provisioningState: Succeeded on\nboth surfaces, and the recording is refreshed from a live run`.\n\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-08-10T10:49:17+10:00",
          "tree_id": "f4c8861aaf7e2efed50a4ec59cc41391001c956a",
          "url": "https://github.com/Azure/azclips/commit/ea1f8af50aeb699f539d95b56d2131216c06ef49"
        },
        "date": 1786323738720,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 963034.4552780016,
            "unit": "ns",
            "range": "± 22683.991394406727"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 954940.4912387938,
            "unit": "ns",
            "range": "± 17208.485870052726"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 945553.9017966517,
            "unit": "ns",
            "range": "± 18042.840708511118"
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
        "date": 1786336851114,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1045882.717781403,
            "unit": "ns",
            "range": "± 18854.89802269797"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1013011.5073274985,
            "unit": "ns",
            "range": "± 22496.56318650347"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1015297.5861786956,
            "unit": "ns",
            "range": "± 19348.23510941417"
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
        "date": 1786425735576,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 956594.969924812,
            "unit": "ns",
            "range": "± 17967.95994544532"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 919949.4152713334,
            "unit": "ns",
            "range": "± 22256.03094566692"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 893317.3526229843,
            "unit": "ns",
            "range": "± 17433.557595300597"
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
        "date": 1786514694734,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 942310.9141476455,
            "unit": "ns",
            "range": "± 19985.063991823594"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 915460.3852659466,
            "unit": "ns",
            "range": "± 23415.960624398736"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 902786.1941295547,
            "unit": "ns",
            "range": "± 22987.133337493164"
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
        "date": 1786599968992,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 957813.7209631728,
            "unit": "ns",
            "range": "± 18541.843771082473"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 931878.0421785421,
            "unit": "ns",
            "range": "± 18362.25013159923"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 908256.03921963,
            "unit": "ns",
            "range": "± 22260.298978274375"
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
        "date": 1786927065432,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 857726.1400892676,
            "unit": "ns",
            "range": "± 18660.139457946174"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 837207.5293999194,
            "unit": "ns",
            "range": "± 22509.14698771014"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 819068.1390508747,
            "unit": "ns",
            "range": "± 20111.79012453879"
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
        "date": 1786931838693,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 830185.8340460191,
            "unit": "ns",
            "range": "± 17441.534918895082"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 814963.9933708317,
            "unit": "ns",
            "range": "± 18565.023530524482"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 797031.6718275653,
            "unit": "ns",
            "range": "± 15865.482928626105"
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
        "date": 1786952699205,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 933521.1294008647,
            "unit": "ns",
            "range": "± 16463.46970142521"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 908470.568036999,
            "unit": "ns",
            "range": "± 19892.499000634118"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 886458.1407648495,
            "unit": "ns",
            "range": "± 20808.204118489983"
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
        "date": 1787009299316,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 833367.3548780488,
            "unit": "ns",
            "range": "± 15633.08885945888"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 799143.724035911,
            "unit": "ns",
            "range": "± 20325.89413058518"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 803738.1971659919,
            "unit": "ns",
            "range": "± 15882.939584532587"
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
        "date": 1787019553830,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 844632.3838486575,
            "unit": "ns",
            "range": "± 17478.003608853727"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 818993.8565813344,
            "unit": "ns",
            "range": "± 18662.155906947173"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 811122.5068548387,
            "unit": "ns",
            "range": "± 18162.57995204903"
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
        "date": 1787034638244,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 846386.8502939388,
            "unit": "ns",
            "range": "± 19256.542914584334"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 831050.2912973409,
            "unit": "ns",
            "range": "± 19237.27647683995"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 811453.0162977867,
            "unit": "ns",
            "range": "± 22003.424250202257"
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
        "date": 1787108451386,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 823650.9381780962,
            "unit": "ns",
            "range": "± 14593.212678992035"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 796797.3478482561,
            "unit": "ns",
            "range": "± 18039.235044907116"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 791123.2530562347,
            "unit": "ns",
            "range": "± 20041.346870733312"
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
        "date": 1787203487353,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 956310.4015260879,
            "unit": "ns",
            "range": "± 22479.170979609848"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 936056.6890756303,
            "unit": "ns",
            "range": "± 19873.842435873215"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 936827.1783728115,
            "unit": "ns",
            "range": "± 21403.38875466354"
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
        "date": 1787530930662,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 981173.7127789047,
            "unit": "ns",
            "range": "± 22605.008417697416"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 955867.2564311964,
            "unit": "ns",
            "range": "± 22544.5131254162"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 949595.718089431,
            "unit": "ns",
            "range": "± 21547.56977663886"
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
        "date": 1787613481809,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1190446.7246010907,
            "unit": "ns",
            "range": "± 26363.099975534955"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1181009.0421760392,
            "unit": "ns",
            "range": "± 24453.818318759106"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1161517.8403003246,
            "unit": "ns",
            "range": "± 24194.628016159542"
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
        "date": 1787672982133,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1203481.0400122174,
            "unit": "ns",
            "range": "± 24724.19040817499"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1202114.1703172978,
            "unit": "ns",
            "range": "± 23657.39598778079"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1174696.3504194804,
            "unit": "ns",
            "range": "± 23775.559504290337"
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
        "date": 1787809755820,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1185281.567955239,
            "unit": "ns",
            "range": "± 26540.427761936866"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1179968.2351742408,
            "unit": "ns",
            "range": "± 24145.892202900137"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1153489.6670052824,
            "unit": "ns",
            "range": "± 24652.874240443492"
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
        "date": 1787898172718,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 1207865.258995731,
            "unit": "ns",
            "range": "± 25119.202632492943"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 1198377.7663153629,
            "unit": "ns",
            "range": "± 23936.405200659876"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 1172677.9253929374,
            "unit": "ns",
            "range": "± 24098.24225878611"
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
        "date": 1788142377172,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2331964.506566983,
            "unit": "ns",
            "range": "± 96020.73305676284"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2338561.0503220614,
            "unit": "ns",
            "range": "± 114504.52830567192"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2307643.9664591285,
            "unit": "ns",
            "range": "± 97613.70155190707"
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
        "date": 1788154382755,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2285038.8442524117,
            "unit": "ns",
            "range": "± 93221.49364051457"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2353405.2646341464,
            "unit": "ns",
            "range": "± 82300.33112391332"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2291335.5913287834,
            "unit": "ns",
            "range": "± 97094.11718696875"
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
        "date": 1788219474685,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2343990.704819277,
            "unit": "ns",
            "range": "± 89152.20207715046"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2343922.8535019066,
            "unit": "ns",
            "range": "± 96147.66741002089"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2317379.4146243474,
            "unit": "ns",
            "range": "± 92431.46027077301"
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
        "date": 1788228747375,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2336047.5945293643,
            "unit": "ns",
            "range": "± 83940.5422841817"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2333682.993553586,
            "unit": "ns",
            "range": "± 102017.06166920289"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2328334.405866988,
            "unit": "ns",
            "range": "± 103663.77707936245"
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
        "date": 1788234540540,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2321324.0292702485,
            "unit": "ns",
            "range": "± 92416.11823719181"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2341955.939259855,
            "unit": "ns",
            "range": "± 98013.64607644429"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2315119.5116419108,
            "unit": "ns",
            "range": "± 96405.40241415407"
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
          "id": "e83849c8645fa4685a532a2c113c135694fae698",
          "message": "[NonSquash] Update extension interface, add default pagination (#777)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-02T09:36:26+10:00",
          "tree_id": "4b3df278ec7341120cead6fc2ae49fecf83bb2b2",
          "url": "https://github.com/Azure/azclips/commit/e83849c8645fa4685a532a2c113c135694fae698"
        },
        "date": 1788306977237,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2321781.7613408267,
            "unit": "ns",
            "range": "± 87569.12595686843"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2324583.6449348046,
            "unit": "ns",
            "range": "± 92745.6450311836"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2311070.054642069,
            "unit": "ns",
            "range": "± 96804.16999114999"
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
          "id": "3991c8f38ae33084a243d292c2473d1139f5008f",
          "message": "{Tests} Run scenario scripts through the real client binary (#784)\n\nScenario tests now exercise the real az binary instead of an in-process host, following the review feedback on the scenario tier (CLIPS task #734). A test case is a script run by a real shell, one process per command, chaining values the way a user would.\n\nThe product side is small: the recording primitives move from Tests.Common into Core.Recording, and the two client composition roots call RecordingHook.TryRegister()\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-03T10:49:26+10:00",
          "tree_id": "a66a6b7ec1131bfbc6fadbfe2a6ccb8f5ca156ff",
          "url": "https://github.com/Azure/azclips/commit/3991c8f38ae33084a243d292c2473d1139f5008f"
        },
        "date": 1788397551219,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2336017.189883581,
            "unit": "ns",
            "range": "± 90274.81763090029"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2335324.6967625176,
            "unit": "ns",
            "range": "± 95696.91010942683"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2310927.8848545635,
            "unit": "ns",
            "range": "± 95594.01633747628"
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
          "id": "ea968585c36fe97794420981ec677e6c391bc403",
          "message": "{Codegen} phase 1 integration: Cli+Compiler+IR+Emitter+Generator (#788)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-03T16:29:06+10:00",
          "tree_id": "9cda8d8204ef0df979b1ad6632b6ba8e57a4a815",
          "url": "https://github.com/Azure/azclips/commit/ea968585c36fe97794420981ec677e6c391bc403"
        },
        "date": 1788417852413,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2333067.6443464314,
            "unit": "ns",
            "range": "± 90030.53366202954"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2318829.497689371,
            "unit": "ns",
            "range": "± 92047.79153235097"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2326219.4204089814,
            "unit": "ns",
            "range": "± 99591.56134030019"
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
        "date": 1788501950009,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2340882.7894209577,
            "unit": "ns",
            "range": "± 94118.39059104663"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2345354.0871097683,
            "unit": "ns",
            "range": "± 104845.5099603661"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2317956.3940550312,
            "unit": "ns",
            "range": "± 95102.13108551779"
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
        "date": 1788739821913,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2382548.0238429173,
            "unit": "ns",
            "range": "± 93659.5054570959"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2382055.837457252,
            "unit": "ns",
            "range": "± 103584.62300823166"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2362971.348205334,
            "unit": "ns",
            "range": "± 102737.03961787517"
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
        "date": 1788753010755,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2360013.8619582662,
            "unit": "ns",
            "range": "± 94428.92865811933"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2362168.831158402,
            "unit": "ns",
            "range": "± 100451.7446124403"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2346695.1458667736,
            "unit": "ns",
            "range": "± 101489.71290267108"
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
        "date": 1788768108403,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2344584.937751004,
            "unit": "ns",
            "range": "± 97738.45681460817"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2366278.9623818146,
            "unit": "ns",
            "range": "± 95940.16575346529"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2341836.249248045,
            "unit": "ns",
            "range": "± 105315.44903708619"
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
        "date": 1788834096885,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 2391217.8177626305,
            "unit": "ns",
            "range": "± 93864.40261608809"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 2380657.433199599,
            "unit": "ns",
            "range": "± 96513.18454710681"
          },
          {
            "name": "Benchmark.CLI.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 2363593.2192137986,
            "unit": "ns",
            "range": "± 98065.3301226507"
          }
        ]
      }
    ],
    "Azclips Compute CLI Benchmarks": [
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
        "date": 1785123308854,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1054601.1361800928,
            "unit": "ns",
            "range": "± 25237.552013364948"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1791898.4712259371,
            "unit": "ns",
            "range": "± 37829.39279707255"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1029226.6419816139,
            "unit": "ns",
            "range": "± 25173.458694735647"
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
        "date": 1785125676973,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1016802.2180428135,
            "unit": "ns",
            "range": "± 15973.004210580626"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1718413.6746421268,
            "unit": "ns",
            "range": "± 21371.152537761234"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 993941.2607883818,
            "unit": "ns",
            "range": "± 12425.052904408827"
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
        "date": 1785201415085,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1086473.1506482814,
            "unit": "ns",
            "range": "± 31091.977472555765"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1826131.0032713145,
            "unit": "ns",
            "range": "± 54031.65943722136"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1078250.7551349173,
            "unit": "ns",
            "range": "± 32341.6862859994"
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
        "date": 1785228525863,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1028646.2313064914,
            "unit": "ns",
            "range": "± 19222.36762902981"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1716433.1393121462,
            "unit": "ns",
            "range": "± 23208.030604461703"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 999559.4116732324,
            "unit": "ns",
            "range": "± 18325.594157836695"
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
        "date": 1785383742845,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1042085.2231960865,
            "unit": "ns",
            "range": "± 18381.022064426816"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1735124.8873637465,
            "unit": "ns",
            "range": "± 27704.04305700255"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 991687.5072930073,
            "unit": "ns",
            "range": "± 9885.0675858509"
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
        "date": 1785389156396,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1030005.4302841023,
            "unit": "ns",
            "range": "± 18882.545315771193"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1717461.837854758,
            "unit": "ns",
            "range": "± 19378.266774720552"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 996724.8783016809,
            "unit": "ns",
            "range": "± 11670.794174578368"
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
        "date": 1785392804584,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1047878.7531512605,
            "unit": "ns",
            "range": "± 20325.42143047788"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1769368.9772167488,
            "unit": "ns",
            "range": "± 26051.61433367167"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1036905.9092201462,
            "unit": "ns",
            "range": "± 17191.3437820901"
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
        "date": 1785451983100,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1030314.5622861743,
            "unit": "ns",
            "range": "± 22192.711753402265"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1762723.5163599183,
            "unit": "ns",
            "range": "± 20829.2689000049"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1019535.7123536536,
            "unit": "ns",
            "range": "± 19277.098132508898"
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
        "date": 1785731626397,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1025634.216636382,
            "unit": "ns",
            "range": "± 21190.90249501946"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1718099.7872802482,
            "unit": "ns",
            "range": "± 15853.247426337879"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1003351.0749005745,
            "unit": "ns",
            "range": "± 14272.488345475605"
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
        "date": 1785802414314,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1050315.6162227602,
            "unit": "ns",
            "range": "± 23655.30971763732"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1833295.4524453694,
            "unit": "ns",
            "range": "± 38246.59321411962"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1050539.3822686323,
            "unit": "ns",
            "range": "± 24695.90140156665"
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
        "date": 1785803883307,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1036527.9697979587,
            "unit": "ns",
            "range": "± 16492.958593231397"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1747072.6849959118,
            "unit": "ns",
            "range": "± 19955.870115465787"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1008715.8046384444,
            "unit": "ns",
            "range": "± 17288.251427385465"
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
        "date": 1785808420015,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1037926.1781029264,
            "unit": "ns",
            "range": "± 23160.34312270609"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1722630.0395918367,
            "unit": "ns",
            "range": "± 27636.365063154062"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1006378.215220455,
            "unit": "ns",
            "range": "± 24545.82651523337"
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
        "date": 1785818106743,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1027654.8062602291,
            "unit": "ns",
            "range": "± 16875.42865749982"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1743988.8802029407,
            "unit": "ns",
            "range": "± 22737.029233652847"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1016283.0606372046,
            "unit": "ns",
            "range": "± 14643.748543569758"
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
        "date": 1785819566334,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1119396.4421521875,
            "unit": "ns",
            "range": "± 23115.521001120163"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1862221.1873228028,
            "unit": "ns",
            "range": "± 54308.32186162174"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1110575.8839756593,
            "unit": "ns",
            "range": "± 30721.91590807147"
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
        "date": 1785904808142,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1078738.4733510313,
            "unit": "ns",
            "range": "± 18314.403823590794"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1812888.2521656454,
            "unit": "ns",
            "range": "± 26227.929217667475"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1050154.9632124351,
            "unit": "ns",
            "range": "± 17335.29450436334"
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
          "id": "ecc27f0c7cd5bf371e1ce102450d6070d21ddaf2",
          "message": "{Help} Validate in CI for PS Help drift (#655)\n\nFixes https://github.com/azure/azclips/issues/652\n\n## What\n\n- **CI gate** — new `build/Test-PSHelpMarkdown.ps1` diffs every\n`src/PS/<Module>/help/*.md` against live cmdlet reflection via PlatyPS\n1.0.1 `Test-` / `Import-` / `Update-` / `Compare-CommandHelp`. Fires\n`PSL13_SchemaFailure` (unparseable md) and `PSL14_ParameterDrift`\n(missing/orphan params, wrong `IsMandatory`, wrong SyntaxParameters\norder, missing Outputs). Wired into the existing `help-validate` job.\n- **Remediation** — `pwsh -File build/New-PSHelpMarkdown.ps1 -Module\n<name> -Refresh` regenerates any drifted `.md` in place. Gate error\nfooter points at this exact command.\n- **Content backfill** — every PS cmdlet now declares\n`[OutputType(...)]`; every `.md` refreshed. Closes #652\n(Connect-AzAccount had 6 undocumented parameters and `-SubscriptionId`\nmis-declared as required).\n- **Smoke fixtures** — `tests/Scripts/PS/HelpDriftFixtures.ps1` builds a\nscratch cmdlet + module in a temp dir and asserts on gate output across\n5 scenarios (clean, missing param, orphan param, required flipped,\nschema corrupt). Runs before the real-repo check in CI so a gate\nregression fails cleanly.\n\n## Notes on observable changes\n\n- Optional `-Profile` / `-SubscriptionId` parameters reordered to the\nend of declaration on VM and Resource cmdlets so the reflected SYNTAX\nmatches the docs' \"mandatory first\" convention. **No impact on\nnamed-parameter binding**; only affects `Get-Command | Select\nParameterSets` enumeration order.\n- Cmdlet `OutputType` now surfaces via reflection where it previously\ndefaulted to `System.Object`. Scripts that inspect `(Get-Command\n<name>).OutputType` will now see the real type name.\n- No public CLI surface touched.\n\n## History Notes\n\n- [Help] `Get-Help Connect-AzAccount` now documents all 11 parameters\nwith `-SubscriptionId` correctly marked optional (closes #652).\n- [Help] Every PS cmdlet advertises a real output type via\n`[OutputType]`; `Get-Help -Full` shows accurate `OUTPUTS` sections\ninstead of `System.Object`.\n\n## Validation\n\n- `pwsh -File build/Test-PSHelpMarkdown.ps1` — exit 0 across all 4\nmodules (Config, Login, Compute, Resources).\n- `pwsh -NoProfile -File tests/Scripts/PS/HelpDriftFixtures.ps1` — all 5\nscenarios green.\n- `dotnet run Azclips.Help.Tool` — no issues.\n- `Get-Help Connect-AzAccount -Full` spot-check: all 11 parameters,\ncorrect required flags, `System.Void` OUTPUTS with prose.",
          "timestamp": "2026-08-06T17:14:44+10:00",
          "tree_id": "39a74ce077bec2727e15c48fc8a3f495598ee509",
          "url": "https://github.com/Azure/azclips/commit/ecc27f0c7cd5bf371e1ce102450d6070d21ddaf2"
        },
        "date": 1786001279128,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1058015.7379550904,
            "unit": "ns",
            "range": "± 15299.43798486216"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1812224.071822077,
            "unit": "ns",
            "range": "± 24661.3219592565"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1062800.1959624796,
            "unit": "ns",
            "range": "± 14285.814772686048"
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
        "date": 1786065374147,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1072866.7820460978,
            "unit": "ns",
            "range": "± 18166.222103309836"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1787041.7558280965,
            "unit": "ns",
            "range": "± 28643.448181992488"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1042046.8621115456,
            "unit": "ns",
            "range": "± 17671.12523750699"
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
        "date": 1786086967955,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1106252.9077393075,
            "unit": "ns",
            "range": "± 17144.93496577308"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1844264.7596618359,
            "unit": "ns",
            "range": "± 53002.338657526336"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1080950.260716442,
            "unit": "ns",
            "range": "± 28583.82143181402"
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
        "date": 1786336789842,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1092388.7780933063,
            "unit": "ns",
            "range": "± 26323.291601014822"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1832041.7644095123,
            "unit": "ns",
            "range": "± 51001.54286669802"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1059216.9649158386,
            "unit": "ns",
            "range": "± 24088.847605841805"
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
        "date": 1786425676731,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1043273.5074657819,
            "unit": "ns",
            "range": "± 20822.95318561973"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1783034.9358531318,
            "unit": "ns",
            "range": "± 28332.086012593656"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1043198.793702059,
            "unit": "ns",
            "range": "± 29344.469180040414"
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
        "date": 1786514620377,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1055317.4265393212,
            "unit": "ns",
            "range": "± 22942.312995477532"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1746884.873818029,
            "unit": "ns",
            "range": "± 23511.839464835"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1019328.2697826978,
            "unit": "ns",
            "range": "± 19747.58438200829"
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
        "date": 1786599906177,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1054553.6862307067,
            "unit": "ns",
            "range": "± 14073.977633672548"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1784818.8464983713,
            "unit": "ns",
            "range": "± 22904.007529581846"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1017191.0770004021,
            "unit": "ns",
            "range": "± 19888.33156324577"
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
        "date": 1786927018074,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 949545.5986669359,
            "unit": "ns",
            "range": "± 16377.947153077539"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1361746.3316471064,
            "unit": "ns",
            "range": "± 16442.69612692116"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 933277.3459056071,
            "unit": "ns",
            "range": "± 18492.92934149887"
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
        "date": 1786931754477,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 988492.4195738458,
            "unit": "ns",
            "range": "± 16770.846135191598"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1431578.536107711,
            "unit": "ns",
            "range": "± 21178.73063675027"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 996249.0367010309,
            "unit": "ns",
            "range": "± 13399.288804656979"
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
        "date": 1786952640747,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 976730.5464629552,
            "unit": "ns",
            "range": "± 11861.371644454795"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1405741.338676948,
            "unit": "ns",
            "range": "± 21894.7453325342"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 965600.3254230293,
            "unit": "ns",
            "range": "± 12592.978280892721"
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
        "date": 1787009122756,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 998723.5296510383,
            "unit": "ns",
            "range": "± 10007.861857628703"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1437485.9457489878,
            "unit": "ns",
            "range": "± 19739.518307318278"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 988131.5163009404,
            "unit": "ns",
            "range": "± 11600.946515313128"
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
        "date": 1787019397180,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 972890.6852808263,
            "unit": "ns",
            "range": "± 12533.642126927"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1416561.6728320194,
            "unit": "ns",
            "range": "± 22206.167800491585"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 965799.4830438378,
            "unit": "ns",
            "range": "± 14175.375482228808"
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
        "date": 1787034579913,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 974139.6390058972,
            "unit": "ns",
            "range": "± 11951.74587549251"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1409355.0966115368,
            "unit": "ns",
            "range": "± 22215.039772810866"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 962846.7730175077,
            "unit": "ns",
            "range": "± 14523.24023441189"
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
        "date": 1787108389736,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 997437.0965885738,
            "unit": "ns",
            "range": "± 18811.841107271848"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1424239.4761759315,
            "unit": "ns",
            "range": "± 25475.203504329627"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 988969.9926241429,
            "unit": "ns",
            "range": "± 25016.076466841972"
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
        "date": 1787203428053,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1111919.674446925,
            "unit": "ns",
            "range": "± 24699.777575798413"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1550959.1863682605,
            "unit": "ns",
            "range": "± 37759.20740486437"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1095508.5268222769,
            "unit": "ns",
            "range": "± 23354.071194611708"
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
        "date": 1787530869792,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1117797.3519723464,
            "unit": "ns",
            "range": "± 21640.79480362748"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 1547340.4724529067,
            "unit": "ns",
            "range": "± 32072.659049718648"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1109098.081724846,
            "unit": "ns",
            "range": "± 21102.330505792925"
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
        "date": 1787613124898,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1521913.8345469255,
            "unit": "ns",
            "range": "± 32450.43837503087"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 3646514.717737003,
            "unit": "ns",
            "range": "± 54336.46381737488"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1500335.2540052729,
            "unit": "ns",
            "range": "± 32132.876631360163"
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
        "date": 1787672907419,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1370696.3261136084,
            "unit": "ns",
            "range": "± 29759.42330632153"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 3241860.916474049,
            "unit": "ns",
            "range": "± 46440.049114201116"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1367813.1425071459,
            "unit": "ns",
            "range": "± 29958.45338584089"
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
        "date": 1787809701631,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1346505.67587737,
            "unit": "ns",
            "range": "± 29405.883073395926"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 3185511.3294093767,
            "unit": "ns",
            "range": "± 50384.381652141106"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1328575.184286007,
            "unit": "ns",
            "range": "± 28752.69729254666"
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
        "date": 1787898123046,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 1361770.4790068632,
            "unit": "ns",
            "range": "± 29193.929678775756"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 3231268.689443535,
            "unit": "ns",
            "range": "± 41646.52586316857"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 1347979.0135847526,
            "unit": "ns",
            "range": "± 29603.406563465796"
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
        "date": 1788142313866,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2479617.6635270542,
            "unit": "ns",
            "range": "± 94521.3014263739"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4873988.552241834,
            "unit": "ns",
            "range": "± 128542.15921032711"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2490689.5634568897,
            "unit": "ns",
            "range": "± 109287.39256432884"
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
        "date": 1788154319019,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2463137.79107322,
            "unit": "ns",
            "range": "± 94704.47570829962"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4867966.347773279,
            "unit": "ns",
            "range": "± 132325.5429900195"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2468789.4378416683,
            "unit": "ns",
            "range": "± 100603.14242258077"
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
        "date": 1788219408567,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2540115.7599838646,
            "unit": "ns",
            "range": "± 96131.31612270584"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 5006316.942862952,
            "unit": "ns",
            "range": "± 123420.42885739982"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2519389.047725882,
            "unit": "ns",
            "range": "± 94920.39122209602"
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
        "date": 1788228689814,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2499807.6186164804,
            "unit": "ns",
            "range": "± 92325.33042152616"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4852788.970600329,
            "unit": "ns",
            "range": "± 109712.0356064196"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2478311.170938446,
            "unit": "ns",
            "range": "± 106532.331116653"
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
        "date": 1788234493292,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2461190.4959871587,
            "unit": "ns",
            "range": "± 89854.22155961652"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4840095.873481781,
            "unit": "ns",
            "range": "± 118802.01875851686"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2470583.1761031635,
            "unit": "ns",
            "range": "± 105477.08944048468"
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
          "id": "e83849c8645fa4685a532a2c113c135694fae698",
          "message": "[NonSquash] Update extension interface, add default pagination (#777)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-02T09:36:26+10:00",
          "tree_id": "4b3df278ec7341120cead6fc2ae49fecf83bb2b2",
          "url": "https://github.com/Azure/azclips/commit/e83849c8645fa4685a532a2c113c135694fae698"
        },
        "date": 1788306919001,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2468196.564746035,
            "unit": "ns",
            "range": "± 92246.77464164258"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4931605.790975314,
            "unit": "ns",
            "range": "± 125936.7644250497"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2467197.1897736457,
            "unit": "ns",
            "range": "± 105403.5525166842"
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
          "id": "3991c8f38ae33084a243d292c2473d1139f5008f",
          "message": "{Tests} Run scenario scripts through the real client binary (#784)\n\nScenario tests now exercise the real az binary instead of an in-process host, following the review feedback on the scenario tier (CLIPS task #734). A test case is a script run by a real shell, one process per command, chaining values the way a user would.\n\nThe product side is small: the recording primitives move from Tests.Common into Core.Recording, and the two client composition roots call RecordingHook.TryRegister()\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-03T10:49:26+10:00",
          "tree_id": "a66a6b7ec1131bfbc6fadbfe2a6ccb8f5ca156ff",
          "url": "https://github.com/Azure/azclips/commit/3991c8f38ae33084a243d292c2473d1139f5008f"
        },
        "date": 1788397490306,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2557949.4198872787,
            "unit": "ns",
            "range": "± 98918.26768772626"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 5121989.249798061,
            "unit": "ns",
            "range": "± 192691.31520408762"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2561861.0631451122,
            "unit": "ns",
            "range": "± 135517.97049448825"
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
          "id": "ea968585c36fe97794420981ec677e6c391bc403",
          "message": "{Codegen} phase 1 integration: Cli+Compiler+IR+Emitter+Generator (#788)\n\n**Related command**\n<!-- If applicable, provide the related command, for example: az vm\ncreate, az login, or Az.Compute. -->\n\n**Description**\n<!-- Why this PR? What changed? What is the user or maintainer impact?\n-->\n\n**Testing Guide**\n<!-- List validation commands or manual test steps. -->\n\n**History Notes**\n<!--\nRelease notes are generated from PR metadata during stable release\npreparation.\n\nUse [Component Name] for customer-facing changes that should appear in\nrelease notes.\nUse {Component Name} for internal, test-only, docs-only, or\nrelease-process changes that should not appear in release notes.\n\nPR titles must not end with trailing punctuation or trailing spaces.\n\nIf one PR needs multiple release-note entries, or if the PR title is\nonly a summary, add notes below in this format:\n\n[Component Name] `az command`: Add customer-facing behavior\n[Component Name] BREAKING CHANGE: `az command`: Remove deprecated\nbehavior\n\nHistory note lines must not end with trailing punctuation or trailing\nspaces.\n\nCommon components: CLI, PowerShell, Agent, Auth, Packaging, DevOps,\nDocs.\nDelete this section only when no release note is needed.\n-->\n\n---\n\n- [ ] The PR title starts with `[Component Name]` for customer-facing\nchanges or `{Component Name}` for non-customer-facing changes, and does\nnot end with trailing punctuation or spaces.\n- [ ] Customer-facing changes include a useful release note in the PR\ntitle or the **History Notes** section.\n- [ ] I added or updated tests for behavior changes.\n- [ ] I updated documentation when behavior, setup, support, or security\nguidance changed.",
          "timestamp": "2026-09-03T16:29:06+10:00",
          "tree_id": "9cda8d8204ef0df979b1ad6632b6ba8e57a4a815",
          "url": "https://github.com/Azure/azclips/commit/ea968585c36fe97794420981ec677e6c391bc403"
        },
        "date": 1788417795983,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2553528.74696814,
            "unit": "ns",
            "range": "± 106679.75178473518"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4984421.569661327,
            "unit": "ns",
            "range": "± 123263.72967257167"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2540127.249848332,
            "unit": "ns",
            "range": "± 107828.25636412489"
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
        "date": 1788501873036,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2440909.126856684,
            "unit": "ns",
            "range": "± 90792.76607172779"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4840988.804957334,
            "unit": "ns",
            "range": "± 126106.06407862071"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2441481.5702179177,
            "unit": "ns",
            "range": "± 102252.29319441094"
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
        "date": 1788739768926,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2551271.2843668475,
            "unit": "ns",
            "range": "± 94244.9732283635"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 5013851.295694557,
            "unit": "ns",
            "range": "± 134560.63387127244"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2539195.3848337387,
            "unit": "ns",
            "range": "± 110926.68135571113"
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
        "date": 1788752954305,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2552205.5354851643,
            "unit": "ns",
            "range": "± 92907.24639002906"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 5009037.107099637,
            "unit": "ns",
            "range": "± 126632.72851889904"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2547419.7801296334,
            "unit": "ns",
            "range": "± 106642.00057885342"
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
        "date": 1788768005522,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2557986.707297514,
            "unit": "ns",
            "range": "± 95382.79829294492"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4959124.811156186,
            "unit": "ns",
            "range": "± 148540.399919333"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2538825.220297534,
            "unit": "ns",
            "range": "± 111606.9928496649"
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
        "date": 1788833973547,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2527265.3571141884,
            "unit": "ns",
            "range": "± 97603.98261682178"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 4964118.195449005,
            "unit": "ns",
            "range": "± 127113.10332610406"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2514638.3737024223,
            "unit": "ns",
            "range": "± 104740.53617229771"
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
        "date": 1788842120074,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.ListVirtualMachines",
            "value": 2562721.678664528,
            "unit": "ns",
            "range": "± 103846.88878306434"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.CreateVirtualMachine",
            "value": 5039895.313186813,
            "unit": "ns",
            "range": "± 141789.6799100363"
          },
          {
            "name": "Benchmark.CLI.VirtualMachineBenchmarks.GetVirtualMachine",
            "value": 2535442.6121911705,
            "unit": "ns",
            "range": "± 108661.81555868317"
          }
        ]
      }
    ],
    "Azclips Identity CLI Benchmarks": [
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
          "id": "f41339f9ab8fdf2f07030c5c0e2739f348add324",
          "message": "{Benchmark} `Extensions.Identity`: Add performance benchmark tests (#817)",
          "timestamp": "2026-09-07T10:59:04+08:00",
          "tree_id": "c4320a24470dc060f32da04ee02bb27a57753e24",
          "url": "https://github.com/Azure/azclips/commit/f41339f9ab8fdf2f07030c5c0e2739f348add324"
        },
        "date": 1788754948692,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.ListUserAssignedIdentities",
            "value": 2442109.5361228175,
            "unit": "ns",
            "range": "± 93409.9363236753"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.CreateUserAssignedIdentity",
            "value": 2448703.313705788,
            "unit": "ns",
            "range": "± 106993.04140383651"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.GetUserAssignedIdentity",
            "value": 2419167.531908489,
            "unit": "ns",
            "range": "± 112870.86831928839"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.UpdateUserAssignedIdentity",
            "value": 2304120.442369478,
            "unit": "ns",
            "range": "± 104301.54958749734"
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
        "date": 1788768056807,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.ListUserAssignedIdentities",
            "value": 2432307.3456665995,
            "unit": "ns",
            "range": "± 97682.81701637815"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.CreateUserAssignedIdentity",
            "value": 2411045.195288907,
            "unit": "ns",
            "range": "± 90246.71035156473"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.GetUserAssignedIdentity",
            "value": 2391318.7781683067,
            "unit": "ns",
            "range": "± 100628.8220522125"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.UpdateUserAssignedIdentity",
            "value": 2291406.712731668,
            "unit": "ns",
            "range": "± 97888.73171761387"
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
        "date": 1788834034420,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.ListUserAssignedIdentities",
            "value": 2430462.3670607093,
            "unit": "ns",
            "range": "± 95275.88974792972"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.CreateUserAssignedIdentity",
            "value": 2445858.9379635197,
            "unit": "ns",
            "range": "± 102926.96301113948"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.GetUserAssignedIdentity",
            "value": 2411404.34305835,
            "unit": "ns",
            "range": "± 100488.38233861476"
          },
          {
            "name": "Benchmark.CLI.UserAssignedIdentityBenchmarks.UpdateUserAssignedIdentity",
            "value": 2304580.8089594212,
            "unit": "ns",
            "range": "± 104873.4704229376"
          }
        ]
      }
    ]
  }
}