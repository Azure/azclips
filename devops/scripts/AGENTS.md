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
| Path                    | Purpose                                                                                                                                                                                                            |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ci/`                   | PR/CI checks (`check_pull_request.py`, `check_pull_request_changes.py`, `scan_recordings.py`). Recording scans flag unredacted signed ARM polling-URL query values (`c`, `s`, `h`) on any polling path (`/operationresults/`, `/operationstatuses/`, Storage's `/asyncoperations/`), mirroring the path-agnostic `arm-operation-signature` sanitiser in `tests/Tests.Recording/RecordingSanitizers.cs`. |
| `compliance/`           | Official CG NOTICE validation/comparison and stable-PR refresh (`Update-CgNotice.ps1`) and Credential Scanner suppression configuration (`CredScanSuppressions.json`, consumed by `CredScan@3` in `.azure-pipelines/templates/dist/stages/component-governance.yml`; each suppression needs a `_justification`). |
| `packaging/common/`     | Shared build steps (`build_common.sh`, `stage_agent.*`, `inject_builtin_extensions.*`, `create_component_archives.sh`, `rename_extension_packages.sh`).                                                            |
| `packaging/config/`     | Package profiles (`full_package.config`, `lean_package.config`) and `artifact_naming.config`.                                                                                                                      |
| `packaging/linux/`      | Linux tarball build + test, including offline installation of each runtime-compatible pipeline archive.                                                                                                            |
| `packaging/macos/`      | macOS tarball build/test and signing entitlements (`azclips-runtime.entitlements.plist`).                                                                                                                          |
| `packaging/windows/`    | Windows zip build + test.                                                                                                                                                                                          |
| `packaging/powershell/` | PowerShell module packaging/validation. The bundled installer infers versions from the package matching the requested module name; signed archive validation executes that installer and imports a child module at its packaged version. |
| `packaging/signing/`    | ESRP input prep, repack, and verification of signed outputs, including collision-resistant macOS bundle ZIP mapping.                                                                                               |
| `publishing/`           | Release publishing helpers (`Update-ReleaseChangelog.ps1`, `Update-ChangedExtensionVersions.ps1`).                                                                                                                 |

## Credential Scanner Suppressions

`compliance/CredScanSuppressions.json` records file paths, scanner-provided SARIF
`fingerprints.HashCode` values, and justifications for reviewed false positives.

## BinSkim Report Presence

`compliance/verify_binskim_results.ps1` requires that the supplied report
directory exists and contains at least one `.sarif` file, searched recursively.
Callers clear the job's BinSkim report directory before scanning so stale reports
cannot satisfy the check. It also parses each report and emits advisory warnings
for unparseable reports, `executionSuccessful=false`, tool notifications with
level `error` (for example load failures hidden by the ignore-errors BinSkim
flags). A clean BinSkim SARIF lists no analyzed files, so the script cannot
verify a file count. These checks never fail
the job.
Guardian's built-in tasks and PostAnalysis remain
responsible for analysis/result handling and enforcement.
The script reports a missing report directory or missing `.sarif` file as an Azure
DevOps warning (`##vso[task.logissue type=warning]`) and exits successfully, so it
never fails the build and needs no `System.PullRequest.IsFork` condition: when
Guardian skips a build, the check only warns.

`compliance/get_binskim_cli_targets.ps1` generates the CLI task's target glob for the
extracted unsigned Windows zip (`-ScanRoot`),
retaining the existing MSAL and Copilot exclusions and adding the reviewed exact
filenames in `compliance/binskim-runtime-exclusions.json`. These additional exclusions
apply only directly under the archive root; nested copies and other dependencies
remain in scope. The optional `patterns` list covers versioned runtime filenames
(currently the `mscordaccore_<arch>_<arch>_*.dll` DAC files) so a runtime upgrade
does not re-trigger ERR997; a pattern may only wildcard the trailing version
segment of an underscore-delimited `.dll` name. The configuration rejects paths and
other wildcards to prevent accidental blanket exclusions.
file-exclusion approach, but the larger .NET runtime list is azclips-specific.
Excluded binaries receive no BinSkim checks, not merely a missing-PDB exemption.
PowerShell's shipping-payload scan does not consume the CLI runtime list.

