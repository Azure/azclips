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
    details: On failure the CLI hands off to an interactive TUI in agent-mode, running ChatAgent on the GitHub Copilot SDK with a bundled native daemon.
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
    details: System.Diagnostics activities with a privacy-preserving filter model and a detached background uploader to Azure Monitor.
---

## Quick Navigation

| Section | Description |
| --- | --- |
| [🚀 Principal-Level Guide](/onboarding-guide) | Dense architectural deep-dive for senior engineers |
| [📚 Zero to Hero](/zero-to-hero-guide) | Progressive learning path from foundations to contributing |
| [📖 Overview](/01-getting-started/overview) | Project architecture and key concepts |
| [⚙️ Environment Setup](/01-getting-started/environment-setup) | Development environment and tooling |
| [💻 CLI Usage](/01-getting-started/cli-usage) | Command-line interface guide |
| [🐚 PowerShell](/01-getting-started/powershell-usage) | PowerShell module usage |
| [🔬 Core Engine](/02-deep-dive/core-engine-orchestration) | Composition root, engine, and session-based fallback |
| [🌐 HTTP Pipeline](/02-deep-dive/http-pipeline) | Delegating-handler chain and runtime |
| [🔐 Authentication](/02-deep-dive/authentication) | MSAL auth and token management |
| [🧩 Extensions](/02-deep-dive/extension-system) | Dynamic extension loading system |
| [⚙️ Configuration & Profiles](/02-deep-dive/configuration-profiles) | `az config` and named connection profiles |
| [🤖 AI Agent](/02-deep-dive/ai-agent) | GitHub Copilot SDK agent and tools |
| [🖥️ Terminal UI](/02-deep-dive/terminal-ui) | Interactive TUI and agent-mode |
| [📊 Telemetry](/02-deep-dive/telemetry) | Activity capture and observability |
| [🧪 Testing](/02-deep-dive/testing-benchmarking) | Test framework and benchmarks |
| [🏗️ Build & CI/CD](/02-deep-dive/build-cicd) | Build system and deployment |
| [📈 Benchmark Dashboard (CLI)](/dev/bench/cli/index.html) | Continuous benchmark results for the CLI |
| [📈 Benchmark Dashboard (PS)](/dev/bench/ps/index.html) | Continuous benchmark results for the PowerShell modules |
