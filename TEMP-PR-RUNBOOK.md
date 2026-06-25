# TEMP PR Runbook: Local Regression (CLI + PowerShell)

@TODO: delete before merge

## 1. Scope

This runbook validates the runtime-discovery migration in this PR, including:

1. Discovery-first extension reads for extension commands.
2. Preference-only registry persistence behavior.
3. Discovery-first runtime help routing with hint-table fallback only.
4. No regressions for PowerShell module load/output surfaces.

## 2. Preconditions

1. .NET SDK from global.json is installed.
2. Run from repo root.
3. You have a clean or intentionally dirty working tree.

## 3. Build

```powershell
dotnet build cirrus.slnx -c Debug
```

Expected:

1. Build succeeds.
2. CLI is available as `az` in your dev environment.
3. PowerShell manifests are copied under ./bin.

## 4. Fast Automated Regression

Run test buckets most relevant to this PR.

```powershell
dotnet test tests/Core.Extensions.Tests/Core.Extensions.Tests.csproj
dotnet test tests/Cli.Tests/Cli.Tests.csproj --filter "FullyQualifiedName~ExtensionListByVerbTests|FullyQualifiedName~ExtensionShowTests|FullyQualifiedName~ExtensionPinTests|FullyQualifiedName~RegistryLookupTests|FullyQualifiedName~ExtensionDispatcherTests|FullyQualifiedName~RuntimeHelpRendererTests|FullyQualifiedName~ProgramFallbackTests"
dotnet test tests/Cmdlets.Tests/Cmdlets.Tests.csproj
```

Expected:

1. Core.Extensions.Tests passes.
2. Targeted Cli.Tests pass (some ProgramFallback tests may be skipped by design).
3. Cmdlets.Tests passes.

## 5. CLI Smoke: Discovery-Backed Extension API

```powershell
az extension list
az extension list --by-verb
az extension list --output json
```

Expected:

1. Installed extensions appear even if not fully represented in registry.json.
2. JSON output is valid and includes discovered/live extension shape.

Show command:

```powershell
az extension show --name Resources
az extension show --name Microsoft.Azure.Cirrus.Extensions.Resources
```

Expected:

1. Both short and full package forms resolve.
2. Returned data reflects discovered extension metadata.

Pin/unpin behavior (preference overlay):

```powershell
az extension pin --name Resources
az extension list --output json
az extension unpin --name Resources
az extension list --output json
```

Expected:

1. Pin and unpin succeed.
2. Pinned state toggles in output.
3. Command works even if entry must be seeded from discovery.

Optional remove/re-add cycle (if you want destructive validation):

```powershell
az extension remove --name Resources
az extension list
az extension add --name Resources
az extension list
```

Expected:

1. Remove deletes the installed payload and preference entry.
2. Re-add restores extension visibility via discovery.

## 6. CLI Scenario: Explicit Add/Remove Attempt

Use a disposable extension target for this step. If `Resources` is your only active extension, prefer a local test package to avoid disrupting your normal dev flow.

Install attempt:

```powershell
az extension add --name Resources
az extension list --output json
```

Expected:

1. Add succeeds (or reports already-installed in a structured way).
2. Extension is visible in discovery-backed list output.
3. Help for contributed verbs resolves after add.

Remove attempt:

```powershell
az extension remove --name Resources
az extension list --output json
```

Expected:

1. Remove succeeds only when payload deletion succeeds.
2. Removed extension no longer appears in discovery-backed list output.
3. Help for removed verbs falls back to install guidance (CNF/hint path).

Re-add recovery:

```powershell
az extension add --name Resources
az extension list --output json
```

Expected:

1. Re-add restores the extension in discovery output.
2. Verb help/dispatch works again without manual registry edits.

## 7. CLI Smoke: Help Routing (5.4)

Root help:

```powershell
az --help
```

Expected:

1. Root groups are rendered from discovered extensions.

Installed extension help:

```powershell
az group --help
az deployment group --help
```

Expected:

1. Group and intermediate-group help render successfully.
2. No registry-only dependency for ownership lookup.

Uninstalled extension hint fallback:

```powershell
az vm --help
```

Expected:

1. If Compute is not installed, returns structured install guidance (ExtensionNotInstalled path).
2. This path is hint-table fallback, not primary ownership resolution.

## 8. Registry Behavior Spot Check (Preference-Only Writes)

Run any command that writes registry preferences (pin/unpin is easiest), then inspect registry.

```powershell
$registryPath = Join-Path $HOME ".cirrus/extensions/registry.json"
Get-Content $registryPath
```

Expected for each entry written by current code path:

1. Includes preference fields (Pinned, Version/Source/InstalledAt as applicable).
2. Does not persist installed-state fields as authoritative data.
3. Contributions/namespaces/verb are not required for runtime routing.

## 9. PowerShell Surface Regression

Import packaged modules:

```powershell
Import-Module ./src/PS/config/Config.psd1 -Force
Import-Module ./src/PS/resources/Resources.psd1 -Force
Get-Module Cirrus*
Get-Command -Module Cirrus.Config,Cirrus.Resources -ErrorAction SilentlyContinue
```

Expected:

1. Modules import without errors.
2. Exported cmdlets/functions are discoverable.

Basic cmdlet help availability (non-auth smoke):

```powershell
Get-Help New-ResourceGroup -ErrorAction SilentlyContinue
Get-Help Get-ResourceGroup -ErrorAction SilentlyContinue
Get-Help Invoke-CirrusRestMethod -ErrorAction SilentlyContinue
```

Expected:

1. Help resolves for shipped cmdlets.

Optional authenticated smoke (only if your environment is configured):

```powershell
# Example only; run with valid Azure context.
# Get-ResourceGroup -SubscriptionId <subscriptionId>
```

Expected:

1. Cmdlets continue to execute through normal runtime path.