The report checker has no PDB-specific policy or report interpretation.
Review runtime exclusions when
dependencies change; do not expand them to `Microsoft.*`, `System.*`, or entire
runtime directories.

Run the regression tests with Python and PowerShell 7 available:
`python -m unittest discover -s devops/scripts/compliance -p test_verify_binskim_results.py -v`.

PowerShell validation's `run_ps_validation_phase.ps1` dispatches the Install and
Uninstall module phases. Install imports every packaged module with
`-RequiredVersion` and fails when the loaded version differs, the exported cmdlets
differ from manifest `CmdletsToExport`, or a cmdlet lacks a `Get-Help` example.
The validation bootstraps install standalone PowerShell archives from the official
GitHub releases, verify the published SHA256, and use the bundled .NET runtime.
Do not reintroduce a separate SDK/runtime installer or framework-dependent tool.
Release lines select the latest stable patch in that line; keep matrix runtime
channels as compatibility/report metadata. Repository restores still use CFS via
`NuGet.config`, which does not govern the standalone archive source.
The lifecycle fixtures resolve symbolic-link ancestors of the OS temporary
directory before creating scratch files, including macOS's `/var` alias.
Artifact capture still rejects symbolic links and reparse points.
`invoke_ps_validation_step.ps1`
publishes successful bootstrap/installation prerequisites so later phases
can continue after failures without clearing their recorded outcomes.
The shared `module_definitions.ps1` reader normalizes known bare source names and
manifest-path dependencies to package identities for lifecycle validation;
already-prefixed and unknown names are preserved.
Remove owned intermediate dotfiles with `-Force`; PowerShell treats them as hidden
on Unix even when the current user created them.
The matrix generator reads `packaging/powershell/ps_validation_matrix.json`
directly with jq; no Python environment or YAML parser is needed. PowerShell
consumers can use the built-in `ConvertFrom-Json`. Keep versions as JSON strings
and flags as booleans.
The matrix generator accepts `latest` only for native Windows OS versions, not
for container, PowerShell or .NET versions. Keep hosted-agent aliases out of
numeric version comparisons. Matrix self-tests remain available locally;
Azure DevOps retains generation and module validation without that self-test step.
The repository matrix pins Windows 2022 and 2025; standalone fixtures cover
`windows-latest` alias support without adding that moving image to production.
The generator preserves `DistributionPool` and also emits routing fields:
`DistributionPoolName` is the configured `pool-*` name with an empty
`DistributionVmImage`, or `Azure Pipelines` with the hosted image label.
Apply this routing after global and per-distribution overrides.

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
- `inject_builtin_extensions.ps1` / `inject_builtin_extensions.sh` — also copies each bundled
  extension's `*.commands.json` build-time sidecar (see `src/Extensions/Targets/ExtensionSidecar.targets`)
  alongside its DLLs, so metadata-first discovery has it in packaged artifacts too.

When you change behavior in one, **update its sibling** so Windows and Unix stay in sync.

## Config-Driven Packaging

- `full_package.config` / `lean_package.config` are include-only extension lists (one name per line; `*` = all). Selected via `PACKAGE_PROFILE=full|lean`.
- `artifact_naming.config` centralizes artifact name patterns — change names here, not scattered across scripts.

## Docker Publish Retry Behavior
- `packaging/linux/docker/publish_docker_images.sh` follows classic retry behavior: existing immutable version tags are retained, their digests are recorded, and downstream verification continues without candidate-image uploads.
- CLI tags follow the classic `azurelinux3.0` spelling, while PowerShell tags use classic `azurelinux-3.0`. Full CLI images omit a `full` qualifier: `VERSION-azurelinux3.0-{amd64,arm64}` are combined in the default `VERSION-azurelinux3.0` manifest.  Aliases such as `latest` are not promoted.
- `packaging/linux/docker/pull_published_docker_images.sh` confirms each multi-architecture manifest maps to the recorded architecture-specific digest, pulls that digest on the authenticated AMD64 release agent, and saves it as a per-image pipeline artifact. Native verification agents load those archives through `verify_published_docker_image.sh`, avoiding Azure CLI and ACR credential requirements on ARM64.
- `packaging/linux/docker/verify_mcr_docker_image.sh` strips the `public/` or `unlisted/` prefix from the ACR repository, checks that the anonymous MCR version tag exists with one retry after a 2-minute gap (`MCR_POLL_ATTEMPTS` default 2, `MCR_POLL_INTERVAL_SECONDS` default 120; the classic release makes a single attempt), then pulls it and runs `verify_published_docker_image.sh` smoke tests, which check the version; digests are not compared. The pipeline provides the post-publish delay.

