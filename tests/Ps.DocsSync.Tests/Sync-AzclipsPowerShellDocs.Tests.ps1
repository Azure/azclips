# Pester tests for build/Sync-AzclipsPowerShellDocs.ps1.
#
# Run: Invoke-Pester -Path tests/Ps.DocsSync.Tests/

BeforeAll {
    $env:AZCLIPS_PS_SYNC_LOAD_ONLY = '1'
    . "$PSScriptRoot/../../build/Sync-AzclipsPowerShellDocs.ps1"
    $env:AZCLIPS_PS_SYNC_LOAD_ONLY = ''
}

Describe 'Get-PublishableModule' {
    BeforeAll {
        $script:TempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-pssync-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:TempRoot | Out-Null

        # Module with both .psd1 and populated help/
        New-Item -ItemType Directory -Path (Join-Path $script:TempRoot 'Alpha') | Out-Null
        Set-Content -Path (Join-Path $script:TempRoot 'Alpha/Alpha.psd1') -Value "@{ ModuleVersion = '1.0'; GUID = '11111111-1111-1111-1111-111111111111'; Description = 'A' }"
        New-Item -ItemType Directory -Path (Join-Path $script:TempRoot 'Alpha/help') | Out-Null
        Set-Content -Path (Join-Path $script:TempRoot 'Alpha/help/Get-Alpha.md') -Value "## SYNOPSIS`nAlpha thing.`n"

        # Module with .psd1 but no help/ (rollup pattern like Az)
        New-Item -ItemType Directory -Path (Join-Path $script:TempRoot 'Beta') | Out-Null
        Set-Content -Path (Join-Path $script:TempRoot 'Beta/Beta.psd1') -Value "@{ ModuleVersion = '1.0'; GUID = '22222222-2222-2222-2222-222222222222' }"

        # Shared library folder with no .psd1 at all (Common pattern)
        New-Item -ItemType Directory -Path (Join-Path $script:TempRoot 'Shared') | Out-Null
        Set-Content -Path (Join-Path $script:TempRoot 'Shared/lib.cs') -Value "// code"

        # Module with .psd1 AND empty help/ folder
        New-Item -ItemType Directory -Path (Join-Path $script:TempRoot 'Gamma') | Out-Null
        Set-Content -Path (Join-Path $script:TempRoot 'Gamma/Gamma.psd1') -Value "@{ ModuleVersion = '1.0' }"
        New-Item -ItemType Directory -Path (Join-Path $script:TempRoot 'Gamma/help') | Out-Null
    }

    AfterAll {
        if (Test-Path $script:TempRoot) { Remove-Item $script:TempRoot -Recurse -Force }
    }

    It 'returns only modules with both .psd1 and non-empty help/' {
        $result = @(Get-PublishableModule -PsRoot $script:TempRoot)
        $result.Count | Should -Be 1
        $result[0].Name | Should -Be 'Alpha'
    }

    It 'exposes HelpFiles as an array of FileInfo' {
        $result = @(Get-PublishableModule -PsRoot $script:TempRoot)
        $result[0].HelpFiles.Count | Should -Be 1
        $result[0].HelpFiles[0].Name | Should -Be 'Get-Alpha.md'
    }

    It 'throws when the source root is missing' {
        { Get-PublishableModule -PsRoot (Join-Path $script:TempRoot 'nonexistent') } | Should -Throw
    }

    It 'discovers all publishable modules against the real src/PS tree' {
        $repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
        $result = @(Get-PublishableModule -PsRoot (Join-Path $repoRoot 'src/PS'))
        ($result | ForEach-Object Name) | Sort-Object | Should -Be @('Accounts', 'Compute', 'Config', 'Identity', 'Login', 'MsGraph', 'Resources', 'Storage')
    }
}

