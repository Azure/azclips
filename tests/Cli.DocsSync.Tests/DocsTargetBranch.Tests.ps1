BeforeAll {
    . "$PSScriptRoot/../../build/Set-DocsTargetBranch.ps1"
}

Describe 'Set-DocsTargetBranch' {
    BeforeEach {
        $script:Repo = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Repo | Out-Null
        git -C $script:Repo init --initial-branch=main --quiet
        git -C $script:Repo config user.email 'test@example.com'
        git -C $script:Repo config user.name 'Test'
        Set-Content -LiteralPath (Join-Path $script:Repo 'tracked.txt') -Value 'base'
        git -C $script:Repo add --all
        git -C $script:Repo commit -m 'base' --quiet
        git -C $script:Repo switch -c review --quiet
        Set-Content -LiteralPath (Join-Path $script:Repo 'tracked.txt') -Value 'review'
        git -C $script:Repo commit -am 'review' --quiet
        $script:ReviewCommit = (git -C $script:Repo rev-parse HEAD).Trim()
        git -C $script:Repo update-ref refs/remotes/upstream/review $script:ReviewCommit
        git -C $script:Repo switch main --quiet
    }

    It 'checks out the requested remote-tracking branch as the sync base' {
        Set-DocsTargetBranch -TargetRepoPath $script:Repo -TargetBranch 'upstream/review'
        (git -C $script:Repo rev-parse HEAD).Trim() | Should -Be $script:ReviewCommit
        Get-Content -LiteralPath (Join-Path $script:Repo 'tracked.txt') | Should -Be 'review'
        git -C $script:Repo switch -c generated --quiet
        (git -C $script:Repo rev-parse HEAD).Trim() | Should -Be $script:ReviewCommit
    }

    It 'checks out a local branch' {
        Set-DocsTargetBranch -TargetRepoPath $script:Repo -TargetBranch 'review'
        (git -C $script:Repo rev-parse HEAD).Trim() | Should -Be $script:ReviewCommit
    }

    It 'refuses a dirty checkout or missing branch without switching' {
        $original = (git -C $script:Repo rev-parse HEAD).Trim()
        { Set-DocsTargetBranch -TargetRepoPath $script:Repo -TargetBranch 'missing' } | Should -Throw '*not found*'
        Set-Content -LiteralPath (Join-Path $script:Repo 'untracked.txt') -Value 'keep'
        { Set-DocsTargetBranch -TargetRepoPath $script:Repo -TargetBranch 'review' } | Should -Throw '*dirty*'
        (git -C $script:Repo rev-parse HEAD).Trim() | Should -Be $original
    }

    It 'does not overwrite ignored files when switching branches' {
        git -C $script:Repo switch review --quiet
        Set-Content -LiteralPath (Join-Path $script:Repo 'ignored.txt') -Value 'tracked on review'
        git -C $script:Repo add ignored.txt
        git -C $script:Repo commit -m 'add file' --quiet
        git -C $script:Repo switch main --quiet
        Add-Content -LiteralPath (Join-Path $script:Repo '.git/info/exclude') -Value 'ignored.txt'
        Set-Content -LiteralPath (Join-Path $script:Repo 'ignored.txt') -Value 'keep'
        { Set-DocsTargetBranch -TargetRepoPath $script:Repo -TargetBranch 'review' } | Should -Throw '*Could not check out*'
        Get-Content -LiteralPath (Join-Path $script:Repo 'ignored.txt') | Should -Be 'keep'
    }
}
