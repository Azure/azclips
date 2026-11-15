function Test-DocsReleaseTag
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$ReleaseTag,
        [switch]$RequireFullHistory
    )

    $tagRef = "refs/tags/$ReleaseTag"
    & git check-ref-format $tagRef 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Invalid release tag '$ReleaseTag'." }

    $tagCommit = & git -C $RepoRoot rev-parse --verify --quiet "${tagRef}^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $tagCommit) { throw "Release tag '$ReleaseTag' not found or does not point to a commit." }

    $headCommit = & git -C $RepoRoot rev-parse --verify HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $headCommit) { throw "Could not resolve source checkout HEAD in '$RepoRoot'." }
    if ($tagCommit.Trim() -ne $headCommit.Trim())
    {
        throw "Source checkout HEAD does not match release tag '$ReleaseTag'. Check out the tag before syncing."
    }

    $changes = @(& git -C $RepoRoot status --porcelain --untracked-files=normal 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect source checkout: $($changes -join "`n")" }
    if ($changes.Count -gt 0) { throw "Release tag sync requires a clean source checkout.`n$($changes -join "`n")" }

    if ($RequireFullHistory)
    {
        $shallow = & git -C $RepoRoot rev-parse --is-shallow-repository 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Could not determine whether source checkout is shallow." }
        if ($shallow.Trim() -eq 'true') { throw "CLI release tag sync requires full Git history for documentation commit metadata." }
    }

    return $headCommit.Trim()
}
