# PowerShell package validation

These scripts exercise the packaged Azure PowerShell modules from a local gallery.
The module tests are shared across distributions; only installation of PowerShell
and its OS prerequisites is distribution-specific.

## Pipeline phases

Each matrix job shows one task for each phase:

| Task | Implementation |
| --- | --- |
| Install PowerShell `<version>` | The distribution's bootstrap script |
| Install Azure PS `v<version>` | `test_ps_installation.ps1` |
| Uninstall Azure PS `v<version>` | `test_ps_uninstall.ps1` |

Installation installs every packaged module from the local gallery, imports each
one with `-RequiredVersion`, and fails if the loaded module's version differs from
the packaged version. Each module must export exactly its manifest
`CmdletsToExport`, each cmdlet must be discoverable with `Get-Command`, and
`Get-Help` must return at least one non-empty example from the packaged external
help. Install requires a successful Bootstrap. Cancellation skips
new validation work, but Uninstall still runs when Install completed successfully.
These rules also apply to unsupported canaries; successful later phases and cleanup
never replace earlier validation failures.

The **Build (PS) Common** job runs the same Install and Uninstall phases against
its freshly packed modules. It publishes the packages first; the dependent
**Validate (PS)** stage owns installation validation across the distribution matrix.

`invoke_ps_validation_step.ps1` orchestrates these tasks using the hosted agent's
preinstalled PowerShell 7. Bootstrap installs the requested PowerShell separately;
all module phases run through that executable and `run_ps_validation_phase.ps1`.
Custom agent images must provide PowerShell 7 for orchestration as well.

Linux jobs keep a single Docker container alive across phases. Each `docker exec`
prepends the installed .NET directory to the **container's** PATH. The always-run
container cleanup task removes it even after a failed phase. Native Windows and
macOS jobs reuse a toolchain under the agent's temporary directory and restore both
`DOTNET_ROOT` and PATH for every module phase.

## Matrix and support policy

`ps_validation_matrix.json` declares schema version 3, the PowerShell release lines
and matching .NET runtime channels, distribution versions, and execution modes.
The .NET SDK comes from the repository's `global.json`. PowerShell release lines
such as `7.6` install the latest available `7.6.*` tool package; the installed version
is printed during bootstrap. `AzVersion` comes from `src/PS/Az/Az.psd1`.
The Az modules target .NET 10, so PowerShell releases older than 7.6 are unsupported.

Keep version identifiers as JSON strings (for example, `"7.6"`, `"10.0"`, and `"2025"`),
and flags as JSON booleans (`true`/`false`). Native Windows entries also accept
`"latest"`, which expands `"poolTemplate": "windows-{version}"` to `windows-latest`.
This configured alias follows the same PowerShell support policy as numeric
Windows versions; it is not a discovered canary. All other distribution,
PowerShell and .NET runtime versions must remain numeric strings.
The generator reads JSON directly with its existing jq rules; no Python, YAML
parser, or additional library is needed. PowerShell consumers can use the built-in
`ConvertFrom-Json`. Keep explanatory notes here rather than adding comments to
the JSON file. Explicit `--config` paths must also contain a single JSON object.
The matrix output remains JSON for Azure Pipelines.

- Configured combinations are blocking unless their PowerShell entry explicitly
  sets `"supported": false`.
- In full runs, optional registry discovery adds the newest unconfigured distribution or
  PowerShell release as an unsupported canary. Canary failures report
  `SucceededWithIssues` and do not block independent phases or final artifact
  publication. Failed prerequisites still skip dependent phases. Failures of
  supported combinations still block publication.
- Registry discovery failures produce warnings and retain configured coverage.
  Malformed configuration or generation errors fail instead of emitting a partial
  matrix.
- A configured version entry's `"testInAlpha": true` opts it into limited runs.
  `--limit-testing` disables both distribution and PowerShell release discovery,
  so it makes no registry requests and adds no discovered canaries. It filters on
  this flag **before** Azure Pipelines expands matrix jobs and fails clearly if
  no configured entries are selected.
- The single `ValidatePS` stage takes two booleans from the shared
  distribution template, both defaulting to `false`. `limit_ps_testing` passes
  `--limit-testing`, and `skip_ps_version_check` passes `--skip-version-check`,
  overriding all discovery flags in the JSON without editing the configuration.
  Each entry pipeline sets both explicitly:

  | Pipeline | `limit_ps_testing` | `skip_ps_version_check` | Coverage |
  | --- | --- | --- | --- |
  | `azclips-alpha-build.yml` | `true` | `true` | `testInAlpha` entries, no discovery |
  | `azclips-beta-build.yml` | `false` | `false` | Full matrix plus discovered canaries |
  | `azclips-stable-release.yml` | `false` | `true` | Full configured matrix, no discovery |
  | `azclips-stable-release-test.yml` | `false` | `true` | Same as stable |
