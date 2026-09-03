# Pester tests for build/Sync-AzclipsCliDocs.ps1.
#
# Run: Invoke-Pester -Path tests/Cli.DocsSync.Tests/

BeforeAll {
    $env:AZCLIPS_CLI_SYNC_LOAD_ONLY = '1'
    . "$PSScriptRoot/../../build/Sync-AzclipsCliDocs.ps1"
    $env:AZCLIPS_CLI_SYNC_LOAD_ONLY = ''
}

Describe 'Get-Cli2YamlDllPath' {
    BeforeAll {
        # Redirect the function's lookup to a fake profile so we don't touch the real cache.
        $script:FakeProfile = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-fake-" + [Guid]::NewGuid().ToString('N'))
        $script:RealUserProfile = $env:USERPROFILE
    }

    AfterAll {
        $env:USERPROFILE = $script:RealUserProfile
        if (Test-Path $script:FakeProfile) { Remove-Item $script:FakeProfile -Recurse -Force }
    }

    BeforeEach {
        if (Test-Path $script:FakeProfile) { Remove-Item $script:FakeProfile -Recurse -Force }
        New-Item -ItemType Directory -Path $script:FakeProfile | Out-Null
        $env:USERPROFILE = $script:FakeProfile
    }

    It 'throws with a fetch snippet when the package cache is missing' {
        { Get-Cli2YamlDllPath -PackageName 'CLI2Yaml' -Feed 'https://example.invalid/feed' } |
            Should -Throw '*not in the local NuGet cache*'
    }

    It 'returns the DLL path when a single version is cached' {
        $version = '20260827.1.0'
        $tools = Join-Path $script:FakeProfile ".nuget/packages/cli2yaml/$version/tools"
        New-Item -ItemType Directory -Path $tools -Force | Out-Null
        Set-Content -Path (Join-Path $tools 'CLI2Yaml.dll') -Value 'stub' -NoNewline

        $result = Get-Cli2YamlDllPath -PackageName 'CLI2Yaml' -Feed 'https://example.invalid/feed'
        $result | Should -Match 'CLI2Yaml\.dll$'
        Test-Path $result | Should -BeTrue
    }

    It 'prefers the newest version when multiple are cached' {
        foreach ($v in '20260616.1.0', '20260827.1.0') {
            $tools = Join-Path $script:FakeProfile ".nuget/packages/cli2yaml/$v/tools"
            New-Item -ItemType Directory -Path $tools -Force | Out-Null
            Set-Content -Path (Join-Path $tools 'CLI2Yaml.dll') -Value $v -NoNewline
        }

        $result = Get-Cli2YamlDllPath -PackageName 'CLI2Yaml' -Feed 'https://example.invalid/feed'
        $result | Should -Match '20260827\.1\.0'
    }

    It 'throws when the package folder exists but contains no DLL' {
        $emptyDir = Join-Path $script:FakeProfile '.nuget/packages/cli2yaml/20260616.1.0/tools'
        New-Item -ItemType Directory -Path $emptyDir -Force | Out-Null

        { Get-Cli2YamlDllPath -PackageName 'CLI2Yaml' -Feed 'https://example.invalid/feed' } |
            Should -Throw '*No CLI2Yaml.dll found*'
    }
}

Describe 'Get-TargetVersionFolder' {
    It 'composes docs-ref-autogen/<VersionFolder>' {
        $result = Get-TargetVersionFolder -TargetRepoPath 'fake-target' -VersionFolder 'preview-version'
        $result -replace '\\','/' | Should -Match 'fake-target/docs-ref-autogen/preview-version$'
    }

    It 'preserves an absolute target path' {
        $abs = Join-Path ([System.IO.Path]::GetTempPath()) 'nope'
        $result = Get-TargetVersionFolder -TargetRepoPath $abs -VersionFolder 'preview-version'
        $result | Should -Match 'nope[\\/]docs-ref-autogen[\\/]preview-version$'
    }
}

