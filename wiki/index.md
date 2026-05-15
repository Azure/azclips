---
layout: home
title: "Cirrus Wiki"
description: "Technical documentation for the Cirrus CLI framework"

hero:
  name: "Cirrus"
  text: "Technical Wiki"
  tagline: "A .NET 9.0 extension-based CLI framework with AI-powered natural language capabilities for Azure resource management"
  image:
    src: /logo.svg
    alt: Cirrus
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
    details: Dynamic extension loading from NuGet packages and local DLLs with assembly isolation and version management.
  - icon: 🤖
    title: AI-Powered Fallback
    details: Natural language command processing via Azure OpenAI with automatic fallback when structured parsing fails.
  - icon: 🔧
    title: Dual Interface
    details: Both CLI (azv2) and PowerShell modules for Azure resource management with shared core libraries.
  - icon: 🔐
    title: MSAL Authentication
    details: Microsoft Authentication Library integration with persistent token caching and device code flow support.
  - icon: 📊
    title: OpenTelemetry
    details: Full observability with Azure Monitor, Zipkin (dev), and local file export for distributed tracing.
  - icon: 🏗️
    title: CI/CD Pipeline
    details: GitHub Actions with MSIX packaging, matrix extension builds, and PowerShell module validation.
---

## Quick Navigation

| Section | Description |
|---------|-------------|
| [🚀 Principal-Level Guide](/onboarding-guide) | Dense architectural deep-dive for senior engineers |
| [📚 Zero to Hero](/zero-to-hero-guide) | Progressive learning path from foundations to contributing |
| [📖 Overview](/01-getting-started/overview) | Project architecture and key concepts |
| [⚙️ Environment Setup](/01-getting-started/environment-setup) | Development environment and tooling |
| [💻 CLI Usage](/01-getting-started/cli-usage) | Command-line interface guide |
| [🐚 PowerShell](/01-getting-started/powershell-usage) | PowerShell module usage |
| [🔬 Core Engine](/02-deep-dive/core-engine-orchestration) | Orchestrator and engine hierarchy |
| [🧩 Extensions](/02-deep-dive/extension-system) | Dynamic extension loading system |
| [🔐 Authentication](/02-deep-dive/authentication) | MSAL auth and token management |
| [🤖 AI Agent](/02-deep-dive/ai-agent) | Azure OpenAI integration |
| [🌐 HTTP Pipeline](/02-deep-dive/http-pipeline) | Request pipeline and middleware |
| [📊 Telemetry](/02-deep-dive/telemetry) | OpenTelemetry observability |
| [🧪 Testing](/02-deep-dive/testing-benchmarking) | Test framework and benchmarks |
| [🏗️ Build & CI/CD](/02-deep-dive/build-cicd) | Build system and deployment |
| [📈 Benchmark Dashboard (CLI)](/dev/bench/cli/index.html) | Continuous benchmark results for the CLI |
| [📈 Benchmark Dashboard (PS)](/dev/bench/ps/index.html) | Continuous benchmark results for the PowerShell modules |

