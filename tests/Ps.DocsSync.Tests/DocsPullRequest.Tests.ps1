BeforeAll {
    . "$PSScriptRoot/../../build/Invoke-DocsPullRequest.ps1"
    $script:SourceRoot = (Resolve-Path "$PSScriptRoot/../..").Path
}

# Linux fixture cloning needs investigation before these tests can run in CI.
Describe 'Docs pull request workflow' -Skip:$IsLinux {
    BeforeEach {
        $script:Root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $work = Join-Path $script:Root 'work'
        $script:Upstream = Join-Path $script:Root 'upstream.git'
        $script:Fork = Join-Path $script:Root 'fork.git'
        New-Item -ItemType Directory -Path (Join-Path $work 'azps-preview'), (Join-Path $work '.github'), (Join-Path $work 'docs-ref-autogen/preview-version/latest') -Force | Out-Null
        git -C $work init --initial-branch=review --quiet
        git -C $work config user.name 'Test'
        git -C $work config user.email 'test@example.com'
        Set-Content -LiteralPath (Join-Path $work 'azps-preview/keep.txt') -Value 'base'
        Set-Content -LiteralPath (Join-Path $work 'docs-ref-autogen/preview-version/latest/old.yml') -Value 'old'
        Set-Content -LiteralPath (Join-Path $work '.github/PULL_REQUEST_TEMPLATE.md') -Value "# PR Summary`n`n## PR Checklist`n`n- [ ] Review"
        git -C $work add --all
        git -C $work commit -m 'base' --quiet
        git clone --bare --quiet $work $script:Upstream
        git clone --bare --quiet $work $script:Fork
        $script:BaseCommit = (git -C $work rev-parse HEAD).Trim()

        $script:SavedGitEnv = @{}
        foreach ($name in 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0', 'GIT_CONFIG_KEY_1', 'GIT_CONFIG_VALUE_1') {
            $script:SavedGitEnv[$name] = [Environment]::GetEnvironmentVariable($name)
        }
        $env:GIT_CONFIG_COUNT = '2'
        $env:GIT_CONFIG_KEY_0 = "url.$(([uri]$script:Upstream).AbsoluteUri).insteadOf"
        $env:GIT_CONFIG_VALUE_0 = 'https://github.com/base/docs.git'
        $env:GIT_CONFIG_KEY_1 = "url.$(([uri]$script:Fork).AbsoluteUri).insteadOf"
        $env:GIT_CONFIG_VALUE_1 = 'https://github.com/fork/docs.git'
        Mock gh { $global:DocsPrArgs = @($args); $global:LASTEXITCODE = 0; 'https://example.invalid/pr/1' }
    }

    AfterEach {
        foreach ($name in $script:SavedGitEnv.Keys) {
            [Environment]::SetEnvironmentVariable($name, $script:SavedGitEnv[$name])
        }
        Remove-Variable -Name DocsPrArgs -Scope Global -ErrorAction SilentlyContinue
    }

    It 'generates from the selected base, pushes a fork branch, and opens a draft PR' {
        $syncScript = Join-Path $script:SourceRoot 'build/Sync-AzclipsPowerShellDocs.ps1'
        & $syncScript -Push -TargetRepository 'base/docs' -HeadRepository 'fork/docs' -TargetBranch 'review' `
            -Module Compute -Timestamp 'fixture' -CommitAuthor 'Test' -CommitEmail 'test@example.com'

        $branch = 'azclips-ps-sync-fixture'
        (git --git-dir $script:Fork rev-parse "refs/heads/$branch^").Trim() | Should -Be $script:BaseCommit
        Should -Invoke gh -Times 1 -Exactly -ParameterFilter {
            $args -contains '--draft' -and $args -contains 'base/docs' -and
            $args -contains 'review' -and $args -contains 'fork:azclips-ps-sync-fixture' -and
            ($args | Where-Object { $_ -match '(?s)# PR Summary.*current checkout.*## PR Checklist.*- \[ \] Review' })
        }
    }

    It 'does not push or create a PR when sync produces no changes' {
        Invoke-DocsPullRequest -TargetRepository 'base/docs' -HeadRepository 'fork/docs' -TargetBranch 'review' `
            -SourceRoot $script:SourceRoot -BranchPrefix 'azclips-test-' -Title 'Test' -Sync { param($path, $stamp) }
        Should -Invoke gh -Times 0
        $branches = @(git --git-dir $script:Fork for-each-ref --format='%(refname:short)' refs/heads)
        $branches | Should -Be @('review')
    }

    It 'creates a PR from a selected tag without switching the source checkout' {
        $source = Join-Path $script:Root 'tag-source'
        $build = Join-Path $source 'build'
        $module = Join-Path $source 'src/PS/Compute'
        New-Item -ItemType Directory -Path $build, (Join-Path $module 'help') -Force | Out-Null
        foreach ($file in @('Sync-AzclipsPowerShellDocs.ps1', 'Test-DocsReleaseTag.ps1', 'Set-DocsTargetBranch.ps1', 'Invoke-DocsPullRequest.ps1', 'Invoke-DocsReleaseCheckout.ps1', 'Get-AzPackageName.ps1', 'ps-docs-sync.config.json')) {
            Copy-Item -LiteralPath (Join-Path $script:SourceRoot "build/$file") -Destination $build
        }
        Copy-Item -LiteralPath (Join-Path $script:SourceRoot 'src/PS/Compute/Compute.psd1') -Destination $module
        Copy-Item -LiteralPath (Join-Path $script:SourceRoot 'src/PS/Compute/help/Get-AzVM.md') -Destination (Join-Path $module 'help')
        git -C $source init --initial-branch=main --quiet
        git -C $source config user.name 'Test'
        git -C $source config user.email 'test@example.com'
        git -C $source add --all
        git -C $source commit -m 'release' --quiet
        git -C $source tag v1
        $tagCommit = (git -C $source rev-parse HEAD).Trim()
        git -C $source commit --allow-empty -m 'later' --quiet
        $original = (git -C $source rev-parse HEAD).Trim()

        & (Join-Path $build 'Sync-AzclipsPowerShellDocs.ps1') -Push -ReleaseTag v1 -TargetRepository 'base/docs' `
            -HeadRepository 'fork/docs' -TargetBranch review -Module Compute -Timestamp 'tag-fixture' `
            -CommitAuthor Test -CommitEmail 'test@example.com'

        (git -C $source rev-parse HEAD).Trim() | Should -Be $original
        (git --git-dir $script:Fork rev-parse 'refs/heads/azclips-ps-sync-tag-fixture^').Trim() | Should -Be $script:BaseCommit
        Should -Invoke gh -Times 1 -Exactly
        $global:DocsPrArgs | Should -Contain 'fork:azclips-ps-sync-tag-fixture'
        ($global:DocsPrArgs | Where-Object { $_.Contains('tag `v1`') -and $_.Contains($tagCommit) }) | Should -Not -BeNullOrEmpty
    }

    It 'runs the CLI PR entry point with a Release build and the selected docs base' {
        $script:CliSource = Join-Path $script:Root 'cli-source'
        $build = Join-Path $script:CliSource 'build'
        $docs = Join-Path $script:CliSource 'src/CLI/Help/Docs'
        $cache = Join-Path $script:Root 'nuget'
        New-Item -ItemType Directory -Path $build, $docs, (Join-Path $script:CliSource 'tools'), (Join-Path $cache 'cli2yaml/1.0/tools') -Force | Out-Null
        foreach ($file in @('Sync-AzClipsCliDocs.ps1', 'Test-DocsReleaseTag.ps1', 'Set-DocsTargetBranch.ps1', 'Invoke-DocsPullRequest.ps1', 'Invoke-DocsReleaseCheckout.ps1', 'Get-DocCommitMap.ps1')) {
            Copy-Item -LiteralPath (Join-Path $script:SourceRoot "build/$file") -Destination $build
        }
        @{
            moniker = 'azure-cli-preview'; versionFolder = 'preview-version'; targetRepoPath = '../unused'
            sourceRepoUrl = 'https://github.com/Azure/azclips'; sourceBranch = 'main'
            exportProject = 'tools/export.csproj'; docsReferenceConfig = 'src/CLI/Help/config.json'
            extensionIndex = 'src/CLI/Help/index.json'; serviceMapping = 'src/CLI/Help/service_name.json'
            cli2yamlPackage = 'CLI2Yaml'; cli2yamlFeed = 'https://example.invalid'
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $build 'cli-docs-sync.config.json')
        Set-Content -LiteralPath (Join-Path $script:CliSource 'tools/export.csproj') -Value 'fixture'
        Set-Content -LiteralPath (Join-Path $docs 'fixture.md') -Value 'fixture'
        foreach ($file in 'config.json', 'index.json', 'service_name.json') {
            Set-Content -LiteralPath (Join-Path (Split-Path $docs -Parent) $file) -Value '[]'
        }
        Set-Content -LiteralPath (Join-Path $cache 'cli2yaml/1.0/tools/CLI2Yaml.dll') -Value 'fixture'
        git -C $script:CliSource init --initial-branch=main --quiet
        git -C $script:CliSource config user.name 'Test'
        git -C $script:CliSource config user.email 'test@example.com'
        git -C $script:CliSource add --all
        git -C $script:CliSource commit -m 'source' --quiet

        $savedCache = $env:NUGET_PACKAGES
        $env:NUGET_PACKAGES = $cache
        $savedGitFunction = Get-Item function:global:git -ErrorAction SilentlyContinue
        $savedDotnetFunction = Get-Item function:global:dotnet -ErrorAction SilentlyContinue
        $global:DocsTestDotnetCalls = [System.Collections.Generic.List[object]]::new()
        Set-Item function:global:git -Value {
            if ($args -contains 'reset' -or $args -contains 'clean') { throw 'Unexpected destructive Git rollback in test.' }
            & (Get-Command git -CommandType Application | Select-Object -First 1).Source @args
        }
        Set-Item function:global:dotnet -Value {
            $global:DocsTestDotnetCalls.Add(@($args))
            $global:LASTEXITCODE = 0
            if ($args[0] -eq 'run') {
                $outputRoot = $args[[array]::IndexOf($args, '--output-folder') + 1]
                $latest = Join-Path $outputRoot 'latest'
                New-Item -ItemType Directory -Path $latest -Force | Out-Null
                Set-Content -LiteralPath (Join-Path $latest '_root_.json') -Value '[]'
                Set-Content -LiteralPath (Join-Path $latest 'group.json') -Value '[]'
            }
            elseif ($args[0] -ne 'build') {
                $out = $args[[array]::IndexOf($args, '-o') + 1]
                New-Item -ItemType Directory -Path (Join-Path $out 'latest') -Force | Out-Null
                Set-Content -LiteralPath (Join-Path $out 'latest/group.yml') -Value 'fixture: true'
            }
            'fixture output'
        }
        try {
            & (Join-Path $build 'Sync-AzClipsCliDocs.ps1') -Push -TargetRepository 'base/docs' -HeadRepository 'fork/docs' `
                -TargetBranch 'review' -Timestamp 'cli-fixture' -SkipServicePages:$false -CommitAuthor 'Test' -CommitEmail 'test@example.com'
            (git --git-dir $script:Fork rev-parse 'refs/heads/azclips-cli-sync-cli-fixture^').Trim() | Should -Be $script:BaseCommit
            @($global:DocsTestDotnetCalls | Where-Object {
                $_[0] -eq 'build' -and $_ -contains 'Release' -and $_ -contains '--no-incremental' -and
                @($_ | Where-Object { $_ -like '-p:RepoArtifacts=*build-artifacts*' }).Count -eq 1
            }).Count | Should -Be 1
            @($global:DocsTestDotnetCalls | Where-Object {
                $_[0] -eq 'run' -and @($_ | Where-Object { $_ -match 'build-artifacts[\\/]Release$' }).Count -eq 1
            }).Count | Should -Be 1
            Should -Invoke gh -Times 1 -Exactly -ParameterFilter { $args -contains 'fork:azclips-cli-sync-cli-fixture' }
        }
        finally {
            $env:NUGET_PACKAGES = $savedCache
            Remove-Item function:global:git, function:global:dotnet
            if ($savedGitFunction) { Set-Item function:global:git -Value $savedGitFunction.ScriptBlock }
            if ($savedDotnetFunction) { Set-Item function:global:dotnet -Value $savedDotnetFunction.ScriptBlock }
            Remove-Variable -Name DocsTestDotnetCalls -Scope Global
        }
    }
}
