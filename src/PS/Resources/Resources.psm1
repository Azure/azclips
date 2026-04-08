# Import shared telemetry helper
. (Join-Path $PSScriptRoot '..\TelemetryHelper.ps1')

# Initialize the DI container for PowerShell (runs once on Import-Module)
$moduleVersion = (Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Resources.psd1')).ModuleVersion
[Core.DependencyInjection.CirrusServiceProvider]::Initialize([Core.Abstractions.ClientType]::PowerShell, $moduleVersion)

$exportsPath = Join-Path $PSScriptRoot './exports'
$defaultFunctions = Get-ChildItem function:
Get-ChildItem -Path $exportsPath -Recurse -Include '*.ps1' -File 
    | Where-Object { $defaultFunctions -notcontains $_.BaseName } 
    | ForEach-Object { . $_.FullName }
Export-ModuleMember -Function *