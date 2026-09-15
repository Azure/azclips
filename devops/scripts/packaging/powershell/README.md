# PowerShell package validation

These scripts exercise the packaged Azure PowerShell modules from a local gallery.
The module tests are shared across distributions; only installation of PowerShell
and its OS prerequisites is distribution-specific.

## Pipeline phases

**Validate Azure PS Smoke-Test Coverage** runs once in the
**Generate PS Validation Matrix** job, before the distribution matrix starts.
It invokes `test_ps_smoke.ps1 -CoverageOnly` using the hosted PowerShell without
installing target modules or executing their commands. Missing cases report
`SucceededWithIssues`; complete coverage reports `Succeeded`. Warnings do not
block matrix expansion. Malformed configuration still fails.

Each matrix job shows one task for each phase:

| Task | Implementation |
| --- | --- |
| Install PowerShell `<version>` | The distribution's bootstrap script |
| Validate Azure PS Dependencies | `test_ps_dependencies.ps1` and `test_ps_dependency_runtime.ps1` |
| Install Azure PS `v<version>` | `test_ps_installation.ps1` |
| Upgrade Azure PS `v<version>` | Explicit placeholder; no upgrade is performed |
| Validate Azure PS `v<version>` | `test_ps_smoke.ps1`, configured by `ps_smoke_tests.psd1` |
| Uninstall Azure PS `v<version>` | `test_ps_uninstall.ps1` |

Installation verifies version-pinned imports, exported cmdlets, and external help
examples. Validate additionally imports every module and executes its configured
smoke commands. Dependencies and Install require a successful Bootstrap, not a
successful preceding validation phase. Upgrade and Validate require a successful
Install and still run after dependency failures. Cancellation skips new validation
work, but Uninstall still runs when Install completed successfully. These rules also
apply to unsupported canaries; successful later phases and cleanup never replace
earlier validation failures.

The **Build (PS) Common** job retains its module-output and package installation/
removal checks, but does not invoke the Dependencies or smoke Validate phase. It publishes the
packages first; the dependent **Validate (PS)** stage owns smoke validation across
the distribution matrix. Missing smoke coverage therefore cannot block publication
of the input packages needed by that stage.

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

`ps_validation_matrix.yaml` declares schema version 3, the PowerShell release lines
and matching .NET runtime channels, distribution versions, and execution modes.
The .NET SDK comes from the repository's `global.json`. PowerShell release lines
such as `7.6` install the latest available `7.6.*` tool package; the installed version
is printed during bootstrap. `AzVersion` comes from `src/PS/Az/Az.psd1`.

Keep version identifiers quoted (for example, `'7.6'`, `'10.0'`, and `'2025'`),
and flags as YAML booleans (`true`/`false`). Use ordinary `#` comments rather than
comment properties. The generator safely converts YAML to temporary JSON for its
existing jq rules; its matrix output remains JSON for Azure Pipelines.
Duplicate keys, multiple documents, and unsafe YAML tags are rejected. Explicit
`--config` paths also accept JSON for compatibility.

- Configured combinations are blocking unless their PowerShell entry explicitly
  sets `supported: false`.
- In full runs, optional registry discovery adds the newest unconfigured distribution or
  PowerShell release as an unsupported canary. Canary failures report
  `SucceededWithIssues` and do not block independent phases or final artifact
  publication. Failed prerequisites still skip dependent phases. Failures of
  supported combinations still block publication.
- Registry discovery failures produce warnings and retain configured coverage.
  Malformed configuration or generation errors fail instead of emitting a partial
  matrix.
- A configured version entry's `testInAlpha: true` opts it into limited runs.
  `--limit-testing` disables both distribution and PowerShell release discovery,
  so it makes no registry requests and adds no discovered canaries. It filters on
  this flag **before** Azure Pipelines expands matrix jobs and fails clearly if
  no configured entries are selected.
- The job parameter `limit_testing` and shared stage parameter `limit_ps_testing`
  are booleans, defaulting to `false` for full coverage. The alpha pipeline
  temporarily sets `limit_ps_testing: false` to exercise the entire matrix in this
  PR, including normal discovery. Restore it to `true` to use configured limited
  coverage after that validation.

From the repository root, on a host with Bash, jq, curl, GNU sort, and Python 3,
install the YAML dependency in an isolated environment:

```bash
python3 -m venv /tmp/ps-validation-tools
. /tmp/ps-validation-tools/bin/activate
python3 -m pip install -r devops/scripts/packaging/powershell/requirements.txt
```