- Publication requires an acceptable whole-stage `ValidatePS` result.

From the repository root, on a host with Bash, jq, curl, and GNU sort, run the
commands below. No parser dependency installation is required in either CI
workflow. Azure DevOps runs the actual matrix generator without a separate matrix
self-test step; the self-test remains available locally and runs in GitHub Actions.

```bash
# Reproducible selection of configured combinations, without registry discovery.
bash devops/scripts/packaging/powershell/generate_ps_validation_matrix.sh --skip-version-check

# Only configured alpha-selected combinations, with no release discovery.
bash devops/scripts/packaging/powershell/generate_ps_validation_matrix.sh --limit-testing
```

To add a Linux distribution, add its JSON entry and
`linux/<id>_install_script.sh`. Keep module tests out of the bootstrap. Set
`testInAlpha` on versions that should run in the limited alpha matrix. Native
Windows/macOS entries use their platform bootstrap and must not enable container
registry discovery. Use `--pool` for container host overrides and `--pool-for
<id>=<image>` for a particular native platform.

Pool values beginning with `pool-` are named Azure DevOps pools, such as
`pool-ubuntu-2204`; other values retain hosted-image semantics, such as
`ubuntu-latest`, `windows-2025`, and `macOS-15-arm64`. The preparation jobs and
generated matrix both honor this distinction. Matrix output retains
`DistributionPool` for diagnostics and separately emits `DistributionPoolName`
and `DistributionVmImage` for routing. Named pools have an empty image field;
hosted images use the `Azure Pipelines` pool.

## Validation artifacts

Every matrix leg publishes its own `Validate_PS_Sim_<ValidationKey>` pipeline
artifact, for example `Validate_PS_Sim_ubuntu_26_04_ps7_6`. `ValidationKey` is the
generator's unique matrix key, including the unsupported suffix for canaries.
There is no consolidation job, and these diagnostics are separate from the input
`PSModules` artifact and the final release distribution.

| Path | Contents |
| --- | --- |
| `installed-modules/<package>/<version>/` | Snapshot of the exact module versions attempted by this run, captured before uninstall or failed-install rollback. |
| `inventories/` | Target-module state before/after installation and uninstall, with failure/rollback state when applicable. |
| `environment.json` | Actual tested PowerShell/.NET versions, OS/architecture, and module paths, captured by the module scripts. |
| `matrix.json` | Requested versions, Az version, matrix identity, canary policy, and separately labelled orchestration-host details. |
| `logs/` | Bootstrap, Install, and Uninstall transcripts, including native stdout/stderr; unexecuted phases get an explicit explanation. |
| `results.json` | Schema version 3: per-distribution phase timestamps, native exit codes, outcomes, and overall result. All three phases are required for success. |
| `capture-errors.json` | Collection errors, when any occur; the original lifecycle failure remains primary. |

Reports live under the agent's temporary directory, not the checkout or package
gallery. Linux mounts that directory writable at `/reports`, while `/src` and
`/artifacts` remain read-only. Files therefore survive container deletion without
a separate export step. Native agents write to the same host-side report layout.
Only targeted module files are snapshotted; whole home directories, toolchain
installations, credentials, and repository configuration are not copied.
Snapshots reject links/reparse points and ambiguous or mismatched module
directories. Capture-only installation failures fail explicitly and still roll
back attempted packages. Failed rollback is reflected in the final inventory;
the diagnostic collector never deletes unregistered module directories itself.

Finalization and publication both use `always()`. A failed bootstrap may leave
only logs and matrix/results metadata; `runtimeEnvironmentCaptured` records
whether the target PowerShell environment was captured. Partial failures retain
the evidence available before cleanup. A phase interrupted before recording its
result is marked `Interrupted`, not passed.

`-ReportRoot` is optional for local callers. The host runner
`invoke_ps_validation_step.ps1` records phase logs/results, and
`complete_ps_validation_artifacts.ps1` finalizes skipped/interrupted phases.
Passing `-ReportRoot` directly to the module scripts or phase dispatcher captures
module snapshots, inventories, and runtime metadata as applicable,
but not host phase transcripts/results. Omitting it runs the same assertions
without retaining reports.

