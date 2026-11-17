# Pester tests for build/Sync-AzClipsCliDocs.ps1.
#
# Run: Invoke-Pester -Path tests/Cli.DocsSync.Tests/

BeforeAll {
    $env:AZCLIPS_CLI_SYNC_LOAD_ONLY = '1'
    . "$PSScriptRoot/../../build/Sync-AzClipsCliDocs.ps1"
    $env:AZCLIPS_CLI_SYNC_LOAD_ONLY = ''
}

Describe 'Get-Cli2YamlDllPath' {
    BeforeAll {
        # Redirect the function's lookup to a fake profile so we don't touch the real cache.
        $script:FakeProfile = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-fake-" + [Guid]::NewGuid().ToString('N'))
        $script:RealUserProfile   = $env:USERPROFILE
        $script:RealNugetPackages = $env:NUGET_PACKAGES
    }

    AfterAll {
        $env:USERPROFILE   = $script:RealUserProfile
        $env:NUGET_PACKAGES = $script:RealNugetPackages
        if (Test-Path $script:FakeProfile) { Remove-Item $script:FakeProfile -Recurse -Force }
    }

    BeforeEach {
        if (Test-Path $script:FakeProfile) { Remove-Item $script:FakeProfile -Recurse -Force }
        New-Item -ItemType Directory -Path $script:FakeProfile | Out-Null
        $env:USERPROFILE   = $script:FakeProfile
        # Clear NUGET_PACKAGES so tests exercise the USERPROFILE fallback path deterministically.
        $env:NUGET_PACKAGES = ''
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

    It 'honours NUGET_PACKAGES over the USERPROFILE fallback' {
        # Simulate a CI-style redirect: package resolves under NUGET_PACKAGES, not USERPROFILE.
        $altRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-nugetalt-" + [Guid]::NewGuid().ToString('N'))
        try {
            $tools = Join-Path $altRoot 'cli2yaml/20260827.1.0/tools'
            New-Item -ItemType Directory -Path $tools -Force | Out-Null
            Set-Content -Path (Join-Path $tools 'CLI2Yaml.dll') -Value 'stub' -NoNewline
            $env:NUGET_PACKAGES = $altRoot

            $result = Get-Cli2YamlDllPath -PackageName 'CLI2Yaml' -Feed 'https://example.invalid/feed'
            $result | Should -Match ([regex]::Escape($altRoot))
            $result | Should -Match 'CLI2Yaml\.dll$'
        }
        finally {
            $env:NUGET_PACKAGES = ''
            if (Test-Path $altRoot) { Remove-Item $altRoot -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
}

Describe 'Invoke-CliExporter' {
    It 'passes the selected configuration artifacts directory to the exporter' -ForEach @(
        @{ Configuration = 'Debug' }
        @{ Configuration = 'Release' }
    ) {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $project = Join-Path $root 'export.csproj'
        New-Item -ItemType Directory -Path (Join-Path $root 'artifacts/cli-export/latest') -Force | Out-Null
        Set-Content -LiteralPath $project -Value 'fixture'
        Mock dotnet { $global:LASTEXITCODE = 0; 'exported' }

        Invoke-CliExporter -RepoRoot $root -ExportProject 'export.csproj' -Configuration $Configuration | Out-Null

        Should -Invoke dotnet -Times 1 -Exactly -ParameterFilter {
            $args -contains '--artifacts-root' -and
            $args -contains (Join-Path (Join-Path $root 'artifacts') $Configuration) -and
            $args -contains $Configuration
        }
    }

    It 'requires freshly produced root and command-group files for release output' {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $output = Join-Path $root 'release-output'
        $latest = Join-Path $output 'latest'
        New-Item -ItemType Directory -Path $latest -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $root 'export.csproj') -Value 'fixture'
        Mock dotnet { $global:LASTEXITCODE = 0 }

        { Invoke-CliExporter -RepoRoot $root -ExportProject 'export.csproj' -Configuration Release -OutputFolder $output -RequireOutput } |
            Should -Throw '*did not produce the root and command-group JSON*'
        Set-Content -LiteralPath (Join-Path $latest '_root_.json') -Value '[]'
        Set-Content -LiteralPath (Join-Path $latest 'group.json') -Value '[]'
        Invoke-CliExporter -RepoRoot $root -ExportProject 'export.csproj' -Configuration Release -OutputFolder $output -RequireOutput |
            Should -Be $latest
        Should -Invoke dotnet -ParameterFilter { $args -contains '--output-folder' -and $args -contains $output }
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

    It 'uses the injected CommitAuthor and CommitEmail when provided' {
        Set-Content -Path (Join-Path $script:VersionPath 'vm.yml') -Value 'x'

        $result = Invoke-SyncApply -TargetRepoPath $script:Target -VersionFolder 'preview-version' -Moniker 'azure-cli-preview' -Timestamp '202609030105' -CommitAuthor 'AzClips Docs Bot' -CommitEmail 'azclips-docs-bot@microsoft.com'

        $author = git -C $script:Target log -1 --format='%an <%ae>' $result.Branch 2>$null
        $author | Should -Match 'AzClips Docs Bot <azclips-docs-bot@microsoft.com>'
    }

    It 'does not persist the injected identity to the target repo config' {
        Set-Content -Path (Join-Path $script:VersionPath 'vm.yml') -Value 'x'

        $null = Invoke-SyncApply -TargetRepoPath $script:Target -VersionFolder 'preview-version' -Moniker 'azure-cli-preview' -Timestamp '202609030106' -CommitAuthor 'AzClips Docs Bot' -CommitEmail 'azclips-docs-bot@microsoft.com'

        (git -C $script:Target config user.name) | Should -Be 'Test'
        (git -C $script:Target config user.email) | Should -Be 'test@example.com'
    }
}

Describe 'New-SyncManifest' {
    BeforeAll {
        $script:FakeCli2YamlDll = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-fakedll-" + [Guid]::NewGuid().ToString('N')) `
            | ForEach-Object {
                $folder = Join-Path $_ 'cli2yaml/20260827.1.0/tools'
                New-Item -ItemType Directory -Path $folder -Force | Out-Null
                $dll = Join-Path $folder 'CLI2Yaml.dll'
                Set-Content -Path $dll -Value 'stub' -NoNewline
                $dll
            }
    }

    BeforeEach {
        $script:Output = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-out-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:Output 'latest') -Force | Out-Null
        Set-Content -Path (Join-Path $script:Output 'latest/vm.yml') -Value @"
### YamlMime:AzureCLIGroup
directCommands:
- editLink: https://github.com/Azure/azclips/blob/main/src/CLI/Help/Docs/vm/index.md
"@
        Set-Content -Path (Join-Path $script:Output 'latest/TOC.yml') -Value '- name: Reference'
    }

    AfterEach {
        if (Test-Path $script:Output) { Remove-Item $script:Output -Recurse -Force -ErrorAction SilentlyContinue }
    }

    AfterAll {
        # DLL path: <root>/cli2yaml/20260827.1.0/tools/CLI2Yaml.dll — four Split-Parents up to <root>.
        $probe = Split-Path (Split-Path (Split-Path (Split-Path $script:FakeCli2YamlDll -Parent) -Parent) -Parent) -Parent
        if (Test-Path $probe) { Remove-Item $probe -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'builds a dry-run manifest with all expected keys' {
        $m = New-SyncManifest -Mode 'dry-run' -Moniker 'azure-cli-preview' -VersionFolder 'preview-version' `
            -SourceRepoUrl 'https://github.com/Azure/azclips' -SourceBranch 'main' `
            -OutputLocation $script:Output -Cli2YamlDll $script:FakeCli2YamlDll

        $m.mode              | Should -Be 'dry-run'
        $m.moniker           | Should -Be 'azure-cli-preview'
        $m.versionFolder     | Should -Be 'preview-version'
        $m.sourceRepoUrl     | Should -Be 'https://github.com/Azure/azclips'
        $m.sourceBranch      | Should -Be 'main'
        $m.cli2yamlVersion   | Should -Be '20260827.1.0'
        $m.editLinkPrefix    | Should -Be 'https://github.com/Azure/azclips/blob/main/'
        $m.editLinkAllMatch  | Should -BeTrue
        $m.filesProduced     | Should -Be 2
        $m.ymlProduced       | Should -Be 2
        $m.topLevelFiles     | Should -Contain 'vm.yml'
    }

    It 'flags editLinkAllMatch=false when the prefix does not apply' {
        Set-Content -Path (Join-Path $script:Output 'latest/wrong.yml') -Value 'editLink: https://github.com/Azure/azure-cli/blob/dev/x.md'

        $m = New-SyncManifest -Mode 'dry-run' -Moniker 'azure-cli-preview' -VersionFolder 'preview-version' `
            -SourceRepoUrl 'https://github.com/Azure/azclips' -SourceBranch 'main' `
            -OutputLocation $script:Output -Cli2YamlDll $script:FakeCli2YamlDll

        $m.editLinkAllMatch | Should -BeFalse
    }

    It 'includes branch/commit fields when Mode=force and ApplyResult is supplied' {
        $apply = [PSCustomObject]@{
            Branch        = 'azclips-cli-sync-202609030200'
            CommitSha     = 'abc123def456'
            CommitMessage = 'AzClips-CLI docs sync -- azure-cli-preview -- 202609030200'
            FileCount     = 20
            NoChanges     = $false
        }

        $m = New-SyncManifest -Mode 'force' -Moniker 'azure-cli-preview' -VersionFolder 'preview-version' `
            -SourceRepoUrl 'https://github.com/Azure/azclips' -SourceBranch 'main' `
            -OutputLocation $script:Output -Cli2YamlDll $script:FakeCli2YamlDll `
            -ApplyResult $apply

        $m.mode            | Should -Be 'force'
        $m.branch          | Should -Be 'azclips-cli-sync-202609030200'
        $m.commitSha       | Should -Be 'abc123def456'
        $m.commitFileCount | Should -Be 20
        $m.noChanges       | Should -BeFalse
    }
}

Describe 'Write-SyncManifest' {
    BeforeEach {
        $script:File = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-manifest-" + [Guid]::NewGuid().ToString('N') + '.json')
    }

    AfterEach {
        if (Test-Path $script:File) { Remove-Item $script:File -Force -ErrorAction SilentlyContinue }
    }

    It 'writes JSON that parses back to the original values' {
        $manifest = [PSCustomObject]@{
            mode          = 'dry-run'
            moniker       = 'azure-cli-preview'
            filesProduced = 20
            topLevelFiles = @('vm.yml', 'config.yml')
        }

        Write-SyncManifest -Path $script:File -Manifest $manifest

        Test-Path $script:File | Should -BeTrue
        $parsed = Get-Content $script:File -Raw | ConvertFrom-Json
        $parsed.mode          | Should -Be 'dry-run'
        $parsed.filesProduced | Should -Be 20
        $parsed.topLevelFiles | Should -Be @('vm.yml', 'config.yml')
    }

    It 'creates the parent directory if it does not exist' {
        $nested = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-newdir-" + [Guid]::NewGuid().ToString('N') + '/deep/manifest.json')
        try {
            $manifest = [PSCustomObject]@{ mode = 'dry-run' }
            Write-SyncManifest -Path $nested -Manifest $manifest
            Test-Path $nested | Should -BeTrue
        }
        finally {
            $probe = Split-Path (Split-Path $nested -Parent) -Parent
            if (Test-Path $probe) { Remove-Item $probe -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }
}

Describe 'Remove-ServicePageOutput' {
    BeforeEach {
        $script:VersionFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-sp-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:VersionFolder 'latest/service-page') -Force | Out-Null
        Set-Content -Path (Join-Path $script:VersionFolder 'latest/service-page/azure-cli.yml') -Value 'x'
        Set-Content -Path (Join-Path $script:VersionFolder 'latest/vm.yml') -Value 'y'
    }

    AfterEach {
        if (Test-Path $script:VersionFolder) { Remove-Item $script:VersionFolder -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'removes the service-page directory but leaves peer files intact' {
        Remove-ServicePageOutput -OutputVersionFolder $script:VersionFolder

        Test-Path (Join-Path $script:VersionFolder 'latest/service-page') | Should -BeFalse
        Test-Path (Join-Path $script:VersionFolder 'latest/vm.yml')       | Should -BeTrue
    }

    It 'is a no-op when the service-page directory does not exist' {
        Remove-Item (Join-Path $script:VersionFolder 'latest/service-page') -Recurse -Force

        { Remove-ServicePageOutput -OutputVersionFolder $script:VersionFolder } | Should -Not -Throw
        Test-Path (Join-Path $script:VersionFolder 'latest/vm.yml') | Should -BeTrue
    }
}

Describe 'Write-CommandGroupToc' {
    BeforeEach {
        $script:VersionFolder = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-clisync-toc-" + [Guid]::NewGuid().ToString('N'))
        $script:Latest = Join-Path $script:VersionFolder 'latest'
        New-Item -ItemType Directory -Path (Join-Path $script:Latest 'agent') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $script:Latest 'msgraph/group') -Force | Out-Null

        function script:New-GroupYaml([string]$RelPath, [string]$Uid, [string]$Name)
        {
            $content = "### YamlMime:AzureCLIGroup`nuid: $Uid`nname: $Name`nsummary: |-`n  s`n"
            [IO.File]::WriteAllText((Join-Path $script:Latest $RelPath), $content)
        }

        New-GroupYaml 'reference-index.yml'          'az'                      'az'
        New-GroupYaml 'vm.yml'                       'az_vm'                   'az vm'
        New-GroupYaml 'config.yml'                   'az_config'               'az config'
        New-GroupYaml 'agent.yml'                    'az_agent'                'az agent'
        New-GroupYaml 'agent/tool.yml'               'az_agent_tool'           'az agent tool'
        New-GroupYaml 'msgraph.yml'                  'az_msgraph'              'az msgraph'
        New-GroupYaml 'msgraph/group.yml'            'az_msgraph_group'        'az msgraph group'
        New-GroupYaml 'msgraph/group/member.yml'     'az_msgraph_group_member' 'az msgraph group member'
        [IO.File]::WriteAllText((Join-Path $script:Latest 'TOC.yml'), "- name: CLI reference`n  items:`n  - name: azure-cli`n    uid: sp-azure-cli`n")
    }

    AfterEach {
        if (Test-Path $script:VersionFolder) { Remove-Item $script:VersionFolder -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'replaces the service-page TOC with a nested command-group tree' {
        Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder

        $expected = @(
            '- name: CLI reference'
            '  displayName: CLI reference'
            '  items:'
            '  - name: List A - Z'
            '    uid: az'
            '    displayName: List A - Z'
            '  - name: agent'
            '    items:'
            '    - name: Commands'
            '      uid: az_agent'
            '      displayName: az agent'
            '    - name: tool'
            '      uid: az_agent_tool'
            '      displayName: az agent tool'
            '      no-loc:'
            '      - tool'
            '    no-loc:'
            '    - agent'
            '  - name: config'
            '    uid: az_config'
            '    displayName: az config'
            '    no-loc:'
            '    - config'
            '  - name: msgraph'
            '    items:'
            '    - name: Commands'
            '      uid: az_msgraph'
            '      displayName: az msgraph'
            '    - name: group'
            '      items:'
            '      - name: Commands'
            '        uid: az_msgraph_group'
            '        displayName: az msgraph group'
            '      - name: member'
            '        uid: az_msgraph_group_member'
            '        displayName: az msgraph group member'
            '        no-loc:'
            '        - member'
            '      no-loc:'
            '      - group'
            '    no-loc:'
            '    - msgraph'
            '  - name: vm'
            '    uid: az_vm'
            '    displayName: az vm'
            '    no-loc:'
            '    - vm'
        ) -join "`n"

        [IO.File]::ReadAllText((Join-Path $script:Latest 'TOC.yml')) | Should -BeExactly ($expected + "`n")
    }

    It 'never references the stripped service page' {
        Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder

        [IO.File]::ReadAllText((Join-Path $script:Latest 'TOC.yml')) | Should -Not -Match 'sp-azure-cli'
    }

    It 'includes root commands alphabetically without treating nested parameters as commands' {
        $root = @'
### YamlMime:AzureCLIGroup
uid: az
name: az
directCommands:
- uid: az_login
  name: az login
  parameters:
  - name: --tenant
- uid: az_logout
  name: az logout
- uid: az_rest
  name: az rest
- uid: az_survey
  name: az survey
commands:
- az_config
- az_login
'@
        [IO.File]::WriteAllText((Join-Path $script:Latest 'reference-index.yml'), $root)

        Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder

        $toc = [IO.File]::ReadAllText((Join-Path $script:Latest 'TOC.yml'))
        foreach ($command in 'login', 'logout', 'rest', 'survey') {
            $toc | Should -MatchExactly "  - name: $command\n    uid: az_$command\n    displayName: az $command"
            [regex]::Matches($toc, "uid: az_$command\b").Count | Should -Be 1
        }
        $toc.IndexOf('name: config') | Should -BeLessThan $toc.IndexOf('name: login')
        $toc.IndexOf('name: logout') | Should -BeLessThan $toc.IndexOf('name: msgraph')
        $toc | Should -Not -Match 'name: --tenant'
    }

    It 'rejects malformed root command names' {
        [IO.File]::WriteAllText((Join-Path $script:Latest 'reference-index.yml'), "uid: az`nname: az`ndirectCommands:`n- uid: az_login`n")
        { Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder } | Should -Throw '*has no valid name*'
    }

    It 'rejects a root command that conflicts with a group' {
        [IO.File]::WriteAllText((Join-Path $script:Latest 'reference-index.yml'), "uid: az`nname: az`ndirectCommands:`n- uid: az_config`n  name: az config`n")
        { Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder } | Should -Throw '*conflicts*'
    }

    It 'preserves CRLF line endings from the CLI2Yaml TOC' {
        [IO.File]::WriteAllText((Join-Path $script:Latest 'TOC.yml'), "- name: CLI reference`r`n")

        Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder

        $text = [IO.File]::ReadAllText((Join-Path $script:Latest 'TOC.yml'))
        $text | Should -Match "`r`n"
        ($text -replace "`r`n", '') | Should -Not -Match "`n"
    }

    It 'writes UTF-8 without a BOM' {
        Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder

        $bytes = [IO.File]::ReadAllBytes((Join-Path $script:Latest 'TOC.yml'))
        $bytes[0] | Should -Not -Be 0xEF
    }

    It 'throws when a generated file has no uid' {
        [IO.File]::WriteAllText((Join-Path $script:Latest 'broken.yml'), "### YamlMime:AzureCLIGroup`nname: az broken`n")

        { Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder } | Should -Throw '*has no top-level uid*'
    }

    It 'throws when the latest folder is missing' {
        Remove-Item $script:Latest -Recurse -Force

        { Write-CommandGroupToc -OutputVersionFolder $script:VersionFolder } | Should -Throw '*does not exist*'
    }
}