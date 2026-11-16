function Set-DocsTargetBranch
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetRepoPath,
        [Parameter(Mandatory)][string]$TargetBranch
    )

    $changes = @(& git -C $TargetRepoPath status --porcelain --untracked-files=normal 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect target checkout: $($changes -join "`n")" }
    if ($changes.Count -gt 0) { throw "Target working tree is dirty; cannot switch branches.`n$($changes -join "`n")" }

    $refs = @("refs/heads/$TargetBranch", "refs/remotes/$TargetBranch")
    $matches = @()
    foreach ($ref in $refs)
    {
        & git check-ref-format $ref 2>$null
        if ($LASTEXITCODE -ne 0) { throw "Invalid target branch '$TargetBranch'." }
        & git -C $TargetRepoPath show-ref --verify --quiet $ref
        if ($LASTEXITCODE -eq 0) { $matches += $ref }
        elseif ($LASTEXITCODE -ne 1) { throw "Could not inspect target branch '$TargetBranch'." }
    }
    if ($matches.Count -eq 0) { throw "Target branch '$TargetBranch' not found in '$TargetRepoPath'. Fetch the docs remote first." }
    if ($matches.Count -gt 1) { throw "Target branch '$TargetBranch' is ambiguous between a local and remote branch." }

    $commit = & git -C $TargetRepoPath rev-parse --verify "$($matches[0])^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $commit) { throw "Could not resolve target branch '$TargetBranch' to a commit." }
    $output = @(& git -C $TargetRepoPath switch --detach --no-overwrite-ignore $commit.Trim() 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Could not check out target branch '$TargetBranch':`n$($output -join "`n")" }
    Write-Host "Docs target     : $TargetBranch ($($commit.Trim()))"
}
