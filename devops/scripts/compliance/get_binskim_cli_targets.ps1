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
# Versioned runtime files (for example mscordaccore_amd64_amd64_<version>.dll) change names on every runtime update.
# A pattern may only replace the trailing version segment of an underscore-delimited name, so it cannot become a blanket exclusion.
foreach ($pattern in @($policy.patterns))
{
    if (-not $pattern) { continue }
    if ($pattern -notmatch '^[A-Za-z][A-Za-z0-9.-]*(_[A-Za-z0-9.-]+){2,}_\*\.dll$')
    {
        throw "Runtime exclusion patterns must be a versioned filename of the form name_part_part_*.dll: $pattern"
    }
    $targets += "-:file|$root/$pattern"
}
$targets -join ';'
