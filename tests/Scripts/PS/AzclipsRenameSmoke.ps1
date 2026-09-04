# Smoke test for the extension rename to Azclips of the Resources extension under PowerShell.
#
# Validates that the renamed assembly Microsoft.Azure.Azclips.Extensions.Resources.dll loads
# in PowerShell and that its processor types are reachable by their new fully-qualified names.
# This is the PowerShell-side counterpart to tests/Scripts/CLI/AzclipsRenameSmoke.sh.
#
# It deliberately uses a direct reflection load (rather than Import-Module Resources.psd1) because
# the dev-layout module manifests chain through Accounts.psd1, whose relative RequiredAssemblies paths
# only resolve in the deployed bin/ layout. The reflection load exercises the renamed assembly the
# same way the module would, with no Azure dependency.
#
# Prereqs: build the solution first with dotnet build so the extension dll exists under artifacts/Debug.
# Usage: pwsh -NoProfile -File ./tests/Scripts/PS/AzclipsRenameSmoke.ps1
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$extDir = Join-Path $root 'artifacts/Debug/Microsoft.Azure.Azclips.Extensions.Resources'
$cliDir = Join-Path $root 'artifacts/Debug/Core.Cli'
$extDll = Join-Path $extDir 'Microsoft.Azure.Azclips.Extensions.Resources.dll'

if (-not (Test-Path $extDll)) { throw "Renamed extension DLL not found at $extDll. Build the solution first." }

# Resolve the extension's dependencies (Core.Abstractions, ExtensionRuntime, etc.) from the CLI output.
$probeDirs = @($extDir, $cliDir)
$resolver = [System.ResolveEventHandler]{
    param($s, $e)
    $name = ([System.Reflection.AssemblyName]::new($e.Name)).Name
    foreach ($d in $probeDirs) {
        $candidate = Join-Path $d "$name.dll"
        if (Test-Path $candidate) { return [System.Reflection.Assembly]::LoadFrom($candidate) }
    }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

$asm = [System.Reflection.Assembly]::LoadFrom($extDll)
Write-Output "Loaded: $($asm.FullName)"

# The assembly name must reflect the rename.
if ($asm.GetName().Name -ne 'Microsoft.Azure.Azclips.Extensions.Resources') {
    throw "FAIL: assembly name '$($asm.GetName().Name)' is not the renamed identifier."
}
Write-Output 'PASS  assembly name is Microsoft.Azure.Azclips.Extensions.Resources'

# Each processor type must be resolvable under its new Azclips namespace.
$types = @(
    'Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupProcessor'
    'Microsoft.Azure.Azclips.Extensions.Resources.Processors.GetResourceGroupProcessor'
    'Microsoft.Azure.Azclips.Extensions.Resources.Processors.ListResourceGroupProcessor'
    'Microsoft.Azure.Azclips.Extensions.Resources.Processors.RestProcessor_Orchestrator'
    'Microsoft.Azure.Azclips.Extensions.Resources.Processors.NewResourceGroupDeploymentProcessor_Orchestrator'
)
foreach ($t in $types) {
    $resolved = $asm.GetType($t, $true)
    Write-Output "PASS  resolved $($resolved.FullName)"
}

# Every loaded processor type must live under the renamed Azclips namespace.
$expectedPrefix = 'Microsoft.Azure.Azclips.Extensions.Resources'
$processors = $asm.GetTypes() | Where-Object { $_.Namespace -and $_.Namespace.Contains('.Processors') }
$offenders = $processors | Where-Object { -not $_.Namespace.StartsWith($expectedPrefix) }
if ($offenders) { throw "FAIL: processor types outside '$expectedPrefix': $($offenders.FullName -join ', ')" }
Write-Output "PASS  all $($processors.Count) processor types are under $expectedPrefix"

Write-Output 'All Azclips rename PowerShell smoke checks passed.'
