---
title: Get started with Next-Generation Azure CLI (Public preview)
description: Install and sign in to Next-Generation Azure CLI, verify your active profile, and learn about supported environments, coexistence, and feedback during public preview.
---

# Get started with Next-Generation Azure CLI (Public preview)

Next-Generation Azure CLI & Azure PowerShell provides command-line and PowerShell experiences
for managing Azure, with shared authentication, profiles, and configuration. This article helps
you start using the CLI public preview. The preview doesn't yet cover every classic command
or workflow.

## Install and verify the preview

Follow the [installation overview](./install-overview.md) to choose a package for your operating
system. The CLI is self-contained and doesn't require the .NET SDK.

The documented download flow uses an authenticated GitHub CLI (`gh`) session and requires access
to the repository and release assets. GitHub sign-in is separate from Azure sign-in.

The installer doesn't change `PATH`. Before using the examples below, follow the installation
overview to put the preview on `PATH` for the current session, or invoke its `az` executable by
full path. Confirm which executable is active:

```bash
# Bash / Zsh
command -v az
az version
az --help
```

```powershell
# PowerShell
Get-Command az -CommandType Application | Select-Object Source
az version
az --help
```

At the default install location, the preview executable is
`~/.local/share/azcli/az` on Linux and macOS, or
`%LOCALAPPDATA%\Programs\azcli\az.exe` on Windows.

## Authentication

For an initial interactive sign-in, specify an Azure subscription that you can access.
Replace the placeholders with your tenant and subscription IDs:

```bash
az login --tenant-id <tenant-id> --subscription-id <subscription-id>
az profile show --active
az group list
```

`az group list` is a read-only check; an empty result is valid if the subscription has no
resource groups. On a lean installation, the command requires the Resources extension. See
[install CLI extensions](./install-cli-extensions.md), or use the full package.

If a browser isn't available, use device code sign-in:

```bash
az login --tenant-id <tenant-id> --subscription-id <subscription-id> --use-device-code
```

The preview implements the following authentication methods:

| Method | CLI options |
| --- | --- |
| Interactive user | `az login` with `--subscription-id`; optionally specify `--tenant-id`. |
| Device code | Add `--use-device-code` to user sign-in. |
| Managed identity | `--identity`; select a user-assigned identity with `--client-id`, `--object-id`, or `--resource-id`. Requires an environment that exposes the identity. |
| Service principal with a client secret | `--client-id`, `--client-secret`, `--tenant-id`, and `--subscription-id`. |
| Service principal with workload identity federation (OIDC) | `--service-principal`, `--federated-token`, `--client-id`, `--tenant-id`, and `--subscription-id`. Requires a configured federated credential. |

Use `az login --help` for the options in your installed build. Don't assume every classic
authentication option is available. Avoid putting secrets in scripts, shell history, or feedback.

### Shared profiles and configuration

Sign-in saves and activates a profile. Without `--out-profile`, sign-in uses the reserved
`default` profile. To keep multiple contexts, see [manage sign-in profiles](./profile-base-login.md).

The two preview surfaces share profiles and authentication state when run as the same operating
system user with the same state directory. This isn't shared sign-in with classic Azure CLI or
classic Azure PowerShell; sign in to the preview separately.

By default, configuration (`config.json`), profiles (`profiles.json`), the active profile pointer,
and authentication cache files use `~/.azclips` (`$HOME\.azclips` on Windows). The environment
variable `AZCLIPS_HOME` overrides this root. See [configuration management](./config.md) for
settings and precedence.

## Coexistence with classic tools

The preview CLI installs separately from classic Azure CLI. Adding the preview directory to
`PATH` makes `az` resolve to the preview instead of the classic executable. Use a session-only
`PATH` change or a full executable path while evaluating it; existing scripts use whichever
`az` their environment resolves.

To switch between the classic Azure CLI and the preview build within the same session, see
[Switch between the classic Azure CLI and the preview build](./switch-cli-versions.md).

## Supported environments

CLI release archives are provided for Windows, Linux (glibc and musl), and macOS on x64 and
Arm64. See the [supported platform table](./install-overview.md#supported-platforms) to select
the matching archive.

Use Bash or Zsh on Linux/macOS, or PowerShell 7 or later on Windows for the documented installer
commands. The separate [PowerShell preview modules](../../azure-powershell/docs-conceptual/getting-started.md)
require PowerShell 7.6 or later; that module requirement doesn't apply to the CLI executable.

## Automation support

Service principal and managed identity sign-in can be used for non-interactive scenarios when
their credentials or identity environment are available. Confirm that your installed preview
supports every command, parameter, and output shape your script needs before using it in automation.
Federated tokens are short-lived and must be refreshed by the issuing identity provider.

## Logging issues and providing feedback

Your feedback helps improve the public preview.

If you encounter an issue:

1. Verify that you're using a supported operating system and shell.
1. Reproduce the issue, if possible.
1. Collect any relevant error messages or command output.
1. Search [existing issues][01], then use the appropriate bug report, feature request, or feedback
   template if you have repository access.

When reporting an issue, include:

- Operating system
- Shell (PowerShell, Bash, or Zsh)
- Next-Generation Azure CLI version (`az version`) and the active executable path
- Classic Azure CLI or Azure PowerShell version, if relevant to a coexistence problem
- Steps to reproduce the problem
- Expected behavior
- Actual behavior
- Any relevant error messages

Remove secrets, tokens, usernames, subscription IDs, tenant IDs, and other sensitive information
from commands, output, logs, and screenshots before submitting. If you can't access the repository
or issue templates, the GitHub feedback route isn't available to your account.

## Known preview limitations

During the public preview, you should expect some limitations:

- Not all Azure CLI and Azure PowerShell scenarios are available.
- Optional [AI-based error recovery](./ai-based-error-recovery.md) can produce incomplete or
  inaccurate suggestions. Review suggestions before running commands.
- Feature availability may change between preview releases.
- In Zsh, the `-?` and `--?` help aliases fail with `zsh: no matches found` unless you quote them
  (`'-?'`, `'--?'`), because Zsh treats an unquoted `?` as a wildcard. Use `-h` or `--help` instead.

<!-- link references -->

[01]: https://github.com/Azure/azclips/issues
