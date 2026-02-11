# Initialize CommandHost (process-level singleton, shared across all Cirrus modules)
if (-not $global:CirrusCommandHost) {
    $builder = [Core.Engine.CirrusHostBuilder]::new()
    [Core.Engine.ServiceCollectionExtensions]::AddCirrusCore($builder.Services) | Out-Null
    [Core.Engine.ServiceCollectionExtensions]::AddCirrusPS($builder.Services) | Out-Null
    $sp = $builder.BuildAsync().GetAwaiter().GetResult()
    $global:CirrusCommandHost = [Core.Engine.CommandHost]::new($sp)
}
$script:CirrusHost = $global:CirrusCommandHost

# Extension assembly path — resolved from the already-loaded assembly (via RequiredAssemblies)
$script:ExtensionAssemblyPath = [Microsoft.Azure.Cirrus.Extensions.Resources.ExtensionIndex].Assembly.Location

# Import shared telemetry helper
. (Join-Path $PSScriptRoot '..\TelemetryHelper.ps1')

$exportsPath = Join-Path $PSScriptRoot './exports'
$defaultFunctions = Get-ChildItem function:
Get-ChildItem -Path $exportsPath -Recurse -Include '*.ps1' -File 
    | Where-Object { $defaultFunctions -notcontains $_.BaseName } 
    | ForEach-Object { . $_.FullName }
Export-ModuleMember -Function *