Describe 'Get-CmdletSynopsis' {
    BeforeAll {
        $script:TempFile = New-TemporaryFile
    }

    AfterAll {
        Remove-Item $script:TempFile -ErrorAction SilentlyContinue
    }

    It 'returns the first non-empty line under ## SYNOPSIS' {
        Set-Content -Path $script:TempFile -Value "# Get-Foo`n`n## SYNOPSIS`n`nDoes a thing.`n`n## SYNTAX"
        Get-CmdletSynopsis -Path $script:TempFile | Should -Be 'Does a thing.'
    }

    It 'returns empty string when SYNOPSIS is missing' {
        Set-Content -Path $script:TempFile -Value "# Get-Foo`n`n## DESCRIPTION`n`nStuff.`n"
        Get-CmdletSynopsis -Path $script:TempFile | Should -Be ''
    }

    It 'stops at the next ## heading' {
        Set-Content -Path $script:TempFile -Value "## SYNOPSIS`nFirst line.`n## NEXT"
        Get-CmdletSynopsis -Path $script:TempFile | Should -Be 'First line.'
    }
}

Describe 'Resolve-TargetModuleName' {
    It 'substitutes {ModuleName} with the source name' {
        Resolve-TargetModuleName -SourceName 'Compute' -Pattern 'Az.{ModuleName}' | Should -Be 'Az.Compute'
    }

    It 'passes through with the identity pattern' {
        Resolve-TargetModuleName -SourceName 'Compute' -Pattern '{ModuleName}' | Should -Be 'Compute'
    }

    It 'supports non-Az prefixes' {
        Resolve-TargetModuleName -SourceName 'Compute' -Pattern 'AzClips.{ModuleName}' | Should -Be 'AzClips.Compute'
    }
}

Describe 'New-SyncPlan' {
    BeforeAll {
        # Use a relative fake path — an absolute Windows-style path like 'C:/fake/target'
        # makes Join-Path invoke drive resolution on Linux, which throws DriveNotFoundException.
        $repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
        $script:Modules = @(Get-PublishableModule -PsRoot (Join-Path $repoRoot 'src/PS'))
        $script:Config = Get-Content -Raw (Join-Path $repoRoot 'build/ps-docs-sync.config.json') | ConvertFrom-Json
        $script:Plan = @(New-SyncPlan -Modules $script:Modules -Moniker 'test-moniker' `
            -TargetRepoPath 'fake-target' -TargetModuleNamePattern 'Az.{ModuleName}' `
            -Config $script:Config -RepoRoot $repoRoot)
    }

    It 'emits one entry per cmdlet plus one module-index per module' {
        $cmdletCount = ($script:Modules | ForEach-Object { $_.HelpFiles.Count } | Measure-Object -Sum).Sum
        $expected = $cmdletCount + $script:Modules.Count
        $script:Plan.Count | Should -Be $expected
    }

    It 'tags per-cmdlet entries as CmdletMarkdown' {
        $cmdletEntries = @($script:Plan | Where-Object Kind -eq 'CmdletMarkdown')
        $cmdletEntries.Count | Should -Be ($script:Modules | ForEach-Object { $_.HelpFiles.Count } | Measure-Object -Sum).Sum
    }

    It 'tags generated indexes as ModuleIndex — one per publishable module' {
        $indexEntries = @($script:Plan | Where-Object Kind -eq 'ModuleIndex')
        $indexEntries.Count | Should -Be $script:Modules.Count
    }

    It 'places each entry under the moniker/Az.SourceName folder' {
        $sample = $script:Plan | Where-Object ModuleName -eq 'Compute' | Select-Object -First 1
        $sample.TargetPath | Should -Match 'test-moniker[\\/]Az\.Compute[\\/]'
    }
}

