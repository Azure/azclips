[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArtifactsRoot,

    [string]$SourcePsRoot,

    [string]$PwshPath = 'pwsh',

    [ValidateSet('Accounts', 'Config', 'Resources', 'Compute', 'MsGraph', 'Storage')]
    [string[]]$Module = @('Accounts', 'Config', 'Resources', 'Compute', 'MsGraph', 'Storage')
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command -Name $PwshPath -ErrorAction SilentlyContinue))
{
    throw "PowerShell executable was not found: $PwshPath"
}

function Resolve-ManifestReference
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestDirectory,

        [Parameter(Mandatory = $true)]
        [string]$Reference
    )

    if ([System.IO.Path]::IsPathRooted($Reference))
    {
        return $Reference
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ManifestDirectory $Reference))
}

$resolvedArtifactsRoot = (Resolve-Path -Path $ArtifactsRoot).Path
$modulesRoot = Join-Path $resolvedArtifactsRoot 'PS'

if (-not (Test-Path -Path $modulesRoot -PathType Container))
{
    throw "PowerShell module artifact root was not found: $modulesRoot"
}

$resolvedSourcePsRoot = $null
if ($SourcePsRoot -and (Test-Path -Path $SourcePsRoot -PathType Container))
{
    $resolvedSourcePsRoot = (Resolve-Path -Path $SourcePsRoot).Path
}

$validated = New-Object System.Collections.Generic.List[string]

foreach ($moduleName in $Module)
{
    $manifestPath = Join-Path $modulesRoot "$moduleName/$moduleName.psd1"
    if (-not (Test-Path -Path $manifestPath -PathType Leaf))
    {
        throw "Missing staged module manifest: $manifestPath"
    }

    $manifestDirectory = Split-Path -Parent $manifestPath
    $rawManifest = Get-Content -Path $manifestPath -Raw
    if ($rawManifest -match '/artifacts/Debug/' -or $rawManifest -match '\\artifacts\\Debug\\')
    {
        throw "Manifest still references Debug artifacts after staging: $manifestPath"
    }

    $manifestData = Import-PowerShellDataFile -Path $manifestPath
    $declaredCmdlets = @($manifestData.CmdletsToExport) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($declaredCmdlets.Count -eq 0)
    {
        throw "Manifest exports no cmdlets: $manifestPath"
    }

    $references = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($manifestData.RootModule))
    {
        $references.Add([string]$manifestData.RootModule)
    }
    foreach ($assembly in @($manifestData.RequiredAssemblies))
    {
        if (-not [string]::IsNullOrWhiteSpace($assembly))
        {
            $references.Add([string]$assembly)
        }
    }
    foreach ($nestedModule in @($manifestData.NestedModules))
    {
        if (-not [string]::IsNullOrWhiteSpace($nestedModule))
        {
            $references.Add([string]$nestedModule)
        }
    }

    foreach ($reference in $references)
    {
        $resolvedReference = Resolve-ManifestReference -ManifestDirectory $manifestDirectory -Reference $reference
        if (-not (Test-Path -Path $resolvedReference -PathType Leaf))
        {
            throw "Manifest reference does not exist for module '$moduleName': $reference -> $resolvedReference"
        }
    }

    # Staged manifests intentionally use absolute RequiredAssemblies paths. Validate the
    # real import path in an isolated pwsh process because binary modules and their
    # dependencies are not reliably unloadable within a long-running PowerShell session.
    $expectedJson = $declaredCmdlets | ConvertTo-Json -Compress
    $accountsManifestPath = Join-Path $modulesRoot 'Accounts/Accounts.psd1'
    & $PwshPath -NoProfile -Command {
        param($ManifestPath, $ModuleName, $ExpectedJson, $AccountsManifestPath)
        $ErrorActionPreference = 'Stop'

        $expectedCmdlets = @($ExpectedJson | ConvertFrom-Json)
        if ($ModuleName -ne 'Accounts')
        {
            Import-Module -Name $AccountsManifestPath -Force -Verbose -ErrorAction Stop
        }

        $loadedModule = Import-Module -Name $ManifestPath -Force -PassThru -Verbose -ErrorAction Stop
        $exportedCmdlets = @($loadedModule.ExportedCommands.Keys)

        foreach ($declaredCmdlet in $expectedCmdlets)
        {
            if ($exportedCmdlets -notcontains $declaredCmdlet)
            {
                throw "Module '$ModuleName' did not export declared cmdlet '$declaredCmdlet'. Exported: $($exportedCmdlets -join ', ')"
            }
        }
    } -Args $manifestPath, $moduleName, $expectedJson, $accountsManifestPath

    if ($LASTEXITCODE -ne 0)
    {
        throw "PowerShell module import validation failed for '$moduleName' (exit $LASTEXITCODE)."
    }

    if ($resolvedSourcePsRoot)
    {
        $helpRoot = Join-Path $resolvedSourcePsRoot "$moduleName/help"
        foreach ($declaredCmdlet in $declaredCmdlets)
        {
            $helpPath = Join-Path $helpRoot "$declaredCmdlet.md"
            if (-not (Test-Path -Path $helpPath -PathType Leaf))
            {
                Write-Warning "Missing markdown help for $moduleName/$declaredCmdlet at $helpPath"
            }
        }
    }

    $validated.Add("$moduleName ($($declaredCmdlets.Count) cmdlets)")
}

Write-Host "Validated PowerShell modules: $($validated -join ', ')"