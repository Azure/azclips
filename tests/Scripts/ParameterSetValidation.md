# CLI and PowerShell parameter-set validation

Build the solution, then run either script from the repository root in PowerShell 7.6:

```powershell
dotnet build azclips.slnx
pwsh -NoProfile -File .\tests\Scripts\CLI\ParameterSetValidation.ps1
pwsh -NoProfile -File .\tests\Scripts\PS\ParameterSetValidation.ps1
```

Use `-OutputDirectory <directory>` to override `artifacts\parameter-set-validation`.
Each script writes a Markdown report and JSON results, including the complete
command inventory, parameter-level required flags, set membership, expected and
observed paths, exit codes and captured errors.

## Scope and implementation

Every product `ExtensionIndex`, including the Agent extension, opts its CLI table
into `AddDefaultParameterSets` after injecting shared parameters such as `Profile`.
Commands with no sets get one `Default` set. Its members use canonical
`ParameterMetadata.Name` keys and copy `IsRequired` exactly. Optional-only and
parameterless commands get optional-only and empty sets, respectively: adding a
set does not invent new mandatory arguments. Existing explicit sets are preserved,
including Storage and MsGraph alternatives. PowerShell metadata is not changed.

## Reading the reports

- `ParameterSetRejected`: the real shared binder rejected missing required members.
- `BindingAccepted`: the metadata-only probe accepted empty input with a synthetic
  profile. No processor was executed.
- `NoParameterSetValidation`: unchanged PowerShell metadata has no shared sets.
  Parameter-level `IsRequired` alone does not enable shared set validation.
- `PowerShellMandatoryBinding` / `PowerShellAmbiguousBinding`: native PowerShell
  binding stopped the invocation before shared argument binding.
- `BindingAcceptedExecutionBlocked`: real CLI/cmdlet routing and shared binding
  accepted the invocation. The test hook stopped it before processor execution.
  **This is not a missing-parameter rejection or a successful Azure operation.**
- `Unexpected`: fail the script and investigate the captured output. A nonzero
  exit alone never passes a probe.

Metadata probes cover every dispatch entry. Public probes execute every distinct
CLI command and its available PowerShell cmdlets without required parameters;
multiple internal PowerShell dispatch entries can map to one public cmdlet.
PowerShell remains an unchanged comparison surface, not a feature modification.

## Safety and limitations

The scripts create a private temporary `AZCLIPS_HOME`, seed a synthetic subscription
profile, and disable telemetry and automatic agent recovery. They start a fresh
process with `DOTNET_STARTUP_HOOKS` pointing to the existing `Tests.Common` build
output. The hook installs a test-only wrapper around the real
`IArgumentParsingService`: failures propagate normally, and accepted inputs raise
an explicit execution-blocked marker before any processor runs. The product has
no test environment checks or validation bypasses.

This boundary is necessary for commands such as `login`, `survey`, local config
and extension management, where empty input may otherwise start an interactive
flow or mutate local state. The original process environment is not modified and
the temporary home is deleted after the worker finishes.

The probes do not test processor-level validation, authentication, HTTP requests
or successful resource operations. Use existing unit/scenario playback tests for
those workflows. No real credentials or Azure resources are needed.
