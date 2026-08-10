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
| Upgrade Azure PS `v<version>` | Explicit placeholder; no upgrade is performed |
| Validate Azure PS `v<version>` | Explicit placeholder; no additional validation is performed |
| Uninstall Azure PS `v<version>` | `test_ps_uninstall.ps1` |

Installation already verifies version-pinned imports, exported cmdlets, and external
help examples. The Validate placeholder does not replace those assertions.

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
- Optional registry discovery adds the newest unconfigured distribution or
  PowerShell release as an unsupported canary. Canary failures report
  `SucceededWithIssues`, skip later phases, and do not block final artifact
  publication. Failures of supported combinations still block publication.
- Registry discovery failures produce warnings and retain configured coverage.
  Malformed configuration or generation errors fail instead of emitting a partial
  matrix.
- A version entry's `testInAlpha: true` opts it into limited runs. Discovered
  canaries also opt in. `--limit-testing` filters on this flag **before** Azure
  Pipelines expands matrix jobs; it fails clearly if no entries are selected.
- The job parameter `limit_testing` and shared stage parameter `limit_ps_testing`
  are booleans, defaulting to `false` for full coverage. The alpha pipeline opts
  into limited coverage with `limit_ps_testing: true`; other callers retain the
  full matrix by default. Set the alpha caller to `false` to run every combination.

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

# Alpha-selected combinations, including canaries when discovery is enabled.
bash devops/scripts/packaging/powershell/generate_ps_validation_matrix.sh --limit-testing
```

To add a Linux distribution, add its YAML entry and
`linux/<id>_install_script.sh`. Keep module tests out of the bootstrap. Set
`testInAlpha` on versions that should run in the limited alpha matrix. Native
Windows/macOS entries use their platform bootstrap and must not enable container
registry discovery. Use `--pool` for container host overrides and `--pool-for
<id>=<image>` for a particular native platform.

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

foreach ($phase in 'Install', 'Upgrade', 'Validate', 'Uninstall')
{
    & $pwshPath -NoProfile -File $dispatcher -Phase $phase `
        -SourceRoot $sourceRoot -RepositoryRoot $gallery -PwshPath $pwshPath
    if ($LASTEXITCODE -ne 0) { throw "$phase failed with exit code $LASTEXITCODE." }
}
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
pwsh -NoProfile -File tests\Scripts\PS\PSValidationPipelineFixtures.ps1
```

The matrix suite uses registry fixtures. Lifecycle fixtures isolate mocked module
operations in child processes. Pipeline fixtures exercise the real host runner
with fake native commands and evaluate the publication gates from the YAML,
including unsupported failures. Both the GitHub PR workflow and the Azure DevOps
matrix-generation job run these suites before distribution validation.