Describe 'New-ModuleIndexContent' {
    BeforeAll {
        $repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
        $modules = @(Get-PublishableModule -PsRoot (Join-Path $repoRoot 'src/PS'))
        $script:Compute = $modules | Where-Object Name -eq 'Compute'
        $script:Config = Get-Content -Raw (Join-Path $repoRoot 'build/ps-docs-sync.config.json') | ConvertFrom-Json
        $script:Manifest = Read-ModuleManifest -Path $script:Compute.Psd1Path
        $script:Body = New-ModuleIndexContent -Module $script:Compute -TargetModuleName 'Az.Compute' `
            -Manifest $script:Manifest -Config $script:Config `
            -RepoRootRelativeHelpDir 'src/PS/Compute/help'
    }

    It 'emits the frontmatter fields docs.ms expects' {
        $script:Body | Should -Match 'Module Name: Az\.Compute'
        $script:Body | Should -Match 'Module Guid: '
        $script:Body | Should -Match 'Download Help Link: https://learn\.microsoft\.com/powershell/module/az\.compute'
        $script:Body | Should -Match 'Locale: en-US'
        $script:Body | Should -Match 'content_git_url:.+/tree/[^/\s]+/src/PS/Compute/help(\r?\n|$)'
    }

    It 'includes an H1 module heading' {
        $script:Body | Should -Match '# Az\.Compute Module'
    }

    It 'lists every cmdlet with a link and its SYNOPSIS' {
        foreach ($cmdlet in @('Get-AzVM', 'New-AzVM', 'Remove-AzVM')) {
            $script:Body | Should -Match "\[$cmdlet\]\($cmdlet\.md\)"
        }
    }

    It 'falls back to a placeholder description when the psd1 has none' {
        $emptyManifest = [PSCustomObject]@{ ModuleVersion = '0'; Guid = '00000000-0000-0000-0000-000000000000'; Description = '' }
        $body = New-ModuleIndexContent -Module $script:Compute -TargetModuleName 'Az.Compute' `
            -Manifest $emptyManifest -Config $script:Config -RepoRootRelativeHelpDir 'src/PS/Compute/help'
        $body | Should -Match 'TODO: fill in Az\.Compute module description\.'
    }
}

Describe 'Test-TargetReady' {
    BeforeEach {
        # Fresh temp git repo with a moniker folder pre-seeded (simulates docs.ms desk-ticket onboarding done).
        $script:Target = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-pssync-target-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Target | Out-Null
        git -C $script:Target init --quiet
        git -C $script:Target config user.email 'test@example.com'
        git -C $script:Target config user.name 'Test'
        git -C $script:Target commit --allow-empty -m 'init' --quiet
    }

    AfterEach {
        if (Test-Path $script:Target) { Remove-Item $script:Target -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'reports no problems when target is a clean git repo with the moniker folder' {
        New-Item -ItemType Directory -Path (Join-Path $script:Target 'azclipsps-test') | Out-Null
        (Test-TargetReady -TargetRepoPath $script:Target -Moniker 'azclipsps-test').Count | Should -Be 0
    }

    It 'flags a missing target directory' {
        $missing = Join-Path $script:Target 'nowhere'
        $problems = @(Test-TargetReady -TargetRepoPath $missing -Moniker 'x')
        $problems.Count | Should -Be 1
        $problems[0] | Should -Match 'does not exist'
    }

    It 'flags a non-git target' {
        $notGit = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-notgit-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $notGit | Out-Null
        try {
            $problems = @(Test-TargetReady -TargetRepoPath $notGit -Moniker 'x')
            $problems.Count | Should -Be 1
            $problems[0] | Should -Match 'not a git repository'
        }
        finally {
            Remove-Item $notGit -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'flags a dirty working tree' {
        New-Item -ItemType Directory -Path (Join-Path $script:Target 'azclipsps-test') | Out-Null
        Set-Content -Path (Join-Path $script:Target 'stray.txt') -Value 'uncommitted'
        $problems = @(Test-TargetReady -TargetRepoPath $script:Target -Moniker 'azclipsps-test')
        ($problems -join "`n") | Should -Match 'working tree is dirty'
    }

    It 'flags a missing moniker folder' {
        $problems = @(Test-TargetReady -TargetRepoPath $script:Target -Moniker 'never-created')
        ($problems -join "`n") | Should -Match "moniker folder 'never-created' does not exist"
    }
}