Both CI workflows provision this environment before running matrix generation or
its regression suite. Local Debian/Ubuntu hosts may also need the
`python3-venv` package to create the environment. Then run:

```bash
# Reproducible selection of configured combinations, without registry discovery.
bash devops/scripts/packaging/powershell/generate_ps_validation_matrix.sh --skip-version-check

# Only configured alpha-selected combinations, with no release discovery.
bash devops/scripts/packaging/powershell/generate_ps_validation_matrix.sh --limit-testing
```

To add a Linux distribution, add its YAML entry and
`linux/<id>_install_script.sh`. Keep module tests out of the bootstrap. Set
`testInAlpha` on versions that should run in the limited alpha matrix. Native
Windows/macOS entries use their platform bootstrap and must not enable container
registry discovery. Use `--pool` for container host overrides and `--pool-for
<id>=<image>` for a particular native platform.

## Validation artifacts

The pre-matrix job publishes `Validate_PS_Smoke_Coverage` once. Its
`smoke-coverage.json` contains configured case counts for each module,
missing-coverage warnings, and the coverage step's outcome, with no import or
command execution results.

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
| `logs/` | Bootstrap, Dependencies, Install, Upgrade, Validate, and Uninstall transcripts, including native stdout/stderr; unexecuted phases get an explicit explanation. |
| `results.json` | Schema version 2: per-distribution phase timestamps, native exit codes, outcomes, and the overall result. Only Upgrade remains an explicitly skipped placeholder; Dependencies and Validate are required for success. Coverage is reported separately in the pre-matrix artifact. |
| `dependency-tests.json` | Package metadata, automatic dependency resolution, missing/incompatible dependency and loaded-version-conflict results, including child exit codes, diagnostics and saved/imported inventories. |
| `runtime-dependencies.json` | Actual runtime details and isolated missing-framework/unsupported-PowerShell rejection results. |
| `smoke-tests.json` | Per-module exact versions, import outcomes, named command results, durations, output counts, expected-error observations, failures, and missing-coverage warnings. Created when Validate runs. |
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
module snapshots, inventories, runtime metadata, and smoke results as applicable,
but not host phase transcripts/results. Omitting it runs the same assertions
without retaining reports.

## Dependency validation

Dependencies runs after Bootstrap and before Install in every selected matrix leg.
It does not preload or install Az modules into the user's module directories.
The normal Install/Validate/Uninstall phases still provide installation lifecycle
and smoke coverage; this additional suite tests the dependency contract itself.
The dispatcher runs both package and runtime suites even if the package suite
fails, returning the first failing exit code only after both finish. Metadata
failures can prevent package scenarios that require a valid dependency contract,
but do not prevent runtime validation or the normal installation lifecycle.

`test_ps_dependencies.ps1` compares every package's NuGet dependency metadata with
the source manifests, then requests each module (including the Az rollup) alone.
The shared module reader maps known source names such as `Accounts` and manifest
paths such as `../Accounts/Accounts.psd1` to `Az.Accounts`, while already-prefixed
package names remain unchanged. Unknown dependencies and mismatched versions
still fail validation.
It uses PowerShellGet `Save-Module` against a uniquely named local repository and
saves into a fresh directory for each case. Each import runs in another process,
with only that directory and the tested PowerShell's built-in modules on
`PSModulePath`. It verifies the expected dependency versions and command exports
without manually preloading dependencies.

For every direct dependency, private gallery copies omit the package or replace
it with version `0.0.0`, below the required version. A case passes only when the
resolver reports the intended missing dependency; unrelated failures and unexpected
success fail validation. For non-rollup consumers, a separate case preloads a dummy
conflicting dependency and requires the packaged module's version-conflict diagnostic.
The dummy supplies only the loaded-version precondition; the import guard under test
is the one in the real consumer package.

The package runner bounds each child to 180 seconds by default (`-TimeoutSeconds`).
It records expected errors and partial inventories, continues other cases after
failures, and removes its own temporary repository registrations and scratch files.
The published gallery and existing user repositories/modules remain unchanged.
Reports must be outside the source modules and gallery. Reuse a fresh report
directory for each run.

The runtime suite checks the target's actual runtime, launches it against an
isolated .NET host without the required framework, and verifies rejection by both
the installation helper and packaged manifest under PowerShell 7.5. Its older
PowerShell/.NET toolchain is private and temporary; provisioning failures are
failures, not successful rejection tests. No runtime is removed from the primary
toolchain. These checks cover the framework dependency, not exhaustive removal of
every distribution-specific native library.

