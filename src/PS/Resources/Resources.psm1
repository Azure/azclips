# Import shared telemetry helper
. (Join-Path $PSScriptRoot '..\TelemetryHelper.ps1')

# Initialize the DI container for PowerShell (runs once on Import-Module)
[Core.DependencyInjection.CirrusServiceProvider]::Initialize([Core.Abstractions.ClientType]::PowerShell)

$exportsPath = Join-Path $PSScriptRoot './exports'
$defaultFunctions = Get-ChildItem function:
Get-ChildItem -Path $exportsPath -Recurse -Include '*.ps1' -File 
    | Where-Object { $defaultFunctions -notcontains $_.BaseName } 
    | ForEach-Object { . $_.FullName }
Export-ModuleMember -Function *