## Bootstrap contracts

Run Linux package-manager bootstraps inside disposable containers, not on a
developer workstation. Use a fresh work directory: tool installation is not an
upgrade operation.

```text
sh linux/<id>_install_script.sh <source-root> <powershell-version> <dotnet-runtime> [work-root]
sh macos/macos_install_script.sh <source-root> <powershell-version> <dotnet-runtime> [work-root]
```

Linux defaults to `/opt`; macOS defaults to
`${AGENT_TEMPDIRECTORY:-${TMPDIR:-/tmp}}/azclips-ps-validation`. Each work directory
contains `dotnet`, `pwsh`, and the downloaded .NET installer.

Windows exposes the corresponding named parameters:

```powershell
.\devops\scripts\packaging\powershell\windows\windows_install_script.ps1 `
    -SourceRoot (Get-Location).Path `
    -PowerShellVersion '7.6' `
    -DotnetRuntimeVersion '10.0' `
    -WorkRoot 'C:\temp\ps-validation-toolchain'
```

The bootstrap scripts install dependencies only. They do not install or uninstall
Az modules, and their environment changes do not persist into another process.

## Running module phases locally

Build and package the modules first. The local build gallery is normally
`artifacts/Release/PSGallery/local-repo`. A downloaded pipeline artifact instead
contains `artifacts/PSGallery/local-repo` beneath its download directory.
Pass the gallery's actual path as `-RepositoryRoot`; do not pass its parent.

Use a disposable test account/environment. Installation refuses to overwrite an
already available target module version or an existing repository registration
with the same name. Other installed versions are allowed. Do not bypass those
guards by deleting normal user modules.

After a successful Windows bootstrap, this runs the shared phases:

```powershell
$sourceRoot = (Get-Location).Path
$workRoot = 'C:\temp\ps-validation-toolchain'
$pwshPath = Join-Path $workRoot 'pwsh\pwsh.exe'
$env:DOTNET_ROOT = Join-Path $workRoot 'dotnet'
$env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
$dispatcher = Join-Path $sourceRoot 'devops\scripts\packaging\powershell\run_ps_validation_phase.ps1'
$gallery = Join-Path $sourceRoot 'artifacts\Release\PSGallery\local-repo'

& $pwshPath -NoProfile -File $dispatcher -Phase Install `
    -SourceRoot $sourceRoot -RepositoryRoot $gallery -PwshPath $pwshPath
if ($LASTEXITCODE -ne 0) { throw "Install failed with exit code $LASTEXITCODE." }
& $pwshPath -NoProfile -File $dispatcher -Phase Uninstall `
    -SourceRoot $sourceRoot -RepositoryRoot $gallery -PwshPath $pwshPath
if ($LASTEXITCODE -ne 0) { throw "Uninstall failed with exit code $LASTEXITCODE." }
```

On Linux/macOS, use `<work-root>/pwsh/pwsh` and export
`DOTNET_ROOT=<work-root>/dotnet` and `PATH="$DOTNET_ROOT:$PATH"` before invoking the
same dispatcher with the same phase arguments.

Successful installation intentionally leaves modules installed for subsequent
phases. If installation or an import/version assertion fails, failure cleanup attempts
to remove this run's packages in reverse order, tolerates absent packages, and
continues after individual removal errors. The original failure remains a failure,
and cleanup errors are reported. Repository operations use an isolated temporary
working directory so they do not edit the checkout's `NuGet.config`.

The normal Uninstall phase is deliberately strict: all expected versions must
have been installed, removed, and become non-importable. It is not the partial
installation cleanup mechanism.

## Offline regression fixtures

No YAML parser, Az module installation, Docker daemon, Azure credentials, or
network access is needed for these fixtures.
Run them from the repository root:

```bash
bash devops/scripts/packaging/powershell/test_generate_ps_validation_matrix.sh
```

```powershell
pwsh -NoProfile -File tests\Scripts\PS\PSModuleLifecycleFixtures.ps1
pwsh -NoProfile -File tests\Scripts\PS\PSValidationPipelineFixtures.ps1
```

The matrix suite uses registry fixtures. Lifecycle fixtures isolate mocked module
operations in child processes, including a module that loads under the wrong version.
Pipeline fixtures exercise the real host runner
with fake native commands and evaluate prerequisite and publication gates from the
YAML, including multiple failures, unsupported warnings, and cancellation. Both
the GitHub PR workflow and the Azure DevOps
matrix-generation job run these suites before distribution validation.
