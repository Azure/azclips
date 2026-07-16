---
layout: home
title: "Azclips Wiki"
description: "Technical documentation for Project Azclips"

hero:
  name: "Azclips"
  text: "Technical Wiki"
  tagline: "A .NET 10 extension-based CLI (the `az` binary) for Azure resource management — with an out-of-process AI agent fallback, an interactive Terminal UI, and mirrored PowerShell cmdlets"
  image:
    src: /logo.svg
    alt: Azclips
  actions:
    - theme: brand
      text: Get Started
      link: /01-getting-started/overview
    - theme: alt
      text: Onboarding Guide
      link: /onboarding-guide
    - theme: alt
      text: Deep Dive
      link: /02-deep-dive/core-engine-orchestration

features:
  - icon: 🧩
    title: Extension Architecture
    details: Dynamic extension loading from NuGet packages and local DLLs with assembly isolation and multi-version management.
  - icon: 🤖
    title: Out-of-Process AI Agent
    details: On failure the CLI spawns a separate azclips-agent process (not the TUI), running ChatAgent on the GitHub Copilot SDK with a bundled native daemon.
  - icon: 🖥️
    title: Interactive Terminal UI
    details: A full-screen TUI that browses Azure resources as a virtual filesystem, sharing the same composition root and engine as the CLI.
  - icon: 🔧
    title: Multiple Client Surfaces
    details: CLI (az), PowerShell cmdlets, TUI, and agent-mode all share one AzclipsServiceProvider DI composition root and engine.
  - icon: 🔐
    title: MSAL Authentication & Profiles
    details: Microsoft Authentication Library with persistent token cache, device-code/interactive flows, and named connection profiles under ~/.azclips.
  - icon: 📊
    title: Telemetry & Observability
    details: System.Diagnostics activities with a privacy-preserving filter model and a detached background uploader to well-known telemetry endpoints.
---

## What is Azclips?

**Azclips** is a .NET 10 extension-based CLI framework — shipped as the `az` binary — for Azure resource management. The GitHub repository is named **`Azure/cirrus`**, but every namespace, binary, and on-disk state directory (`~/.azclips`) uses the **Azclips** identity; they are the same codebase, not two products.

Four client surfaces share one `AzclipsServiceProvider` dependency-injection composition root and the same `IEngine`/`ExecutionSession` pipeline:

- **CLI** (`az`) — structured, extensible commands backed by dynamically loaded extensions (Compute, Config, Login, Resources, ...).
- **PowerShell cmdlets** (`src/PS/`) — the same engine, wired through `CmdletOutputStreamsFactory` into `$PSCmdlet.WriteObject`.
- **Interactive Terminal UI** — a full-screen shell (built on the Hex1b widget library) that browses Azure resources as a virtual filesystem.
- **Out-of-process AI agent fallback** — when a command fails, the CLI spawns a **separate** `azclips-agent` process built on the **GitHub Copilot SDK** (driving a bundled `copilot` daemon over stdio), *not* an in-process Azure OpenAI call — the agent proposes a plan and waits for user approval before executing anything.

## Quick Navigation

| Section                                                                   | Description                                                     |
|----------------------------------------------------------------------------|-------------------------------------------------------------------|
| [🚀 Principal-Level Guide](/onboarding-guide)                             | Dense architectural deep-dive for senior engineers                |
| [📚 Zero to Hero](/zero-to-hero-guide)                                    | Progressive learning path from foundations to contributing        |
| [📖 Overview & Architecture](/01-getting-started/overview)                | Project architecture, the four client surfaces, and key concepts  |
| [⚙️ Environment Setup](/01-getting-started/environment-setup)             | .NET 10 SDK, solution layout, and build prerequisites              |
| [💻 CLI Usage](/01-getting-started/cli-usage)                             | `az` command-line interface guide, flags, and error handling      |
| [🐚 PowerShell Usage](/01-getting-started/powershell-usage)               | PowerShell module and cmdlet usage                                 |
| [🖥️ TUI Quickstart](/01-getting-started/tui-quickstart)                  | Launching and driving the interactive Terminal UI                  |
| [🤖 Agent Fallback Quickstart](/01-getting-started/agent-mode-quickstart) | Opting into and using the AI agent fallback                        |
| [🧠 Agent Architecture](/02-deep-dive/agent-architecture)                 | The standalone `azclips-agent` process lifecycle                   |
| [🔌 Copilot SDK Integration](/02-deep-dive/copilot-sdk-integration)       | How the GitHub Copilot SDK and bundled daemon are wired in         |
| [🛠️ Agent Tools](/02-deep-dive/agent-tools)                              | The full tool catalog: env, help, resource cache, sku, shell       |
| [📋 Prompts & Plan Mode](/02-deep-dive/prompts-and-plan-mode)             | System prompts and the propose-plan/submit-response protocol       |
| [🎨 Rendering & Terminal UX](/02-deep-dive/rendering-and-terminal-ux)     | The agent console renderer vs. the TUI's full-screen renderer      |
| [🔀 TUI ↔ Agent Fallback Interplay](/02-deep-dive/tui-agent-mode)         | How a redirected `az` child process changes fallback behavior      |
| [🗂️ TUI Interactive Shell & Azure VFS](/02-deep-dive/tui-interactive-shell) | The Azure virtual filesystem and shell ergonomics                |
| [🔬 Core Engine & Orchestration](/02-deep-dive/core-engine-orchestration) | The composition root, engine, and session model                    |
| [🌐 HTTP Pipeline & Runtime](/02-deep-dive/http-pipeline-runtime)         | Delegating-handler chain and runtime execution layer                |
| [🔐 Authentication](/02-deep-dive/authentication)                         | MSAL auth, token cache, and credential factories                    |
| [🧩 Extension System](/02-deep-dive/extension-system)                    | Dynamic extension loading, resolution, and compatibility           |
| [🐚 PowerShell Cmdlet Architecture](/02-deep-dive/powershell-cmdlet-architecture) | How cmdlets share the CLI's engine                          |
| [⚙️ Configuration Management](/02-deep-dive/configuration-management)     | Typed config, schema validation, and the `az config` surface       |
| [📊 Telemetry & Observability](/02-deep-dive/telemetry-observability)     | Privacy-preserving telemetry capture and upload                     |
| [📦 Packaging & Distribution](/02-deep-dive/packaging-and-distribution)   | Build flavors, signing, staging, and install scripts                |
| [🧪 Testing & Build Pipeline](/02-deep-dive/testing-and-build)            | Test projects, CI gates, and the build toolchain                    |
| [📈 Benchmark Dashboard (CLI)](/dev/bench/cli/index.html)                 | Continuous benchmark results for the CLI                           |
| [📈 Benchmark Dashboard (PS)](/dev/bench/ps/index.html)                  | Continuous benchmark results for the PowerShell modules            |
| [📈 Startup Benchmark Dashboard (CLI)](/dev/bench/startup-cli/index.html) | Continuous startup benchmark results for the CLI                   |
| [📈 Startup Benchmark Dashboard (PS)](/dev/bench/startup-ps/index.html)   | Continuous startup benchmark results for PowerShell                |

