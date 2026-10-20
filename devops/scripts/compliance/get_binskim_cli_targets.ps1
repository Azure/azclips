[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutRoot,

    [Parameter(Mandatory = $true)]
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Rid
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($OutRoot).Replace('\', '/').TrimEnd('/')
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'binskim-runtime-exclusions.json') -Raw | ConvertFrom-Json
if (-not $policy.files)
{
    throw 'The reviewed BinSkim runtime exclusion list is missing or empty.'
}

$targets = @(
    "$root/**/*.dll"
    "$root/**/*.exe"
    '-:file|**/msalruntime*.dll'
    '-:file|**/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/**/native/**'
)
foreach ($name in $policy.files)
{
    if ($name -notmatch '^[A-Za-z0-9_.-]+\.(dll|exe)$')
    {
        throw "Runtime exclusions must be exact binary filenames, not paths or wildcards: $name"
    }
    $targets += "-:file|$root/publish/cli/$Rid/$name"
}
$targets -join ';'
