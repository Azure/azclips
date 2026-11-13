function Invoke-DocsPullRequest
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$TargetRepository,
        [Parameter(Mandatory)][string]$HeadRepository,
        [Parameter(Mandatory)][string]$TargetBranch,
        [Parameter(Mandatory)][string]$SourceRoot,
        [string]$ReleaseTag,
        [Parameter(Mandatory)][string]$BranchPrefix,
        [Parameter(Mandatory)][string]$Title,
        [Parameter(Mandatory)][scriptblock]$Sync,
        [string]$Timestamp
    )

    foreach ($repository in @($TargetRepository, $HeadRepository))
    {
        if ($repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')
        {
            throw "Repository '$repository' must be a GitHub owner/repository name."
        }
    }
    if (($TargetRepository.Split('/')[1]) -ine ($HeadRepository.Split('/')[1]))
    {
        throw "The head repository must have the same repository name as the target."
    }
    & git check-ref-format "refs/heads/$TargetBranch" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Invalid target branch '$TargetBranch'." }

    $sourceCommit = & git -C $SourceRoot rev-parse --verify HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $sourceCommit) { throw 'Could not resolve AzClips source commit.' }
    $sourceChanges = @(& git -C $SourceRoot status --porcelain --untracked-files=normal 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Could not inspect AzClips source checkout: $($sourceChanges -join "`n")" }

    $workspace = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-docs-pr-" + [Guid]::NewGuid().ToString('N'))
    $targetPath = Join-Path $workspace 'docs'
    $success = $false
    try
    {
        New-Item -ItemType Directory -Path $workspace | Out-Null
        $cloneOutput = @(& git clone --depth=1 --single-branch "--branch=$TargetBranch" "https://github.com/$TargetRepository.git" $targetPath 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Docs clone failed:`n$($cloneOutput -join "`n")" }
        & git -C $targetPath show-ref --verify --quiet "refs/remotes/origin/$TargetBranch"
        if ($LASTEXITCODE -ne 0) { throw "Target '$TargetBranch' is not a branch in '$TargetRepository'." }
        $baseCommit = & git -C $targetPath rev-parse --verify HEAD 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $baseCommit) { throw 'Could not resolve docs target commit.' }

        $timestamp = if ($Timestamp) { $Timestamp } else { [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8) }
        $branch = "$BranchPrefix$timestamp"
        & $Sync $targetPath $timestamp
        $currentBranch = & git -C $targetPath branch --show-current
        if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the generated docs branch.' }
        if ($currentBranch.Trim() -ne $branch)
        {
            $currentCommit = & git -C $targetPath rev-parse --verify HEAD
            if ($LASTEXITCODE -ne 0 -or $currentCommit.Trim() -ne $baseCommit.Trim())
            {
                throw 'Sync did not leave the expected branch or the original target commit.'
            }
            Write-Host 'No documentation changes; no PR created.'
            $success = $true
            return
        }

        $githubDir = Join-Path $targetPath '.github'
        $templates = @()
        if (Test-Path -LiteralPath $githubDir)
        {
            $templates += @(Get-ChildItem -LiteralPath $githubDir -File | Where-Object Name -ieq 'pull_request_template.md')
            $templateDirs = @(Get-ChildItem -LiteralPath $githubDir -Directory | Where-Object Name -ieq 'pull_request_template')
            foreach ($dir in $templateDirs) { $templates += @(Get-ChildItem -LiteralPath $dir.FullName -File -Filter '*.md') }
        }
        if ($templates.Count -gt 1) { throw 'Multiple PR templates found; select a template before publishing.' }
        $template = if ($templates.Count -eq 1) { Get-Content -LiteralPath $templates[0].FullName -Raw } else { '# PR Summary' }
        $sourceDescription = if ($ReleaseTag) { "tag ``$ReleaseTag``" } else { 'current checkout' }
        $dirtyNote = if ($sourceChanges.Count -gt 0) { ' (with uncommitted changes)' } else { '' }
        $summary = "Generated AzClips documentation from $sourceDescription at ``$($sourceCommit.Trim())``$dirtyNote.`n`nDocs base: ``$TargetBranch`` at ``$($baseCommit.Trim())``."
        $heading = [regex]::Match($template, '(?m)^# PR Summary[^\r\n]*(?:\r?\n|$)')
        $body = if ($heading.Success) {
            $template.Insert($heading.Index + $heading.Length, "`n$summary`n")
        } else {
            "$template`n`n$summary`n"
        }

        $pushOutput = @(& git -C $targetPath push "https://github.com/$HeadRepository.git" "refs/heads/${branch}:refs/heads/${branch}" 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Could not push docs branch '$branch':`n$($pushOutput -join "`n")" }
        Write-Host "Pushed docs branch: $HeadRepository $branch"
        $head = if ($HeadRepository -ieq $TargetRepository) { $branch } else { "$($HeadRepository.Split('/')[0]):$branch" }
        $prOutput = @(& gh pr create --repo $TargetRepository --base $TargetBranch --head $head --draft --title $Title --body $body 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Branch '$branch' was pushed to '$HeadRepository', but PR creation failed:`n$($prOutput -join "`n")" }
        $prOutput | ForEach-Object { Write-Host $_ }
        $success = $true
    }
    finally
    {
        if ($success -and (Test-Path -LiteralPath $workspace))
        {
            Remove-Item -LiteralPath $workspace -Recurse -Force
        }
        elseif (Test-Path -LiteralPath $workspace)
        {
            Write-Warning "Docs PR workspace retained for inspection: $workspace"
        }
    }
}
