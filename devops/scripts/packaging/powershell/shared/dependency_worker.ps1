[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RequestPath,
    [Parameter(Mandatory)][string]$ResultPath
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'phase_artifacts.ps1')
$request = Get-Content -LiteralPath $RequestPath -Raw | ConvertFrom-Json
# PowerShell may prepend user locations at startup. Reset the path inside every worker.
$env:PSModulePath = @($request.ModuleRoot, (Join-Path $PSHOME 'Modules')) -join [IO.Path]::PathSeparator
$global:PSModuleAutoLoadingPreference = 'ModuleQualified'
Import-Module Microsoft.PowerShell.Management, Microsoft.PowerShell.Utility -ErrorAction Stop
$result = [ordered]@{ status = 'Failed'; errorId = $null; message = $null; diagnostics = @(); inventory = @(); imported = @() }
$resolverWarnings = @()

try
{
    if ($request.Operation -eq 'Resolve')
    {
        Import-Module PowerShellGet -ErrorAction Stop
        Save-Module -Name $request.Module.Name -RequiredVersion $request.Module.Version `
            -Repository $request.RepositoryName -Path $request.ModuleRoot -Force -WarningVariable resolverWarnings -ErrorAction Stop
    }
    elseif ($request.Operation -eq 'Import')
    {
        if ($request.Conflict)
        {
            Import-Module -Name $request.ConflictPath -Global -ErrorAction Stop
        }
        $module = Import-Module -Name $request.Module.Name -RequiredVersion $request.Module.Version -PassThru -ErrorAction Stop
        foreach ($command in $request.Module.Commands)
        {
            if ($command -notin $module.ExportedCommands.Keys) { throw "$($module.Name) does not export $command." }
        }
        foreach ($expected in $request.Closure)
        {
            $imported = @(Get-Module -Name $expected.Name | Where-Object Version -eq ([version]$expected.Version))
            if ($imported.Count -ne 1) { throw "Import did not load dependency $($expected.Name) $($expected.Version)." }
            $expectedRoot = [IO.Path]::GetFullPath($request.ModuleRoot) + [IO.Path]::DirectorySeparatorChar
            if (-not $imported[0].ModuleBase.StartsWith($expectedRoot, [StringComparison]::OrdinalIgnoreCase))
            {
                throw "Dependency $($expected.Name) was imported from outside the isolated module directory."
            }
            $result.imported += @{ name = $imported[0].Name; version = $imported[0].Version.ToString() }
        }
    }
    else { throw "Unknown dependency worker operation: $($request.Operation)" }
    $result.status = 'Succeeded'
}
catch
{
    $result.errorId = $_.FullyQualifiedErrorId
    $result.message = $_.Exception.Message
}
finally
{
    $result.diagnostics = @($resolverWarnings | ForEach-Object { $_.ToString() })
    $result.inventory = @(
        Get-ChildItem -LiteralPath $request.ModuleRoot -Filter '*.psd1' -Recurse -File -ErrorAction Stop |
            Where-Object { $_.Directory.Parent.Name -eq $_.BaseName } |
            ForEach-Object { @{ name = $_.BaseName; version = $_.Directory.Name } }
    )
    Write-PSValidationJson -Path $ResultPath -Value $result
}
if ($result.status -ne 'Succeeded') { exit 1 }
