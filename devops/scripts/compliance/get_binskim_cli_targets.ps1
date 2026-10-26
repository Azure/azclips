[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ScanRoot
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($ScanRoot).Replace('\', '/').TrimEnd('/')
$policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'binskim-runtime-exclusions.json') -Raw | ConvertFrom-Json
if (-not $policy.files)
{
    throw 'The reviewed BinSkim runtime exclusion list is missing or empty.'
}

$targets = @(
    "$root/**/*.dll"
    "$root/**/*.exe"
    "-:file|$root/msalruntime*.dll"
    "-:file|$root/**/msalruntime*.dll"
    "-:file|$root/**/Microsoft.Azure.Azclips.Extensions.Agent/deps/runtimes/**/native/**"
)
foreach ($name in $policy.files)
{
    if ($name -notmatch '^[A-Za-z0-9_.-]+\.(dll|exe)$')
    {
        throw "Runtime exclusions must be exact binary filenames, not paths or wildcards: $name"
    }
    $targets += "-:file|$root/$name"
}
$targets -join ';'
