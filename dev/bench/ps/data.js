window.BENCHMARK_DATA = {
  "lastUpdate": 1783468030335,
  "repoUrl": "https://github.com/Azure/azclips",
  "entries": {
    "Azclips PS Benchmarks": [
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
        "date": 1781246655443,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21153343.6,
            "unit": "ns",
            "range": "± 3134162.885886405"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 20360482.2,
            "unit": "ns",
            "range": "± 2209807.177463047"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 26549560.4,
            "unit": "ns",
            "range": "± 7840531.994990155"
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
        "date": 1781498148730,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 20258134.4,
            "unit": "ns",
            "range": "± 1970532.8906942406"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 20142502.8,
            "unit": "ns",
            "range": "± 2141576.8044232503"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 20405688.4,
            "unit": "ns",
            "range": "± 2113464.686912535"
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
        "date": 1781589919408,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 20298089.8,
            "unit": "ns",
            "range": "± 2339446.629335386"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 19840388,
            "unit": "ns",
            "range": "± 2300839.267322361"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 20274798,
            "unit": "ns",
            "range": "± 2208828.1926574777"
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
        "date": 1781590006843,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 22726165.2,
            "unit": "ns",
            "range": "± 2705658.8858393994"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22418451.6,
            "unit": "ns",
            "range": "± 2480786.488514842"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 23532171,
            "unit": "ns",
            "range": "± 1811432.1005302684"
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
        "date": 1781662737378,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23222261.4,
            "unit": "ns",
            "range": "± 2232256.074553231"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 24376612,
            "unit": "ns",
            "range": "± 1804524.4619741235"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 24138526.4,
            "unit": "ns",
            "range": "± 2790326.4813349885"
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
        "date": 1781745795764,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 16469785.8,
            "unit": "ns",
            "range": "± 1902372.8204775478"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 16495842.6,
            "unit": "ns",
            "range": "± 2339842.7466912814"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 16105420.2,
            "unit": "ns",
            "range": "± 1902680.6276405666"
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
        "date": 1781759615957,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 22143079.2,
            "unit": "ns",
            "range": "± 1624816.7855326643"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22461911.6,
            "unit": "ns",
            "range": "± 1720378.0659706166"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21793184.2,
            "unit": "ns",
            "range": "± 2298254.1516389563"
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
        "date": 1781766757060,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 20844589,
            "unit": "ns",
            "range": "± 2029807.7711402872"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 20660298,
            "unit": "ns",
            "range": "± 2201500.8106176113"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 20916454,
            "unit": "ns",
            "range": "± 2151664.466039373"
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
        "date": 1781831147792,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 29559195.8,
            "unit": "ns",
            "range": "± 8831377.868505158"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 22446886.6,
            "unit": "ns",
            "range": "± 1709500.5478860778"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21203067,
            "unit": "ns",
            "range": "± 2619760.991664793"
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
        "date": 1781842922419,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21267961.2,
            "unit": "ns",
            "range": "± 2810985.7028172878"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 20704031.4,
            "unit": "ns",
            "range": "± 2054392.8719843973"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 21219983.4,
            "unit": "ns",
            "range": "± 2199897.312141137"
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
        "date": 1781856279947,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 23259675.6,
            "unit": "ns",
            "range": "± 2366790.519366532"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 24000872.6,
            "unit": "ns",
            "range": "± 2249543.4026041375"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 22690316.8,
            "unit": "ns",
            "range": "± 2063715.046131539"
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
        "date": 1782103407671,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 21551122.2,
            "unit": "ns",
            "range": "± 1700120.4092658262"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 21423370.4,
            "unit": "ns",
            "range": "± 1960593.1513657544"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 22047117,
            "unit": "ns",
            "range": "± 1787550.0965195354"
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
        "date": 1782108273211,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 15908794.142205324,
            "unit": "ns",
            "range": "± 528196.4422210145"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 15727056.81911447,
            "unit": "ns",
            "range": "± 475442.3917361827"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 15747425.433210652,
            "unit": "ns",
            "range": "± 466218.3337324247"
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
        "date": 1782131565982,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 13568625.628182996,
            "unit": "ns",
            "range": "± 542548.4408569074"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 13783355.170476813,
            "unit": "ns",
            "range": "± 484077.5019584347"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 14044663.168449782,
            "unit": "ns",
            "range": "± 391865.6000330863"
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
        "date": 1782201220599,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 13869986.035679374,
            "unit": "ns",
            "range": "± 453333.79037106776"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 13912978.450689692,
            "unit": "ns",
            "range": "± 490928.9854841383"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 14116528.00454447,
            "unit": "ns",
            "range": "± 573237.0428544746"
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
        "date": 1782269188951,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5750754.518020073,
            "unit": "ns",
            "range": "± 101257.8428315143"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5759701.460991698,
            "unit": "ns",
            "range": "± 102603.84314632122"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5723605.203841317,
            "unit": "ns",
            "range": "± 101964.54767407787"
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
        "date": 1782271225004,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5753425.401549681,
            "unit": "ns",
            "range": "± 99931.96674123337"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5722368.890692888,
            "unit": "ns",
            "range": "± 103134.69010620813"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5708456.557705441,
            "unit": "ns",
            "range": "± 103030.65801512182"
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
        "date": 1782303450995,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5762802.744573909,
            "unit": "ns",
            "range": "± 97124.26189594353"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5722458.079746546,
            "unit": "ns",
            "range": "± 96881.3963809784"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5738820.926648415,
            "unit": "ns",
            "range": "± 97768.57242114852"
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
        "date": 1782320891178,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5749434.665835694,
            "unit": "ns",
            "range": "± 103206.94035263063"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5733191.549329393,
            "unit": "ns",
            "range": "± 102528.61791272102"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5739274.185291418,
            "unit": "ns",
            "range": "± 101238.45849755453"
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
        "date": 1782357442595,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5821897.669398907,
            "unit": "ns",
            "range": "± 103571.50668553212"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5800373.962662708,
            "unit": "ns",
            "range": "± 100719.05119969469"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5790972.318750718,
            "unit": "ns",
            "range": "± 100107.92406038813"
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
        "date": 1782371348452,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5812461.367911638,
            "unit": "ns",
            "range": "± 95903.88997301948"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5787701.788593679,
            "unit": "ns",
            "range": "± 96551.36732240983"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5779354.115230575,
            "unit": "ns",
            "range": "± 96121.50473650466"
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
        "date": 1782695872387,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5833603.00967118,
            "unit": "ns",
            "range": "± 100004.81410294924"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5799930.360310042,
            "unit": "ns",
            "range": "± 103217.15129753575"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5786198.347504303,
            "unit": "ns",
            "range": "± 102350.87125913876"
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
        "date": 1782779236183,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5797797.942064486,
            "unit": "ns",
            "range": "± 98360.94317545301"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5773324.308461978,
            "unit": "ns",
            "range": "± 96637.78141468091"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5769104.825451222,
            "unit": "ns",
            "range": "± 97082.52901775466"
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
        "date": 1782782800727,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5768917.414659208,
            "unit": "ns",
            "range": "± 103018.75561142455"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5730289.860886408,
            "unit": "ns",
            "range": "± 104343.97113357892"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5731374.405719494,
            "unit": "ns",
            "range": "± 101334.37069614763"
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
        "date": 1782791234536,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5776086.611776789,
            "unit": "ns",
            "range": "± 102704.20798662075"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5752381.146696337,
            "unit": "ns",
            "range": "± 99032.87729633632"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5755511.005198218,
            "unit": "ns",
            "range": "± 98685.98213342404"
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
        "date": 1782889197003,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5795794.322558538,
            "unit": "ns",
            "range": "± 99606.84851278177"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5762388.996344528,
            "unit": "ns",
            "range": "± 101695.75049662862"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5771243.8102937825,
            "unit": "ns",
            "range": "± 101246.48363587959"
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
        "date": 1782892542208,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5775798.1447293125,
            "unit": "ns",
            "range": "± 98235.95820914174"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5774496.001149822,
            "unit": "ns",
            "range": "± 98473.61199253701"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5766203.064008274,
            "unit": "ns",
            "range": "± 100646.54502483024"
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
        "date": 1782954931286,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5763616.007391403,
            "unit": "ns",
            "range": "± 101536.08091493974"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5750826.923635539,
            "unit": "ns",
            "range": "± 102159.61381233635"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5728787.367412141,
            "unit": "ns",
            "range": "± 102454.49592510394"
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
        "date": 1782968935431,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5793468.626415094,
            "unit": "ns",
            "range": "± 100098.39983152329"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5770849.623928694,
            "unit": "ns",
            "range": "± 99462.2046161461"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5757644.323358743,
            "unit": "ns",
            "range": "± 99583.15393003619"
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
        "date": 1782969800725,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5756667.43492209,
            "unit": "ns",
            "range": "± 100484.12098459247"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5759598.425947056,
            "unit": "ns",
            "range": "± 100843.85715801489"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5752213.17331813,
            "unit": "ns",
            "range": "± 99938.39392499298"
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
        "date": 1782974887448,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5792619.107257326,
            "unit": "ns",
            "range": "± 97130.53261801129"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5743086.701750372,
            "unit": "ns",
            "range": "± 94995.20170837357"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5743695.701748332,
            "unit": "ns",
            "range": "± 98714.70644198732"
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
        "date": 1782978243650,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5794950.591686761,
            "unit": "ns",
            "range": "± 97415.23913851535"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5786897.988149499,
            "unit": "ns",
            "range": "± 98724.72979638811"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5769324.032334294,
            "unit": "ns",
            "range": "± 99951.48562629212"
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
        "date": 1782994535404,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5809477.89348371,
            "unit": "ns",
            "range": "± 98704.96155038485"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5786668.094445719,
            "unit": "ns",
            "range": "± 99247.55554711432"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5773155.639236151,
            "unit": "ns",
            "range": "± 101074.48634822734"
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
        "date": 1783035127446,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5787674.101556242,
            "unit": "ns",
            "range": "± 99007.10070914666"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5787857.711314286,
            "unit": "ns",
            "range": "± 99347.44706182445"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5761396.664492505,
            "unit": "ns",
            "range": "± 102903.83406567267"
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
        "date": 1783036027680,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5774812.716137172,
            "unit": "ns",
            "range": "± 99631.79196459315"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5752724.577541938,
            "unit": "ns",
            "range": "± 100136.84563975417"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5759488.956531686,
            "unit": "ns",
            "range": "± 100836.34625278106"
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
        "date": 1783040657365,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5787219.28125,
            "unit": "ns",
            "range": "± 99699.28918344398"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5751108.524859984,
            "unit": "ns",
            "range": "± 103105.05145149752"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5758461.181071836,
            "unit": "ns",
            "range": "± 100010.72248058081"
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
        "date": 1783051850608,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5771105.424974248,
            "unit": "ns",
            "range": "± 97741.99310843303"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5725414.35162625,
            "unit": "ns",
            "range": "± 97804.96648710303"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5756699.282973345,
            "unit": "ns",
            "range": "± 98647.96365467137"
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
        "date": 1783056095231,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5779371.130548899,
            "unit": "ns",
            "range": "± 98802.05770781505"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5747590.907394767,
            "unit": "ns",
            "range": "± 99881.34461228659"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5754154.503318837,
            "unit": "ns",
            "range": "± 99871.81027413181"
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
        "date": 1783321291165,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5787890.70237823,
            "unit": "ns",
            "range": "± 95889.08599016038"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5746329.100749659,
            "unit": "ns",
            "range": "± 97728.909508086"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5761178.835231154,
            "unit": "ns",
            "range": "± 97406.16441852813"
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
        "date": 1783349323642,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5814402.396311522,
            "unit": "ns",
            "range": "± 100811.87956387704"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5806057.514837977,
            "unit": "ns",
            "range": "± 100193.21250808427"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5806304.17916762,
            "unit": "ns",
            "range": "± 100933.86801669425"
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
        "date": 1783382315864,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5803730.193694715,
            "unit": "ns",
            "range": "± 98616.72853744464"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5796181.00647139,
            "unit": "ns",
            "range": "± 96342.44307168377"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5786170.686104784,
            "unit": "ns",
            "range": "± 100699.553127895"
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
        "date": 1783385192880,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5930019.526064193,
            "unit": "ns",
            "range": "± 103240.44170724484"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5880874.128935106,
            "unit": "ns",
            "range": "± 105737.67654583469"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5882696.5857664235,
            "unit": "ns",
            "range": "± 104600.92925108701"
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
        "date": 1783391744093,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5807689.239903625,
            "unit": "ns",
            "range": "± 93663.05866015099"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5783974.690339934,
            "unit": "ns",
            "range": "± 99850.57719569943"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5800133.047646252,
            "unit": "ns",
            "range": "± 98507.42694286394"
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
        "date": 1783394875653,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5826669.951923077,
            "unit": "ns",
            "range": "± 101438.77731347487"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5788624.9843928,
            "unit": "ns",
            "range": "± 100365.45011546854"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5798937.666019417,
            "unit": "ns",
            "range": "± 98779.56120544294"
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
        "date": 1783406810231,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5931512.147347965,
            "unit": "ns",
            "range": "± 101525.12483792605"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5886008.4436739795,
            "unit": "ns",
            "range": "± 100901.65944263624"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5898710.63350486,
            "unit": "ns",
            "range": "± 98601.64614165568"
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
        "date": 1783412608500,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5837895.712746551,
            "unit": "ns",
            "range": "± 106544.50530416006"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5814399.887340036,
            "unit": "ns",
            "range": "± 106220.83165955091"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5761869.44758296,
            "unit": "ns",
            "range": "± 99102.27188482364"
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
        "date": 1783468030303,
        "tool": "benchmarkdotnet",
        "benches": [
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.ListResourceGroups",
            "value": 5826089.899988531,
            "unit": "ns",
            "range": "± 92486.33191350216"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.CreateResourceGroup",
            "value": 5808225.276818181,
            "unit": "ns",
            "range": "± 107479.83537153128"
          },
          {
            "name": "Benchmark.PS.ResourceGroupBenchmarks.GetResourceGroup",
            "value": 5812184.179491623,
            "unit": "ns",
            "range": "± 103019.41572484082"
          }
        ]
      }
    ]
  }
}