Describe 'Clear-TargetLatest' {
    BeforeEach {
        $script:Root = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-clear-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:Root 'latest/nested') | Out-Null
        Set-Content -Path (Join-Path $script:Root 'latest/top.yml') -Value 'a'
        Set-Content -Path (Join-Path $script:Root 'latest/nested/child.yml') -Value 'b'
    }

    AfterEach {
        if (Test-Path $script:Root) { Remove-Item $script:Root -Recurse -Force }
    }

    It 'removes all files under latest/' {
        Clear-TargetLatest -TargetVersionFolder $script:Root
        (Get-ChildItem (Join-Path $script:Root 'latest') -Recurse -File).Count | Should -Be 0
    }

    It 'removes nested subdirectories under latest/' {
        Clear-TargetLatest -TargetVersionFolder $script:Root
        (Get-ChildItem (Join-Path $script:Root 'latest') -Recurse -Directory).Count | Should -Be 0
    }

    It 'is a no-op when latest/ does not exist' {
        $stale = Join-Path $script:Root 'nope'
        { Clear-TargetLatest -TargetVersionFolder $stale } | Should -Not -Throw
    }
}

Describe 'Test-TargetReady' {
    BeforeEach {
        $script:Target = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-target-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Target | Out-Null
        git -C $script:Target init --initial-branch=main --quiet
        git -C $script:Target config user.email 'test@example.com'
        git -C $script:Target config user.name 'Test'
        git -C $script:Target commit --allow-empty -m 'init' --quiet
        New-Item -ItemType Directory -Path (Join-Path $script:Target 'docs-ref-autogen/preview-version') -Force | Out-Null
    }

    AfterEach {
        if (Test-Path $script:Target) { Remove-Item $script:Target -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'reports no problems when target is a clean git repo with the version folder present' {
        $problems = @(Test-TargetReady -TargetRepoPath $script:Target -VersionFolder 'preview-version')
        $problems.Count | Should -Be 0
    }

    It 'flags a missing target directory' {
        $problems = @(Test-TargetReady -TargetRepoPath (Join-Path $script:Target 'nope') -VersionFolder 'preview-version')
        $problems.Count | Should -Be 1
        $problems[0] | Should -Match 'does not exist'
    }

    It 'flags a non-git target' {
        $plain = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-plain-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $plain | Out-Null
        try {
            $problems = @(Test-TargetReady -TargetRepoPath $plain -VersionFolder 'preview-version')
            $problems.Count | Should -Be 1
            $problems[0] | Should -Match 'not a git repository'
        }
        finally {
            Remove-Item $plain -Recurse -Force
        }
    }

    It 'flags a dirty working tree' {
        Set-Content -Path (Join-Path $script:Target 'dirty.txt') -Value 'x'
        $problems = @(Test-TargetReady -TargetRepoPath $script:Target -VersionFolder 'preview-version')
        $problems.Count | Should -BeGreaterThan 0
        ($problems | Where-Object { $_ -match 'working tree is dirty' }).Count | Should -BeGreaterThan 0
    }

    It 'flags a missing version folder' {
        Remove-Item (Join-Path $script:Target 'docs-ref-autogen/preview-version') -Recurse -Force
        $problems = @(Test-TargetReady -TargetRepoPath $script:Target -VersionFolder 'preview-version')
        ($problems | Where-Object { $_ -match "Target version folder 'preview-version'" }).Count | Should -BeGreaterThan 0
    }
}

Describe 'New-Cli2YamlWorkspace' {
    BeforeEach {
        $script:RepoRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-ws-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:RepoRoot 'build') | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $script:RepoRoot 'artifacts') | Out-Null

        # Stub Get-DocCommitMap.ps1 that just writes a minimal commit map.
        $stubScript = @'
[CmdletBinding()]
param([string]$OutputPath)
Set-Content -Path $OutputPath -Value '{}' -NoNewline
Write-Host "Wrote 0 verb entries to $OutputPath"
'@
        Set-Content -Path (Join-Path $script:RepoRoot 'build/Get-DocCommitMap.ps1') -Value $stubScript

        # Fake exporter output.
        $script:ExporterOutput = Join-Path $script:RepoRoot 'artifacts/cli-export/latest'
        New-Item -ItemType Directory -Path $script:ExporterOutput -Force | Out-Null
        Set-Content -Path (Join-Path $script:ExporterOutput '_root_.json') -Value '[]'
        Set-Content -Path (Join-Path $script:ExporterOutput 'vm.json') -Value '[{}]'
    }

    AfterEach {
        if (Test-Path $script:RepoRoot) { Remove-Item $script:RepoRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'creates input/latest, input-ext, and a commit map' {
        $ws = New-Cli2YamlWorkspace -RepoRoot $script:RepoRoot -ExporterOutput $script:ExporterOutput
        Test-Path (Join-Path $ws 'input/latest') | Should -BeTrue
        Test-Path (Join-Path $ws 'input-ext')    | Should -BeTrue
        Test-Path (Join-Path $ws 'input/doc_commit_map.json') | Should -BeTrue
    }

    It 'copies exporter JSON into input/latest' {
        $ws = New-Cli2YamlWorkspace -RepoRoot $script:RepoRoot -ExporterOutput $script:ExporterOutput
        $copied = @(Get-ChildItem (Join-Path $ws 'input/latest') -Filter '*.json' -File)
        $copied.Name | Sort-Object | Should -Be @('_root_.json', 'vm.json')
    }

    It 'wipes any stale workspace from a previous run' {
        $ws = New-Cli2YamlWorkspace -RepoRoot $script:RepoRoot -ExporterOutput $script:ExporterOutput
        Set-Content -Path (Join-Path $ws 'input/latest/stale.json') -Value 'stale'

        $null = New-Cli2YamlWorkspace -RepoRoot $script:RepoRoot -ExporterOutput $script:ExporterOutput
        Test-Path (Join-Path $ws 'input/latest/stale.json') | Should -BeFalse
    }
}

Describe 'Invoke-SyncApply' {
    BeforeEach {
        $script:Target = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-apply-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Target | Out-Null
        git -C $script:Target init --initial-branch=main --quiet
        git -C $script:Target config user.email 'test@example.com'
        git -C $script:Target config user.name 'Test'
        git -C $script:Target commit --allow-empty -m 'init' --quiet

        $script:VersionPath = Join-Path $script:Target 'docs-ref-autogen/preview-version/latest'
        New-Item -ItemType Directory -Path $script:VersionPath -Force | Out-Null
    }

    AfterEach {
        if (Test-Path $script:Target) { Remove-Item $script:Target -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'creates the timestamped branch, commits new content, and returns a summary' {
        Set-Content -Path (Join-Path $script:VersionPath 'vm.yml') -Value '### YamlMime:AzureCLIGroup'
        Set-Content -Path (Join-Path $script:VersionPath 'reference-index.yml') -Value '### YamlMime:AzureCLIGroup'

        $result = Invoke-SyncApply -TargetRepoPath $script:Target -VersionFolder 'preview-version' -Moniker 'azure-cli-preview' -Timestamp '202609030100'

        $result.NoChanges     | Should -BeFalse
        $result.Branch        | Should -Be 'azclips-cli-sync-202609030100'
        $result.CommitMessage | Should -Match 'azure-cli-preview -- 202609030100'
        $result.FileCount     | Should -BeGreaterThan 0
        (git -C $script:Target rev-parse --verify --quiet 'refs/heads/azclips-cli-sync-202609030100') | Should -Not -BeNullOrEmpty
    }

    It 'reports NoChanges and cleans up the branch when nothing is staged' {
        # Version folder exists but is empty; git add finds nothing new.
        $result = Invoke-SyncApply -TargetRepoPath $script:Target -VersionFolder 'preview-version' -Moniker 'azure-cli-preview' -Timestamp '202609030102'

        $result.NoChanges | Should -BeTrue
        $result.Branch    | Should -BeNullOrEmpty
        $result.FileCount | Should -Be 0
        (git -C $script:Target rev-parse --verify --quiet 'refs/heads/azclips-cli-sync-202609030102') | Should -BeNullOrEmpty
    }

    It 'refuses to run when the target branch name already exists' {
        git -C $script:Target checkout -b 'azclips-cli-sync-202609030103' 2>&1 | Out-Null
        git -C $script:Target checkout main 2>&1 | Out-Null

        Set-Content -Path (Join-Path $script:VersionPath 'vm.yml') -Value 'x'

        { Invoke-SyncApply -TargetRepoPath $script:Target -VersionFolder 'preview-version' -Moniker 'azure-cli-preview' -Timestamp '202609030103' } |
            Should -Throw '*already exists in target*'
    }

    It 'produces a clean working tree after a successful commit' {
        Set-Content -Path (Join-Path $script:VersionPath 'vm.yml') -Value 'x'

        $null = Invoke-SyncApply -TargetRepoPath $script:Target -VersionFolder 'preview-version' -Moniker 'azure-cli-preview' -Timestamp '202609030104'

        $dirty = git -C $script:Target status --porcelain 2>$null
        $dirty | Should -BeNullOrEmpty
    }
}