## Editing Rules for Agents

- Preserve each file's existing header style: keep `set -euo pipefail` in bash, and don't add or strip a shebang just to normalize (most `.ps1` here intentionally have none).
- Contributor PRs may rename `.csproj` files only when their direct release-owned version properties are unchanged. CI uses a 1% similarity threshold and requires project additions and deletions to match one-to-one as renames, rejecting surplus paths so version validation cannot be bypassed through Git's rename classification.
- Preserve encoding, line endings, and final-newline state when publishing scripts make version-only file updates.
- `publishing/Update-ChangedExtensionVersions.ps1` supports dependency hashtables on their own lines or inline in `RequiredModules = @(@{ ... })`. Dependency version updates preserve surrounding layout and ignore commented declarations.
- New-extension detection uses a successful `git ls-tree` lookup for an absent base-version project rather than leaving a failed `git show` exit code for `PowerShell@2` to propagate. Git lookup/read failures remain fatal. The new-extension regression runs through the task's exit-code wrapper, not only in-process file assertions.
- `compliance/Update-CgNotice.ps1` requires a nonempty official generated file and committed root `NOTICE`. Comparison ignores CRLF/LF, trailing horizontal whitespace, and final-newline differences. Only stable-release PR preparation passes `-UpdateRootNotice`; changed content is written as UTF-8 without BOM, with LF and no trailing whitespace. Other builds only warn on differences and never replace the packaged NOTICE. Regression coverage is in `tests/Release.Tests/Update-CgNotice.Tests.ps1`.
- `ci/test_stable_release_tag.py` runs the stable trigger's actual tagging Bash against temporary local Git repositories. It verifies that a version bump with root `NOTICE` creates the expected tag, while NOTICE-only changes and unrelated paths do not; GitHub pushes are redirected to the local fixture. The GitHub Actions `release-script-tests` job runs this suite alongside `test_check_pull_request_changes.py`.
- Preserve env-var override points and script-relative path resolution.
- Keep RID/output-root names aligned with `.azure-pipelines/templates/dist/variables/packaging.yml`.
- Verify third-party archives against registry-provided checksums before extracting or executing them.
- Validate SDK-bundled Copilot runtimes with their server contract (`--server --stdio`); they do not expose the user-facing CLI's `--version` command.
- Keep active Windows process redirection files outside published artifact directories, wait for asynchronous stream handling to finish, and dispose process handles before copying diagnostics into artifacts.
- Run CLI package lifecycle validators only on matching native operating systems and architectures; layout-only success paths are not supported.
- Validate structured CLI results with `--query` and compare stdout rather than parsing presentation-oriented output; keep diagnostics on stderr separate from asserted values.
- Failure assertions match the text error contract: `ERROR: [Code] message` on stderr plus the process exit code, usually captured with `2>&1`. Do not parse a JSON error envelope; `--output` applies only to successful stdout.
- Preserve installer and uninstaller safety coverage for default paths, unsafe roots, unknown nonempty directories, and forced removal boundaries.
- If a script is called from a pipeline job, verify the corresponding `templates/dist/jobs/*.yml` still passes the expected env vars/args after your change.
- Update the parity sibling (`.ps1`/`.sh`) when editing one side.
- Validate locally where feasible (`bash -n script.sh`, `pwsh -NoProfile -File script.ps1 -WhatIf` where supported, `python3 -m py_compile`).
- PowerShell packaging requires MAML generated by `build/build.ps1 -GenerateHelpArtifacts`. The build installs pinned PlatyPS through PSResourceGet and stops on dependency or help-generation failures; do not bypass missing help files. Regression coverage is in `tests/Release.Tests/Build-HelpArtifacts.Tests.ps1`.
- Do not add secrets. Keep packaging build outputs under `devops/out/`; preserve explicit output/staging parameters for compliance, signing, and publishing scripts.
