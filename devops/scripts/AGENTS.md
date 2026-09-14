# DevOps Scripts — Agent Guide

Scoped guidance for the build, packaging, signing, compliance, and publishing
scripts under `devops/scripts/`. These are invoked by the Azure DevOps pipelines
(see `.azure-pipelines/AGENTS.md`) and are also runnable locally. Read this before
editing any script here. When you add or change a script, parity pair, config, or
output/staging path, update this guide in the same PR so it stays accurate.

## Essentials
- This is the **executable layer** the pipelines shell out to. Keep reusable build/packaging logic here; pipeline-specific orchestration, conditions, and short validation steps may remain in YAML.
- Three languages. Directly-executed scripts carry a shebang; scripts invoked explicitly by an interpreter (e.g. `pwsh -File …`) may omit it:
  - Bash: `#!/usr/bin/env bash` then `set -euo pipefail` (all `.sh` here follow this).
  - PowerShell: `#!/usr/bin/env pwsh` only when the file is run directly. Most `.ps1` here are invoked as `pwsh -File` and have no shebang — match the surrounding file's existing style rather than adding one.
  - Python: `#!/usr/bin/env python3`.
- **Parity pairs** under `packaging/common/**` (`stage_agent.*`, `inject_builtin_extensions.*`) provide equivalent behavior across platforms; keep both sides in sync. Individual scripts may still be platform-specific — e.g. `build_common.sh` and `create_component_archives.sh` run on Ubuntu — and per-platform scripts under `linux/`, `macos/`, `windows/` are intentionally OS-specific.
- Avoid repo-relative CWD assumptions — resolve paths from the script location (see below).
- No secrets, no hardcoded credentials. They are supplied by the pipeline environment (variable groups, Key Vault, approved service connections, and built-in pipeline authentication).

## Directory Layout
| Path | Purpose |
| --- | --- |
| `ci/` | PR/CI checks (`check_pull_request.py`, `check_pull_request_changes.py`, `scan_recordings.py`). |
| `compliance/` | NOTICE generation (`generate_notice.ps1`, `NOTICE.template`, `notice-overrides.json`). |
| `packaging/common/` | Shared build steps (`build_common.sh`, `stage_agent.*`, `inject_builtin_extensions.*`, `create_component_archives.sh`, `rename_extension_packages.sh`). |
| `packaging/config/` | Package profiles (`full_package.config`, `lean_package.config`) and `artifact_naming.config`. |
| `packaging/linux/` | Linux tarball build + test. |
| `packaging/macos/` | macOS tarball build/test and signing entitlements (`azclips-runtime.entitlements.plist`). |
| `packaging/windows/` | Windows zip build + test. |
| `packaging/powershell/` | PowerShell module packaging/validation. |
| `packaging/signing/` | ESRP input prep, repack, and verification of signed outputs. |
| `publishing/` | Release publishing helpers (`Update-ReleaseChangelog.ps1`, `Update-ChangedExtensionVersions.ps1`). |

## Path Resolution Convention (Bash)
Scripts resolve their own location and derive roots from it, allowing env-var overrides —
the **pattern**, not fixed depths:
```bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# `..` is the packaging/common/* depth; deeper scripts adjust it
# (e.g. packaging/linux/tarball/* uses `../..`) so the roots still resolve.
PACKAGING_ROOT="${PACKAGING_ROOT:-$(cd "$SCRIPT_DIR/.." && pwd)}"
REPO_ROOT="${REPO_ROOT:-$(cd "$PACKAGING_ROOT/../../.." && pwd)}"
```
- The relative depth **varies by script location** — don't copy a snippet verbatim.
  For example `packaging/common/*` uses `SCRIPT_DIR/..`, while `packaging/linux/tarball/*`
  and `packaging/macos/tarball/*` use `SCRIPT_DIR/../..`. Verify your derived roots resolve
  correctly for the file's actual directory.
- Prefer `${VAR:-default}` so the pipeline can override roots but local runs still work.
- Output goes under `devops/out/{common,linux,windows,macos}` (matches `PackagingOutRoot` in the pipeline).
- Match the surrounding scripts' pattern; do not assume the caller's working directory.

## Cross-Platform Parity Pairs
Some steps ship as matched `.ps1` + `.sh` implementations:
- `stage_agent.ps1` / `stage_agent.sh`
- `inject_builtin_extensions.ps1` / `inject_builtin_extensions.sh`

When you change behavior in one, **update its sibling** so Windows and Unix stay in sync.

## Config-Driven Packaging
- `full_package.config` / `lean_package.config` are include-only extension lists (one name per line; `*` = all). Selected via `PACKAGE_PROFILE=full|lean`.
- `artifact_naming.config` centralizes artifact name patterns — change names here, not scattered across scripts.

## Editing Rules for Agents
- Preserve each file's existing header style: keep `set -euo pipefail` in bash, and don't add or strip a shebang just to normalize (most `.ps1` here intentionally have none).
- Preserve encoding, line endings, and final-newline state when publishing scripts make version-only file updates.
- Preserve env-var override points and script-relative path resolution.
- Keep RID/output-root names aligned with `.azure-pipelines/templates/dist/variables/packaging.yml`.
- If a script is called from a pipeline job, verify the corresponding `templates/dist/jobs/*.yml` still passes the expected env vars/args after your change.
- Update the parity sibling (`.ps1`/`.sh`) when editing one side.
- Validate locally where feasible (`bash -n script.sh`, `pwsh -NoProfile -File script.ps1 -WhatIf` where supported, `python3 -m py_compile`).
- Do not add secrets. Keep packaging build outputs under `devops/out/`; preserve explicit output/staging parameters for compliance, signing, and publishing scripts.
