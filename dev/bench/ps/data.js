window.BENCHMARK_DATA = {
  "lastUpdate": 1781243499956,
  "repoUrl": "https://github.com/Azure/cirrus",
  "entries": {
    "Cirrus PS Benchmarks": [
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
        "date": 1778815408854,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23280221.8,
            "unit": "ns",
            "range": "± 2148242.5364787607"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 23031720.2,
            "unit": "ns",
            "range": "± 2348817.1265382073"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 22952618.8,
            "unit": "ns",
            "range": "± 2150444.169441792"
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
        "date": 1778927952466,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23044144.8,
            "unit": "ns",
            "range": "± 2463120.948304955"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22315977.4,
            "unit": "ns",
            "range": "± 2849797.3938796595"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21528406.2,
            "unit": "ns",
            "range": "± 2122087.142965906"
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
        "date": 1778928792480,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21859827.4,
            "unit": "ns",
            "range": "± 3259288.3877778905"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22284199.4,
            "unit": "ns",
            "range": "± 1990767.5649085203"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21564648.4,
            "unit": "ns",
            "range": "± 1990033.3949204972"
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
        "date": 1778929323511,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 20919511,
            "unit": "ns",
            "range": "± 2154966.257027242"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 21617593.4,
            "unit": "ns",
            "range": "± 2165270.997954367"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21277373,
            "unit": "ns",
            "range": "± 2213368.0854691793"
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
        "date": 1779896085236,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21938465.8,
            "unit": "ns",
            "range": "± 2568733.795770535"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 21534762.6,
            "unit": "ns",
            "range": "± 1802278.0446749332"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21695570.4,
            "unit": "ns",
            "range": "± 1436226.0681530258"
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
        "date": 1780011358228,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 17216161.2,
            "unit": "ns",
            "range": "± 2348690.5332996086"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 16879282.6,
            "unit": "ns",
            "range": "± 1826392.1902910941"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 19056653,
            "unit": "ns",
            "range": "± 7807308.797720473"
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
        "date": 1780388055824,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 16916769.2,
            "unit": "ns",
            "range": "± 1971609.289895237"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 17074052,
            "unit": "ns",
            "range": "± 1821838.7158605726"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 16407861,
            "unit": "ns",
            "range": "± 1904804.173371242"
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
        "date": 1780408842739,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21659660.2,
            "unit": "ns",
            "range": "± 2394755.2695245706"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 20224610.8,
            "unit": "ns",
            "range": "± 2272394.994376572"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 20611257.8,
            "unit": "ns",
            "range": "± 2222456.012140893"
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
        "date": 1780533435868,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23599466.4,
            "unit": "ns",
            "range": "± 3399451.382650192"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22093698.4,
            "unit": "ns",
            "range": "± 2025696.4590287954"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 20988343.4,
            "unit": "ns",
            "range": "± 2007318.3292442183"
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
        "date": 1780550560615,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 20709249.2,
            "unit": "ns",
            "range": "± 1965202.800034312"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 20966936.4,
            "unit": "ns",
            "range": "± 2418996.9510100256"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 20789633.6,
            "unit": "ns",
            "range": "± 2007257.6751496803"
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
        "date": 1780986404276,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 32146372.4,
            "unit": "ns",
            "range": "± 3684937.569220244"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22666956.8,
            "unit": "ns",
            "range": "± 2444458.8234644495"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 22049951.4,
            "unit": "ns",
            "range": "± 1820252.2008491845"
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
        "date": 1781064902868,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 18124913,
            "unit": "ns",
            "range": "± 1965474.39472014"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 17748753.6,
            "unit": "ns",
            "range": "± 1675745.6980876604"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 18291703.8,
            "unit": "ns",
            "range": "± 1807142.097965376"
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
        "date": 1781074504739,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 22723597.8,
            "unit": "ns",
            "range": "± 2365165.7442375366"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22495365.8,
            "unit": "ns",
            "range": "± 2534919.5924274796"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21752417.6,
            "unit": "ns",
            "range": "± 2511621.935883026"
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
        "date": 1781102954611,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 22326331.4,
            "unit": "ns",
            "range": "± 1922006.5771207183"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22996298,
            "unit": "ns",
            "range": "± 1764172.0434879644"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 24577706.2,
            "unit": "ns",
            "range": "± 2880401.759565443"
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
        "date": 1781164533049,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21827861.4,
            "unit": "ns",
            "range": "± 1770780.6128722723"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 21769994.6,
            "unit": "ns",
            "range": "± 1699281.1012303997"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21982531.2,
            "unit": "ns",
            "range": "± 2482822.0035266723"
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
        "date": 1781242981989,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 20026794.8,
            "unit": "ns",
            "range": "± 2486289.109943713"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 19512750,
            "unit": "ns",
            "range": "± 4164437.086983246"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 18086945.2,
            "unit": "ns",
            "range": "± 1786647.4731198933"
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
        "date": 1781243499883,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21163716,
            "unit": "ns",
            "range": "± 2164166.2775134677"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 21130334.4,
            "unit": "ns",
            "range": "± 1863282.9422318821"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21671206,
            "unit": "ns",
            "range": "± 2857225.1591638695"
          }
        ]
      }
    ]
  }
}