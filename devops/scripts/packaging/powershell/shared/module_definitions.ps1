<#
.SYNOPSIS
Discovers the packaged Az modules in their canonical installation order.

.DESCRIPTION
Reads source manifests once, with Accounts (or legacy Login) first, Az last, and
other modules ordered by manifest path. Callers resolve the source root; uninstall
reverses this list.
#>
function Get-AzModuleDefinitions
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePsRoot
    )

    $manifests = @(Get-ChildItem -LiteralPath $SourcePsRoot -Filter '*.psd1' -File -Recurse -ErrorAction Stop |
        Sort-Object -Property @{ Expression = { if ($_.BaseName -in 'Accounts', 'Login') { 0 } elseif ($_.BaseName -eq 'Az') { 2 } else { 1 } } }, FullName)

    if ($manifests.Count -eq 0)
    {
        throw "No Az module definitions were found under: $SourcePsRoot"
    }

    foreach ($manifest in $manifests)
    {
        $manifestData = Import-PowerShellDataFile -LiteralPath $manifest.FullName -ErrorAction Stop
        $version = [string]$manifestData.ModuleVersion
        if ([string]::IsNullOrWhiteSpace($version) -or -not ($version -as [version]))
        {
            throw "Invalid or missing ModuleVersion in Az module definition: $($manifest.FullName)"
        }

        $sourceModule = $manifest.BaseName
        $requiredModules = @(
            foreach ($requiredModule in $manifestData.RequiredModules)
            {
                $name = if ($requiredModule -is [string]) { $requiredModule } else { [string]$requiredModule.ModuleName }
                if ([string]::IsNullOrWhiteSpace($name)) { throw "Invalid RequiredModules entry in: $($manifest.FullName)" }
                if ($name -match '(^|[/\\])([^/\\]+)\.psd1$')
                {
                    $baseName = $Matches[2]
                    $name = if ($baseName -eq 'Az' -or $baseName.StartsWith('Az.')) { $baseName } else { "Az.$baseName" }
                }
                $name
            }
        )
        [pscustomobject]@{
            SourceModule = $sourceModule
            PackageName = if ($sourceModule -eq 'Az') { 'Az' } else { "Az.$sourceModule" }
            Version = $version
            CmdletsToExport = @($manifestData.CmdletsToExport)
            RequiredModules = $requiredModules
        }
    }
}