Describe 'Invoke-SyncApply' {
    BeforeEach {
        $script:Target = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-pssync-apply-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:Target | Out-Null
        git -C $script:Target init --initial-branch=main --quiet
        git -C $script:Target config user.email 'test@example.com'
        git -C $script:Target config user.name 'Test'
        git -C $script:Target commit --allow-empty -m 'init' --quiet
        New-Item -ItemType Directory -Path (Join-Path $script:Target 'azclipsps-test') | Out-Null

        # Fabricate a source module + a plan pointing at the fresh target.
        $script:SrcRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-pssync-src-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:SrcRoot 'Foo/help') -Force | Out-Null
        Set-Content -Path (Join-Path $script:SrcRoot 'Foo/Foo.psd1') -Value "@{ ModuleVersion = '1.0'; GUID = 'ffffffff-1111-2222-3333-444444444444'; Description = 'Foo module' }"
        Set-Content -Path (Join-Path $script:SrcRoot 'Foo/help/Get-Foo.md') -Value "## SYNOPSIS`nGets a foo.`n"
        Set-Content -Path (Join-Path $script:SrcRoot 'Foo/help/Set-Foo.md') -Value "## SYNOPSIS`nSets a foo.`n"

        $modules = @(Get-PublishableModule -PsRoot $script:SrcRoot)
        $cfg = [PSCustomObject]@{
            moniker              = 'azclipsps-test'
            sourceRepoUrl        = 'https://github.com/Azure/azclips'
            sourceBranch         = 'main'
            downloadHelpLinkBase = 'https://learn.microsoft.com/powershell/module'
        }
        $script:Plan = @(New-SyncPlan -Modules $modules -Moniker 'azclipsps-test' `
            -TargetRepoPath $script:Target -TargetModuleNamePattern 'Az.{ModuleName}' `
            -Config $cfg -RepoRoot $script:SrcRoot)
    }

    AfterEach {
        if (Test-Path $script:Target) { Remove-Item $script:Target -Recurse -Force -ErrorAction SilentlyContinue }
        if (Test-Path $script:SrcRoot) { Remove-Item $script:SrcRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'creates the timestamped branch, writes every plan file, and returns a summary' {
        $result = Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201200'

        $result.Branch | Should -Be 'azclips-ps-sync-202608201200'
        $result.CommitMessage | Should -Match 'AzClips-PS docs sync -- azclipsps-test -- 202608201200'
        $result.FileCount | Should -Be 3
        $result.CommitSha | Should -Match '^[0-9a-f]{7,}$'

        Test-Path (Join-Path $script:Target 'azclipsps-test/Az.Foo/Get-Foo.md') | Should -Be $true
        Test-Path (Join-Path $script:Target 'azclipsps-test/Az.Foo/Set-Foo.md') | Should -Be $true
        Test-Path (Join-Path $script:Target 'azclipsps-test/Az.Foo/Az.Foo.md') | Should -Be $true

        $currentBranch = (git -C $script:Target rev-parse --abbrev-ref HEAD).Trim()
        $currentBranch | Should -Be 'azclips-ps-sync-202608201200'
    }

    It 'produces a clean working tree after commit' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201201' | Out-Null
        $status = "$(git -C $script:Target status --porcelain)"
        $status.Trim() | Should -Be ''
    }

    It 'commits every synced file in a single commit' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201202' | Out-Null
        $changedFiles = @(git -C $script:Target diff --name-only HEAD^..HEAD)
        $changedFiles.Count | Should -Be 3
    }

    It 'copies cmdlet markdown content-equivalent to source' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201203' | Out-Null
        $sourceContent = Get-Content -Raw (Join-Path $script:SrcRoot 'Foo/help/Get-Foo.md')
        $targetContent = Get-Content -Raw (Join-Path $script:Target 'azclipsps-test/Az.Foo/Get-Foo.md')
        $targetContent | Should -Be $sourceContent
    }

    It 'materializes the generated module-index with expected frontmatter and cmdlet links' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201204' | Out-Null
        $indexContent = Get-Content -Raw (Join-Path $script:Target 'azclipsps-test/Az.Foo/Az.Foo.md')
        $indexContent | Should -Match 'Module Name: Az\.Foo'
        $indexContent | Should -Match '\[Get-Foo\]\(Get-Foo\.md\)'
        $indexContent | Should -Match '\[Set-Foo\]\(Set-Foo\.md\)'
        $indexContent | Should -Match 'Gets a foo'
        $indexContent | Should -Match 'Sets a foo'
    }

    It 'refuses to run when the target branch name already exists' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201205' | Out-Null
        git -C $script:Target checkout main --quiet 2>&1 | Out-Null

        { Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201205' } |
            Should -Throw -ExpectedMessage "*already exists*"
    }
}
