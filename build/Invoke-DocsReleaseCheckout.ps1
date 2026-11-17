function Invoke-DocsReleaseCheckout
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourceRoot,
        [Parameter(Mandatory)][string]$ReleaseTag,
        [Parameter(Mandatory)][string]$ScriptName,
        [Parameter(Mandatory)][hashtable]$ScriptArguments,
        [switch]$KeepOnSuccess
    )

    $tagRef = "refs/tags/$ReleaseTag"
    & git check-ref-format $tagRef 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Invalid release tag '$ReleaseTag'." }
    $commit = & git -C $SourceRoot rev-parse --verify --quiet "${tagRef}^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $commit) { throw "Release tag '$ReleaseTag' not found or does not point to a commit." }

    $workspace = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-docs-source-" + [Guid]::NewGuid().ToString('N'))
    $checkout = Join-Path $workspace 'source'
    $success = $false
    try
    {
        New-Item -ItemType Directory -Path $workspace | Out-Null
        $cloneOutput = @(& git clone --no-hardlinks --no-checkout -- $SourceRoot $checkout 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Release source clone failed:`n$($cloneOutput -join "`n")" }
        $checkoutOutput = @(& git -C $checkout switch --detach $commit.Trim() 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Could not check out release tag '$ReleaseTag':`n$($checkoutOutput -join "`n")" }
        $script = Join-Path (Join-Path $checkout 'build') $ScriptName
        if (-not (Test-Path -LiteralPath (Join-Path $checkout 'build/Invoke-DocsReleaseCheckout.ps1')) -or
            -not (Test-Path -LiteralPath $script))
        {
            throw "Release tag '$ReleaseTag' does not contain the tag-aware docs sync scripts."
        }
        & $script @ScriptArguments -ReleaseCheckout
        $success = $true
    }
    finally
    {
        if ($success -and $KeepOnSuccess)
        {
            Write-Host "Release source workspace retained for dry-run output: $workspace"
        }
        elseif ($success -and (Test-Path -LiteralPath $workspace))
        {
            Remove-Item -LiteralPath $workspace -Recurse -Force
        }
        elseif (Test-Path -LiteralPath $workspace)
        {
            Write-Warning "Release source workspace retained for inspection: $workspace"
        }
    }
}
