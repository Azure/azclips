---
title: Install the Azure CLI public preview
description: Learn how to install the Azure CLI public preview.
---

# Install the Azure CLI public preview

The Azure CLI public preview ships as **azcli** — the `az` command-line tool (self-contained; no .NET SDK needed). It ships in two flavours:

- **full** — built-in + add-on extensions bundled.
- **lean** — built-in extensions only.

> [!NOTE]
> For the Az PowerShell modules, see the [Azure PowerShell public preview install overview](../../azure-powershell/docs-conceptual/install-overview.md).

## Install azcli (one command)

The installer downloads the latest release from [GitHub Releases](https://github.com/Azure/azclips/releases).

- **Linux / macOS / WSL:** Bash, `curl`, `tar`, and standard system text utilities.
- **Windows:** PowerShell 7+.

**Linux / macOS / WSL:**

```bash
# Linux / macOS / WSL
curl -fsSL https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-install.sh | bash
```

**Windows (PowerShell 7+):**

```powershell
# Windows (PowerShell 7+)
irm https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-install.ps1 -ErrorAction Stop | iex
```

### Install the lean package

To install only the built-in extensions, use:

```bash
# Linux / macOS / WSL
curl -fsSL https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-install.sh | AZCLI_PACKAGE=lean bash
```

```powershell
# Windows (PowerShell 7+)
$env:AZCLI_PACKAGE='lean'; irm https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-install.ps1 -ErrorAction Stop | iex
```

The Bash assignment applies only to the installer process. The PowerShell variable persists for the current session; run `Remove-Item Env:AZCLI_PACKAGE` afterward to restore the default full-package selection.

## Run azcli

By default the installer does **not** modify your `PATH`, so azcli (`az`) installs in isolation and coexists with the classic `azure-cli` (`az`). It lands at `~/.local/share/azcli/az` (Windows: `%LOCALAPPDATA%\Programs\azcli\az.exe`).

- **Run by full path** (no setup):

  ```bash
  ~/.local/share/azcli/az --help
  ```

- **Add to `PATH` for this session** (recommended during public preview; does not persist or shadow classic `az` in new shells):

  ```bash
  export PATH="$HOME/.local/share/azcli:$PATH"     # Linux / macOS / WSL — current shell only
  ```

  ```powershell
  $env:Path = "$env:LOCALAPPDATA\Programs\azcli;$env:Path"   # Windows — current session only
  ```

- **Make it permanent** (optional) — add the same line to your shell profile (`~/.zshrc` / `~/.bashrc`, or your PowerShell `$PROFILE`). Note that putting the folder on `PATH` makes `az` resolve to azcli and **shadows** the classic `azure-cli`.

> [!TIP]
> Want to keep both installed and switch between the classic Azure CLI and the preview build in the same session? See [Switch between the classic Azure CLI and the preview build](./switch-cli-versions.md).

## Uninstall

```bash
# Linux / macOS / WSL
curl -fsSL https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-uninstall.sh | bash
```

```powershell
# Windows
irm https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-uninstall.ps1 -ErrorAction Stop | iex
```

Removes the install directory. Download and review the uninstall script first if preferred. To remove a folder that doesn't look like an azcli install, pass `--force` with `bash -s -- --force`, or download the PowerShell script and run `./azcli-uninstall.ps1 -Force`.

## Alternative: download and run

Use this optional workflow to inspect the script before running it or to pass installer parameters.

Download the script for your platform:

```bash
# Linux / macOS / WSL
curl -fsSL https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-install.sh -o azcli-install.sh
```

```powershell
# Windows (PowerShell 7+)
Invoke-WebRequest https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-install.ps1 -OutFile ./azcli-install.ps1 -ErrorAction Stop
```

After a successful download, review the script, then run:

```bash
# Linux / macOS / WSL
bash ./azcli-install.sh
```

```powershell
# Windows (PowerShell 7+)
./azcli-install.ps1
```

For the lean package, pass `--package lean` (Bash) or `-Package lean` (PowerShell).
The `AZCLI_PACKAGE` environment variable is also supported; the default is `full`.

## Download troubleshooting

The installer reports failed downloads and missing or ambiguous release assets. GitHub's API is rate-limited; if a release lookup returns HTTP 403 or 429, check GitHub's response and retry after the rate limit resets. You can also download the archive from [GitHub Releases](https://github.com/Azure/azclips/releases) and follow the [manual guide](./install-cli-manual.md).

Piped Bash execution may not report the downloader's exit status. If the bootstrap download fails, do not assume installation succeeded; use the [download-and-run workflow](#alternative-download-and-run) to check download success separately.

## Other install scenarios

| You want to… | Guide |
| --- | --- |
| Install azcli manually / offline (you already have the archive, or prefer not to use the installer) | [install-cli-manual.md](./install-cli-manual.md) |
| Add an add-on azcli extension (lean installs, or a specific version) | [install-cli-extensions.md](./install-cli-extensions.md) |
| Enable `az` tab completion (bash / zsh / PowerShell) | [install-cli-completions.md](./install-cli-completions.md) |
| Enable AI-based error recovery in Azure CLI | [ai-based-error-recovery.md](./ai-based-error-recovery.md) |

## What's in a release

| Category | Files | Platforms | Notes |
| --- | --- | --- | --- |
| CLI archives | `azcli-lean-…` and `azcli-…` | 8 platform variants | Windows x64/Arm64, Linux glibc x64/Arm64, Linux musl x64/Arm64, and macOS x64/Arm64. Lean = built-in extensions only; Full = built-in + add-on extensions bundled. Self-contained, no .NET SDK needed. |
| Extension roll-up | `azcli-extensions-<version>.tar.gz` | agnostic | Convenience bundle of all extension packages. |
| CLI extension packages | `azcli.extensions.*.nupkg` | agnostic | Install into an existing CLI with `az extension add`. |

## Supported platforms

| OS and C library | x64 / Intel | Arm64 / Apple Silicon |
| --- | --- | --- |
| Windows | `win-x64` | `win-arm64` |
| Linux with glibc (Ubuntu, Debian, Fedora, Azure Linux) | `linux-x64` | `linux-arm64` |
| Linux with musl (Alpine Linux) | `linux-musl-x64` | `linux-musl-arm64` |
| macOS | `macos-x86_64` | `macos-arm64` |

Throughout these guides, replace `<version>` with the version in your downloaded file name (for example `0.1.0`). Preview and beta builds may include a `-preview` suffix in the file name.
