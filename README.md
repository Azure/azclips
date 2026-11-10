# Azure CLI & Azure PowerShell — a unified command surface for Azure command line tools

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Build and Test](https://github.com/Azure/azclips/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/Azure/azclips/actions/workflows/build-and-test.yml)
[![Published](https://img.shields.io/badge/status-published-green.svg)](#)
<!-- NEEDS ENG REVIEW: confirm the Build and Test badge/workflow before publishing (private-repo badge only renders for members with access). -->

> [!IMPORTANT]
> **🔒 Limited Preview — This content is confidential and should only be discussed under NDA.**
> This program is a closed, invitation-only preview shared under NDA with a small group. Access is provisioned through GitHub — you're here because your account was added to this **private** repository. **Do not share** builds, screenshots, benchmarks, command output, or any details publicly or with anyone outside the preview. The program runs through July to Sep 2026.

## What is Azure CLI & Azure PowerShell?

This new generation of command line tools provide a lightweight, efficient and intelligent set of commands to manage Azure resources.
Depending on preferences, scripting environment or other requirements, you can use the native command or its PowerShell equivalent to obtain identical outcomes.

The architecture used for these commands allows execution in secure, constrained environments and provides faster execution or a smaller disk footprint.

## Preview capabilities (<sub>[What's new](docs/azure-cli/docs-conceptual/whats-new.md)</sub>)

- **Profile-based login** — sign in and manage which subscription and context your commands run against.
- **Single sign-on (SSO)** — one `az login` is shared between Azure PowerShell and native `az` commands in the same session; no re-authentication when switching.
- **Faster tab completion** — press <kbd>Tab</kbd> to complete command groups, subcommands, and parameters, resolved against the installed CLI.
- **AI-powered error recovery** — when a command fails, an integrated AI model explains what went wrong and suggests a guided fix. See [AI-based error recovery](./docs/azure-cli/docs-conceptual/ai-based-error-recovery.md).

Plus the basics: install, `az --help`, `az --version` and `az config`.

## Getting started

1. **Install** — see the [Azure CLI install overview](docs/azure-cli/docs-conceptual/install-overview.md) and the [Azure PowerShell modules install guide](docs/azure-powershell/docs-conceptual/install-powershell-modules.md) for the single-command install (Windows, Linux, macOS). Packages ship via [GitHub Releases](https://github.com/Azure/azclips/releases).
2. **Verify** — run `az --version`.
3. **Sign in** — run `az login`.
4. **Explore supported commands**
   - CLI: `config`, `deployment`, `disk`, `extension`, `group`, `login`, `logout`, `profile`, `rest`, and `vm`
   - PowerShell: **Accounts**, **Resources**, **Compute**, and **Config**
   - Additional commands will continue to be added in weekly iterations.

> `az` installs side-by-side with the classic `azure-cli` and does **not** modify your `PATH` by default. See the [install overview](docs/azure-cli/docs-conceptual/install-overview.md) for PATH and offline options.

## Try these scenarios

**Profile-based login** — sign in, save a named profile, and switch between them:

```bash
az login --subscription-id <id> --out-profile prod  # save a named profile
az profile list                                     # list profiles
az profile set prod                                 # switch the active profile
```

**Azure PowerShell ↔ native SSO** — sign in once, then use both surfaces in the same session, no re-auth:

```bash
az login --subscription-id <id>   # sign in once
az group list       # native az command — already authenticated
```

```powershell
Get-AzResourceGroup   # Azure PowerShell — same session, no re-login
```

**Tab completion** — type a partial command and press <kbd>Tab</kbd>:

```bash
az pro<Tab>         # completes to: az profile
```

Enable it for bash, zsh, or PowerShell — see [tab completion](docs/azure-cli/docs-conceptual/install-cli-completions.md).

**AI-powered error recovery** — when a command fails, the AI explains the cause and suggests a fix:

```bash
az profile set does-not-exist
# → what went wrong, a suggested plan / next actions, and values to review.
```

## Documentation

Start here, then follow the pointers into `./docs`.

**Install**
- [Install overview](docs/azure-cli/docs-conceptual/install-overview.md)
- [Manual / offline install](docs/azure-cli/docs-conceptual/install-cli-manual.md)
- [Add-on CLI extensions](docs/azure-cli/docs-conceptual/install-cli-extensions.md)
- [Tab completion](docs/azure-cli/docs-conceptual/install-cli-completions.md)
- [Azure PowerShell modules](docs/azure-powershell/docs-conceptual/install-powershell-modules.md)

**Azure CLI**
- [Getting started](docs/azure-cli/docs-conceptual/getting-started.md)
- [AI-based error recovery](docs/azure-cli/docs-conceptual/ai-based-error-recovery.md)
- [Troubleshooting](docs/azure-cli/docs-conceptual/troubleshooting.md)
- [What's new](docs/azure-cli/docs-conceptual/whats-new.md)

**Azure PowerShell**
- [Overview](docs/azure-powershell/docs-conceptual/README.md) — getting-started guide in progress

> Documentation is being consolidated during the preview, so paths may change.

## Known limitations

This is an early public preview — expect rough edges and change between builds.

- The preview focuses on **interactive user scenarios**. Non-interactive auth methods — Resource Owner Password Credentials (ROPC), service principals, and managed identities — aren't supported yet.
- Supported shells are **PowerShell 7+, bash, and zsh**. Windows PowerShell 5.1 and the PowerShell ISE aren't supported.
- Azure CLI & Azure PowerShell installs side-by-side and doesn't replace your existing Azure CLI or Azure PowerShell — you can roll back at any time.
- Commands, behavior, and packaging may change without notice during the preview.

For the current list, see [Troubleshooting](https://github.com/Azure/azclips/blob/main/docs/azure-cli/docs-conceptual/troubleshooting.md).

## Feedback

Please share early and often.

- **[GitHub Issues](https://github.com/Azure/azclips/issues)** — primary channel for bugs, install problems, doc gaps, command issues, and feature requests (templates provided).
- **[GitHub Discussions](https://github.com/Azure/azclips/discussions)** — questions and ideas.
- **[Pulse survey](https://forms.cloud.microsoft/r/n0H7LqgM5e)** — short (~5 min) Microsoft Forms survey.
- **Moderated sessions** — group and 1:1; details will be shared over email.

We review on a regular cadence. For quick collaboration with the Product Group, you will be added to a Microsoft Teams channel. Share your Teams ID on the invite email if you haven't been added yet.

> Never include secrets, tokens, customer data, tenant IDs, or subscription IDs in issues or discussions.

## Repository structure

```
azclips/
├── Agent/            # Agent host, chat/agent-mode entry, hooks, and tools
├── build/            # Build scripts (build.ps1, module staging, help gen)
├── devops/           # DevOps helper scripts
├── docs/             # Documentation (getting-started & reference), being consolidated
│   ├── azure-cli/    # Azure CLI conceptual & reference docs
│   ├── azure-powershell/ # Azure PowerShell conceptual & reference docs
│   ├── howto/        # Task-focused guides (build, config, help, ...)
│   └── spec/         # Technical specifications
├── install/          # Single-command install for all platforms/architectures
│   └── docs/         # Manual/offline install, extensions, completions, PS modules
├── openspec/         # OpenSpec change proposals and specs
├── src/              # Product source code
│   ├── CLI/          # CLI entry point (produces the 'az' binary)
│   ├── Core/         # Core framework libraries
│   ├── Extensions/   # Extension modules
│   └── PS/           # PowerShell modules and manifests
├── tests/            # xUnit test projects and fixtures
├── toolkit/          # TypeScript toolkit packages
├── tools/            # Repository tooling (help generation, ...)
├── TUI/              # Terminal UI app
├── wiki/             # Rendered documentation site (npm run preview)
├── azclips.slnx      # Main solution file
├── global.json       # Pinned .NET SDK version
├── AGENTS.md         # Repository guidelines
├── CHANGELOG.md      # Version history
├── CONTRIBUTING.md   # Contribution guidelines
└── LICENSE           # MIT license
```

## Governance

- **License** — [MIT](LICENSE). *(Open-source review in progress.)*
- **Support** — [SUPPORT.md](SUPPORT.md)
- **Code of Conduct** — [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- **Security Policy** — [SECURITY.md](SECURITY.md)
- **Privacy & telemetry** — the preview collects limited telemetry with your consent (command names, parameter *names* only, success/failure, and basic environment data — never parameter values, URLs, or personal content). Telemetry is on by default; opt out with `az config set --key telemetry.enabled --value false` (Azure PowerShell equivalent: `Set-AzConfig -Key telemetry.enabled -Value false`). See [Configuration Management](docs/howto/config.md#valid-configuration-keys).

### Data Collection

The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft's privacy statement. Our privacy statement is located at [https://go.microsoft.com/fwlink/?LinkID=824704](https://go.microsoft.com/fwlink/?LinkID=824704). You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.

### Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos is subject to those third-party policies.