Run just the package dependency suite locally against an existing gallery:

```powershell
pwsh -NoProfile -File .\devops\scripts\packaging\powershell\test_ps_dependencies.ps1 `
    -RepositoryRoot .\artifacts\Release\PSGallery\local-repo `
    -SourcePsRoot .\src\PS -PwshPath $pwshPath -ReportRoot 'C:\temp\ps-dependencies'
```

The full `Dependencies` dispatcher phase additionally requires the bootstrap's
`DOTNET_ROOT` and downloads the older tool/runtime for negative probes. The package
fixtures below use real local synthetic NuGet packages, while runtime fixtures are
offline harness tests. Neither establishes public PSGallery or PSResourceGet
delivery; those remain separate validation paths.

## Configuring module smoke tests

`ps_smoke_tests.psd1` is a declarative PowerShell data file, so the tested runtime
needs no YAML parser, Pester, or other added dependency. Like Azure PowerShell's
`tools/Test/SmokeTest/RmCoreSmokeTests.ps1`, it defines named command cases, records
each outcome and duration, continues after failures, and fails the run at the end.
Unlike that authenticated suite, these defaults are **offline packaging probes**:
they neither sign in nor create, modify, or delete Azure resources.

Each module runs in a fresh `-NoProfile -NonInteractive` process of the installed
target PowerShell, with a default 120-second **per-module** timeout. Its Az
dependencies are imported at the exact source-manifest versions before importing
the module. Source requirements are retained even when packaging moves dependency
declarations to NuGet metadata and clears the installed manifest's requirements.
Commands are taken from the pinned module's exports, so another
module or a newer installed version cannot satisfy a test accidentally.
Timeouts and import failures fail the module while other module cases continue.
Command failures also allow remaining cases and modules to run. The final error
lists every failed module, with individual case details in the log and report.
Partial results survive a timeout: completed cases retain their results, the
active case is `Interrupted`, and later cases remain `NotRun`.

To cover a new module, add an entry to `Modules`:

```powershell
@{
    Names = @('Az.Example')
    Tests = @(
        @{
            Name = 'local-schema'
            Command = 'Get-AzExampleSchema'
            Mode = 'ReadOnly'
            Parameters = @{ KeysOnly = $true }
            MinimumOutputCount = 1
        }
    )
}
```

Use the module's real exported command and full parameter names. Add further
named hashtables to `Tests` for more cases; no runner or pipeline edits are needed.
Every discovered source module should be configured with at least one case.
Unconfigured modules and empty test lists emit actionable PowerShell and Azure
Pipelines warnings once in **Validate Azure PS Smoke-Test Coverage**, marking that
pre-matrix step `SucceededWithIssues` while returning exit code zero. Each matrix
leg's **Validate Azure PS** then runs with `-SkipCoverageWarnings` so it does not
repeat coverage annotations.
Exact-version imports still run, and configured commands for other modules execute normally.
Warnings are recorded at the report and module levels in `smoke-tests.json`;
a successful import with no command cases is not command coverage. Import failures
still fail validation, even for modules without command cases.
Duplicate names, unknown properties, or invalid assertions fail the pre-matrix
coverage step before smoke commands execute; the build-time fixture suite and
matrix generator themselves do not enforce production smoke coverage.
Entries for modules absent from a particular branch are
allowed: the shared Accounts/Login entry supports the authentication-module rename,
and Identity/MsGraph entries also cover the current main-branch package set.

- `ReadOnly` permits `Get-` and `Test-` commands. Choose commands that operate
  locally without authentication; a read verb alone does not guarantee this.
- `WhatIf` requires `SupportsShouldProcess`; the runner supplies `-WhatIf` and
  disables confirmation. Verify that the actual implementation calls
  `ShouldProcess` **before** any mutation or network operation. Metadata alone is
  insufficient. The configured removal/disconnect probes have this guard.
- `MinimumOutputCount` optionally requires a nonnegative number of output objects.
  Config's probe requests schema keys only, not user configuration values.
