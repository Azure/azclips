BeforeAll {
    . "$PSScriptRoot/../../build/Test-DocsReleaseTag.ps1"
}

Describe 'Test-DocsReleaseTag' {
    BeforeEach {
        $script:Repo = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Repo | Out-Null
        git -C $script:Repo init --initial-branch=main --quiet
        git -C $script:Repo config user.email 'test@example.com'
        git -C $script:Repo config user.name 'Test'
        Set-Content -LiteralPath (Join-Path $script:Repo 'tracked.txt') -Value 'source'
        $build = Join-Path $script:Repo 'build'
        New-Item -ItemType Directory -Path $build | Out-Null
        foreach ($file in @('Test-DocsReleaseTag.ps1', 'Set-DocsTargetBranch.ps1', 'Invoke-DocsPullRequest.ps1', 'Invoke-DocsReleaseCheckout.ps1', 'Sync-AzClipsCliDocs.ps1', 'Sync-AzclipsPowerShellDocs.ps1', 'Get-AzPackageName.ps1', 'cli-docs-sync.config.json', 'ps-docs-sync.config.json')) {
            Copy-Item -LiteralPath (Join-Path "$PSScriptRoot/../../build" $file) -Destination $build
        }
        git -C $script:Repo add --all
        git -C $script:Repo commit -m 'release' --quiet
        git -C $script:Repo tag -a v1.0.0 -m 'release'
    }

    It 'accepts a clean checkout at the annotated tag and returns its commit' {
        $expected = (git -C $script:Repo rev-parse HEAD).Trim()
        Test-DocsReleaseTag -RepoRoot $script:Repo -ReleaseTag 'v1.0.0' -RequireFullHistory | Should -Be $expected
        git -C $script:Repo tag 'release/v1'
        Test-DocsReleaseTag -RepoRoot $script:Repo -ReleaseTag 'release/v1' | Should -Be $expected
    }

    It 'rejects a missing tag or a tag whose commit differs from HEAD' {
        { Test-DocsReleaseTag -RepoRoot $script:Repo -ReleaseTag 'missing' } | Should -Throw '*not found*'
        { Test-DocsReleaseTag -RepoRoot $script:Repo -ReleaseTag 'main' } | Should -Throw '*not found*'
        git -C $script:Repo commit --allow-empty -m 'later' --quiet
        { Test-DocsReleaseTag -RepoRoot $script:Repo -ReleaseTag 'v1.0.0' } | Should -Throw '*does not match*'
    }

    It 'rejects shallow history when required' {
        $shallowRepo = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        git clone --quiet --depth 1 "file:///$($script:Repo -replace '\\', '/')" $shallowRepo
        { Test-DocsReleaseTag -RepoRoot $shallowRepo -ReleaseTag 'v1.0.0' -RequireFullHistory } |
            Should -Throw '*requires full Git history*'
    }

    It 'rejects dirty tracked or untracked source' -ForEach @(
        @{ File = 'tracked.txt' }
        @{ File = 'untracked.txt' }
    ) {
        Set-Content -LiteralPath (Join-Path $script:Repo $File) -Value 'change'
        { Test-DocsReleaseTag -RepoRoot $script:Repo -ReleaseTag 'v1.0.0' } | Should -Throw '*clean source checkout*'
    }

    It 'enforces Release and a build in the CLI entry point' {
        $cli = Join-Path $script:Repo 'build/Sync-AzClipsCliDocs.ps1'
        { & $cli -ReleaseTag 'v1.0.0' -NoBuild } | Should -Throw '*requires a build*'
        { & $cli -ReleaseTag 'v1.0.0' -Configuration Debug } | Should -Throw '*requires -Configuration Release*'
        { & $cli -Push -TargetBranch 'review' -HeadRepository 'fork/docs' -Configuration Debug } | Should -Throw '*requires -Configuration Release*'
        { & $cli -Push -TargetBranch 'review' -HeadRepository 'fork/docs' -NoBuild } | Should -Throw '*requires a Release build*'
        New-Item -ItemType Directory -Path (Join-Path $script:Repo 'artifacts/Release') -Force | Out-Null
        { & $cli -ReleaseTag 'v1.0.0' -ReleaseCheckout } | Should -Throw '*requires a fresh checkout*'
    }

    It 'checks the tag in the PowerShell entry point before planning' {
        $ps = Join-Path $script:Repo 'build/Sync-AzclipsPowerShellDocs.ps1'
        { & $ps -ReleaseTag 'missing' } | Should -Throw '*not found*'
    }

    It 'runs the tagged PowerShell source without switching the caller checkout' {
        $module = Join-Path $script:Repo 'src/PS/Example'
        New-Item -ItemType Directory -Path (Join-Path $module 'help') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $module 'Example.psd1') -Value "@{ ModuleVersion = '1.0'; Description = 'fixture' }"
        Set-Content -LiteralPath (Join-Path $module 'help/Get-Example.md') -Value "## SYNOPSIS`nTagged source."
        git -C $script:Repo add --all
        git -C $script:Repo commit -m 'tagged content' --quiet
        git -C $script:Repo tag v2
        git -C $script:Repo commit --allow-empty -m 'later' --quiet
        $original = (git -C $script:Repo rev-parse HEAD).Trim()
        $ps = Join-Path $script:Repo 'build/Sync-AzclipsPowerShellDocs.ps1'
        $output = & $ps -ReleaseTag 'v2' -TargetRepoPath (Join-Path $script:Repo 'missing-docs') 6>&1 | Out-String
        $output | Should -Match 'Release source  : v2'
        $output | Should -Match 'Get-Example.md'
        (git -C $script:Repo rev-parse HEAD).Trim() | Should -Be $original
    }
}
