# Source-of-truth for the source-module -> packaged-module name mapping.
# Every consumer (packaging, changelog, docs sync) must go through this function
# so the rule can never drift between them.
#
# See `devops/scripts/packaging/powershell/package_az_modules.ps1::New-AzModulePackage`
# for the authoritative product naming (Az rollup stays 'Az'; every leaf module becomes
# 'Az.<SourceModule>').

function Get-AzPackageName
{
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceModule
    )

    if ($SourceModule -eq 'Az')
    {
        return 'Az'
    }

    return "Az.$SourceModule"
}