- `ExpectedError = @{ Id = '...'; Message = '...' }` requires both the exact error
  ID (before PowerShell's command suffix) and exact exception message. An unrelated
  exception or an unexpectedly successful invocation fails. `{MissingProfile}` is
  replaced in string parameters and the expected message with a fresh unique name.
  Resources uses this to exercise its explicit missing-profile guard, which fails
  before the HTTP pipeline is created.
- The `Az` rollup has no commands of its own. Its explicit
  `CommandModule = 'Az.Config'` invokes a pinned, declared dependency after importing
  `Az`; delegation to unrelated modules is rejected.

Do not place credentials in this configuration. Reports count command output
rather than serialize the returned objects; command errors and host streams
remain diagnostic output in the Validate log. These probes do not establish
successful authenticated Azure API behavior. Keep live-resource tests separate.

For already installed target packages, the runner also works directly:

```powershell
.\devops\scripts\packaging\powershell\test_ps_smoke.ps1 `
    -SourcePsRoot .\src\PS -PwshPath $pwshPath -ReportRoot 'C:\temp\ps-smoke-reports'
```

Use `-ConfigPath` for an alternative data file and `-TimeoutSeconds` to override
the per-module bound. The same runner and configuration apply to every OS/arch.
Use `-CoverageOnly` to run just coverage validation without requiring installed
target modules or a working `-PwshPath`. This writes `smoke-coverage.json` instead
of `smoke-tests.json` when `-ReportRoot` is supplied.

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
$validationExitCode = 1
$uninstallExitCode = 1
try
{
    & $pwshPath -NoProfile -File $dispatcher -Phase Validate `
        -SourceRoot $sourceRoot -RepositoryRoot $gallery -PwshPath $pwshPath
    $validationExitCode = $LASTEXITCODE
}
finally
{
    & $pwshPath -NoProfile -File $dispatcher -Phase Uninstall `
        -SourceRoot $sourceRoot -RepositoryRoot $gallery -PwshPath $pwshPath
    $uninstallExitCode = $LASTEXITCODE
}
if ($validationExitCode -ne 0) { throw "Validate failed with exit code $validationExitCode; uninstall exited $uninstallExitCode." }
if ($uninstallExitCode -ne 0) { throw "Uninstall failed with exit code $uninstallExitCode." }
```

On Linux/macOS, use `<work-root>/pwsh/pwsh` and export
`DOTNET_ROOT=<work-root>/dotnet` and `PATH="$DOTNET_ROOT:$PATH"` before invoking the
same dispatcher with the same phase arguments.

Successful installation intentionally leaves modules installed for subsequent
phases. If installation or an import/help assertion fails, failure cleanup attempts
to remove this run's packages in reverse order, tolerates absent packages, and
continues after individual removal errors. The original failure remains a failure,
and cleanup errors are reported. Repository operations use an isolated temporary
working directory so they do not edit the checkout's `NuGet.config`.

The normal Uninstall phase is deliberately strict: all expected versions must
have been installed, removed, and become non-importable. It is not the partial
installation cleanup mechanism.

## Offline regression fixtures

After installing the Python YAML dependency above, no Az module installation,
Docker daemon, Azure credentials, or network access is needed for these fixtures.
Run them from the repository root:

```bash
bash devops/scripts/packaging/powershell/test_generate_ps_validation_matrix.sh
```

```powershell
pwsh -NoProfile -File tests\Scripts\PS\PSModuleLifecycleFixtures.ps1
pwsh -NoProfile -File tests\Scripts\PS\PSSmokeFixtures.ps1
pwsh -NoProfile -File tests\Scripts\PS\PSDependencyFixtures.ps1
pwsh -NoProfile -File tests\Scripts\PS\PSDependencyRuntimeFixtures.ps1
pwsh -NoProfile -File tests\Scripts\PS\PSValidationPipelineFixtures.ps1
```

Smoke fixtures use their own synthetic configurations, not `ps_smoke_tests.psd1`.
They exercise runner behaviour (including the expected failure for missing coverage)
without enforcing the current production module/test catalogue. Removing a production
case therefore fails the actual Validate task after installation, while the build
fixtures remain independent.

The matrix suite uses registry fixtures. Lifecycle fixtures isolate mocked module
operations in child processes. Smoke fixtures import actual dummy script modules
in isolated processes, exercising version/dependency pinning, coverage, command
provenance, assertions, timeout handling, and both authentication-module layouts.
Pipeline fixtures exercise the real host runner
with fake native commands and evaluate prerequisite and publication gates from the
YAML, including multiple failures, unsupported warnings, and cancellation. Both
the GitHub PR workflow and the Azure DevOps
matrix-generation job run these suites before distribution validation.
