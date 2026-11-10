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
        ($result | ForEach-Object Name) | Sort-Object | Should -Be @('Accounts', 'Compute', 'Config', 'KeyVault', 'ManagedServiceIdentity', 'MsGraph', 'Network', 'Resources', 'Storage')
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

Describe 'Get-AzPackageName' {
    It 'prefixes leaf modules with Az.' {
        Get-AzPackageName -SourceModule 'Compute' | Should -Be 'Az.Compute'
        Get-AzPackageName -SourceModule 'Config'  | Should -Be 'Az.Config'
        Get-AzPackageName -SourceModule 'Accounts' | Should -Be 'Az.Accounts'
    }

    It 'leaves the Az rollup unchanged' {
        Get-AzPackageName -SourceModule 'Az' | Should -Be 'Az'
    }
}

Describe 'Copy-CmdletMarkdownWithRewrite' {
    BeforeEach {
        $script:TmpRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-copyrewrite-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $script:TmpRoot -Force | Out-Null
    }

    AfterEach {
        if (Test-Path $script:TmpRoot) { Remove-Item $script:TmpRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'rewrites Module Name in frontmatter to the target module name' {
        $src = Join-Path $script:TmpRoot 'Set-AzConfig.md'
        $dst = Join-Path $script:TmpRoot 'target/Set-AzConfig.md'
        New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
        Set-Content -LiteralPath $src -Value @"
---
document type: cmdlet
Module Name: Config
PlatyPS schema version: 2024-05-01
title: Set-AzConfig
---

# Set-AzConfig

## SYNOPSIS
Whatever.
"@ -NoNewline

        Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName 'Az.Config'
        $written = Get-Content -LiteralPath $dst -Raw
        $written | Should -Match '(?m)^Module Name: Az\.Config\s*$'
        $written | Should -Not -Match '(?m)^Module Name: Config\s*$'
    }

    It 'leaves body content without an internal syntax heading untouched' {
        $src = Join-Path $script:TmpRoot 'Get-AzVM.md'
        $dst = Join-Path $script:TmpRoot 'target/Get-AzVM.md'
        New-Item -ItemType Directory -Path (Split-Path -Parent $dst) -Force | Out-Null
        Set-Content -LiteralPath $src -Value @"
---
Module Name: Compute
---

# Get-AzVM

## SYNOPSIS
Body content with "Module Name:" inline should NOT be touched.

## EXAMPLES

``````powershell
Module Name: fake in a code block
``````
"@ -NoNewline

        Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName 'Az.Compute'
        $written = Get-Content -LiteralPath $dst -Raw
        $written | Should -Match '(?m)^Module Name: Az\.Compute\s*$'
        $sourceBody = (Get-Content -LiteralPath $src -Raw) -split '# Get-AzVM', 2
        $targetBody = $written -split '# Get-AzVM', 2
        $targetBody[1] | Should -BeExactly $sourceBody[1]
    }

    It 'preserves <Name> line endings and UTF-8 body content' -ForEach @(
        @{ Name = 'LF'; Newline = "`n" }
        @{ Name = 'CRLF'; Newline = "`r`n" }
    ) {
        $src = Join-Path $script:TmpRoot 'Get-AzVM.md'
        $dst = Join-Path $script:TmpRoot 'output.md'
        $content = "---${Newline}Module Name: Compute${Newline}---${Newline}${Newline}Description: $([char]0x00E9)"
        Set-Content -LiteralPath $src -Value $content -NoNewline -Encoding utf8

        Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName 'Az.Compute'

        Get-Content -LiteralPath $dst -Raw | Should -BeExactly $content.Replace('Module Name: Compute', 'Module Name: Az.Compute')
    }

    It 'relabels only the unnamed syntax heading for Learn with <Name> line endings' -ForEach @(
        @{ Name = 'LF'; Newline = "`n" }
        @{ Name = 'CRLF'; Newline = "`r`n" }
    ) {
        $src = Join-Path $script:TmpRoot 'Set-AzConfig.md'
        $dst = Join-Path $script:TmpRoot 'output.md'
        $lines = @('---', 'Module Name: Config', '---', '# Set-AzConfig', '## SYNTAX',
            '', '### __AllParameterSets', '', '```', 'Set-AzConfig -Key <string>', '```',
            '## DESCRIPTION', 'Authored description.', '## EXAMPLES', '### Example 1', 'Set-AzConfig')
        $content = $lines -join $Newline
        Set-Content -LiteralPath $src -Value $content -NoNewline -Encoding utf8
        $sourceHash = (Get-FileHash -LiteralPath $src).Hash

        Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName 'Az.Config'

        $expected = $content.Replace('### __AllParameterSets', '### Default (Default)').
            Replace('Module Name: Config', 'Module Name: Az.Config')
        Get-Content -LiteralPath $dst -Raw | Should -BeExactly $expected
        (Get-FileHash -LiteralPath $src).Hash | Should -BeExactly $sourceHash
        Copy-CmdletMarkdownWithRewrite -SourcePath $dst -TargetPath $dst -TargetModuleName 'Az.Config'
        Get-Content -LiteralPath $dst -Raw | Should -BeExactly $expected
    }

    It 'preserves named/default sets, similar names and headings outside syntax or inside fences' {
        $src = Join-Path $script:TmpRoot 'Get-Fixture.md'
        $dst = Join-Path $script:TmpRoot 'output.md'
        $content = @'
---
Module Name: Fixture
---
## SYNTAX
### ByName (Default)
```powershell
### __AllParameterSets
## DESCRIPTION
```
### ById
~~~~
### __AllParameterSets
```
~~~
~~~~
### Default (Default)
### __AllParameterSetsCustom
## EXAMPLES
### __AllParameterSets
Example text.
## DESCRIPTION
### __AllParameterSets
Description text.
'@
        Set-Content -LiteralPath $src -Value $content -NoNewline
        Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName 'Az.Fixture'
        Get-Content -LiteralPath $dst -Raw |
            Should -BeExactly $content.Replace('Module Name: Fixture', 'Module Name: Az.Fixture')
    }

    It 'rejects <Name> without writing a target' -ForEach @(
        @{ Name = 'missing frontmatter'; Content = "# Get-Foo`nModule Name: Foo" }
        @{ Name = 'missing module name'; Content = "---`ntitle: Get-Foo`n---`n" }
        @{ Name = 'empty frontmatter with a body module name'; Content = "---`n---`nModule Name: Foo`n---`n" }
        @{ Name = 'duplicate module names'; Content = "---`nModule Name: Foo`nModule Name: Bar`n---`n" }
    ) {
        $src = Join-Path $script:TmpRoot 'Get-Foo.md'
        $dst = Join-Path $script:TmpRoot 'output.md'
        Set-Content -LiteralPath $src -Value $Content -NoNewline

        { Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName 'Az.Foo' } |
            Should -Throw "*exactly one 'Module Name:' field*"
        Test-Path -LiteralPath $dst | Should -BeFalse
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
            -TargetRepoPath 'fake-target' `
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

    It 'uses the current Accounts module name for authentication help' {
        $entries = @($script:Plan | Where-Object ModuleName -eq 'Accounts')
        $entries.Count | Should -BeGreaterThan 0
        foreach ($entry in $entries) {
            $entry.TargetModule | Should -Be 'Az.Accounts'
            $entry.TargetPath | Should -Match 'test-moniker[\\/]Az\.Accounts[\\/]'
        }
        @($script:Plan | Where-Object ModuleName -eq 'Login').Count | Should -Be 0
    }

    It 'rewrites all 78 Learn copies with only module naming and 44 internal-heading relabels' {
        $targetRoot = Join-Path $TestDrive 'normalized'
        New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null
        $entries = @($script:Plan | Where-Object Kind -eq 'CmdletMarkdown')
        $entries.Count | Should -Be 78
        $headingCount = 0
        foreach ($entry in $entries) {
            $sourceHash = (Get-FileHash -LiteralPath $entry.SourcePath).Hash
            $target = Join-Path $targetRoot "$($entry.TargetModule)-$([System.IO.Path]::GetFileName($entry.SourcePath))"
            Copy-CmdletMarkdownWithRewrite -SourcePath $entry.SourcePath -TargetPath $target -TargetModuleName $entry.TargetModule
            $source = Get-Content -LiteralPath $entry.SourcePath -Raw
            $headingCount += [regex]::Matches($source, '(?m)^### __AllParameterSets\r?$').Count
            $expected = $source.Replace("Module Name: $($entry.ModuleName)", "Module Name: $($entry.TargetModule)")
            $expected = $expected -creplace '(?m)^### __AllParameterSets(?=\r?$)', '### Default (Default)'
            Get-Content -LiteralPath $target -Raw | Should -BeExactly $expected
            (Get-FileHash -LiteralPath $entry.SourcePath).Hash | Should -BeExactly $sourceHash
        }
        $headingCount | Should -Be 44
    }
}

Describe 'Learn copy PlatyPS parsing compatibility' {
    BeforeAll {
        Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
    }

    It 'preserves parsed sections and syntax for <Cmdlet>' -ForEach @(
        @{ Cmdlet = 'Disconnect-AzAccount'; Module = 'Accounts'; Count = 1 }
        @{ Cmdlet = 'Set-AzConfig'; Module = 'Config'; Count = 1 }
        @{ Cmdlet = 'New-AzVM'; Module = 'Compute'; Count = 2 }
    ) {
        $src = Join-Path $PSScriptRoot "../../src/PS/$Module/help/$Cmdlet.md"
        $dst = Join-Path $TestDrive "$Cmdlet.md"
        Copy-CmdletMarkdownWithRewrite -SourcePath $src -TargetPath $dst -TargetModuleName "Az.$Module"
        $original = Import-MarkdownCommandHelp -LiteralPath $src
        $published = Import-MarkdownCommandHelp -LiteralPath $dst
        @($published.Syntax).Count | Should -Be $Count
        $published.Aliases | Should -BeNullOrEmpty
        $published.Description | Should -BeExactly $original.Description
        @($published.Examples).Count | Should -Be @($original.Examples).Count
        @($published.Parameters).Count | Should -Be @($original.Parameters).Count
        for ($i = 0; $i -lt $Count; $i++) {
            $expectedName = if ($original.Syntax[$i].ParameterSetName -eq '__AllParameterSets') {
                'Default'
            } else {
                $original.Syntax[$i].ParameterSetName
            }
            $published.Syntax[$i].ParameterSetName | Should -BeExactly $expectedName
            $expectedDefault = if ($original.Syntax[$i].ParameterSetName -eq '__AllParameterSets') {
                $true
            } else {
                $original.Syntax[$i].IsDefaultParameterSet
            }
            $published.Syntax[$i].IsDefaultParameterSet | Should -Be $expectedDefault
        }
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
        Set-Content -Path (Join-Path $script:SrcRoot 'Foo/help/Get-Foo.md') -Value "---`nModule Name: Foo`n---`n## SYNOPSIS`nGets a foo.`n"
        Set-Content -Path (Join-Path $script:SrcRoot 'Foo/help/Set-Foo.md') -Value "---`nModule Name: Foo`n---`n## SYNOPSIS`nSets a foo.`n"

        $modules = @(Get-PublishableModule -PsRoot $script:SrcRoot)
        $cfg = [PSCustomObject]@{
            moniker              = 'azclipsps-test'
            sourceRepoUrl        = 'https://github.com/Azure/azclips'
            sourceBranch         = 'main'
            downloadHelpLinkBase = 'https://learn.microsoft.com/powershell/module'
        }
        $script:Plan = @(New-SyncPlan -Modules $modules -Moniker 'azclipsps-test' `
            -TargetRepoPath $script:Target `
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

    It 'copies cmdlet markdown and rewrites Module Name to the packaged name' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201203' | Out-Null
        $sourceContent = Get-Content -Raw (Join-Path $script:SrcRoot 'Foo/help/Get-Foo.md')
        $targetContent = Get-Content -Raw (Join-Path $script:Target 'azclipsps-test/Az.Foo/Get-Foo.md')
        # Frontmatter Module Name rewrites from 'Foo' -> 'Az.Foo'; body is untouched.
        $sourceContent | Should -Match '(?m)^Module Name: Foo\s*$'
        $targetContent | Should -Match '(?m)^Module Name: Az\.Foo\s*$'
        $targetContent | Should -Not -Match '(?m)^Module Name: Foo\s*$'
        $targetContent | Should -Match 'Gets a foo\.'
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

    It 'removes stale module folders and cmdlet pages in the same commit' {
        $staleModule = Join-Path $script:Target 'azclipsps-test/Az.Old'
        New-Item -ItemType Directory -Path $staleModule | Out-Null
        Set-Content -Path (Join-Path $staleModule 'Get-Old.md') -Value 'old'
        Set-Content -Path (Join-Path $staleModule 'Az.Old.md') -Value 'old index'
        New-Item -ItemType Directory -Path (Join-Path $script:Target 'azclipsps-test/Az.Foo') | Out-Null
        Set-Content -Path (Join-Path $script:Target 'azclipsps-test/Az.Foo/Remove-Foo.md') -Value 'renamed away'
        git -C $script:Target add -A
        git -C $script:Target commit -m 'seed' --quiet

        $result = Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201206'

        @($result.RemovedFiles) | Sort-Object | Should -Be @(
            'azclipsps-test/Az.Foo/Remove-Foo.md'
            'azclipsps-test/Az.Old/Az.Old.md'
            'azclipsps-test/Az.Old/Get-Old.md'
        )
        Test-Path $staleModule | Should -Be $false
        $deleted = @(git -C $script:Target diff --name-only --diff-filter=D HEAD^..HEAD)
        $deleted.Count | Should -Be 3
        "$(git -C $script:Target status --porcelain)".Trim() | Should -Be ''
    }

    It 'leaves out-of-scope module folders alone on a -Module subset sync' {
        $other = Join-Path $script:Target 'azclipsps-test/Az.Other'
        New-Item -ItemType Directory -Path $other | Out-Null
        Set-Content -Path (Join-Path $other 'Get-Other.md') -Value 'other'
        git -C $script:Target add -A
        git -C $script:Target commit -m 'seed' --quiet

        $result = Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201207' `
            -ScopeTargetModules 'Az.Foo'

        @($result.RemovedFiles).Count | Should -Be 0
        Test-Path (Join-Path $other 'Get-Other.md') | Should -Be $true
    }

    It 'regenerates the moniker service map named in monikerMapping.json' {
        $mappingDir = Join-Path $script:Target 'mapping'
        New-Item -ItemType Directory -Path $mappingDir | Out-Null
        Set-Content -Path (Join-Path $mappingDir 'monikerMapping.json') -Value '{ "azclipsps-test": { "packageRoot": "azclipsps-test", "serviceMap": "mapping/az-groupMapping-test.json" } }'
        Set-Content -Path (Join-Path $mappingDir 'az-groupMapping-test.json') -Value '{ "Get-Gone": "Gone" }'
        git -C $script:Target add -A
        git -C $script:Target commit -m 'seed' --quiet

        $cfg = [PSCustomObject]@{ serviceGroupNames = [PSCustomObject]@{ Foo = 'Foo Service' } }
        $result = Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201208' -Config $cfg

        $result.ServiceMapPath | Should -Be 'mapping/az-groupMapping-test.json'
        $raw = [System.IO.File]::ReadAllText((Join-Path $mappingDir 'az-groupMapping-test.json'))
        $raw | Should -Be "{`n  `"Get-Foo`": `"Foo Service`",`n  `"Set-Foo`": `"Foo Service`"`n}`n"
    }

    It 'creates no branch or commit when the target already matches' {
        Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201209' | Out-Null
        $before = (git -C $script:Target rev-parse HEAD).Trim()

        $result = Invoke-SyncApply -Plan $script:Plan -TargetRepoPath $script:Target -Moniker 'azclipsps-test' -Timestamp '202608201210'

        $result.NoChanges | Should -Be $true
        $result.Branch | Should -BeNullOrEmpty
        (git -C $script:Target rev-parse --abbrev-ref HEAD).Trim() | Should -Be 'azclips-ps-sync-202608201209'
        (git -C $script:Target rev-parse HEAD).Trim() | Should -Be $before
        git -C $script:Target rev-parse --verify --quiet 'refs/heads/azclips-ps-sync-202608201210' 2>$null
        $LASTEXITCODE | Should -Not -Be 0
    }
}

Describe 'Get-StaleTargetFile' {
    BeforeEach {
        $script:Root = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-pssync-stale-" + [Guid]::NewGuid().ToString('N'))
        $script:Mon = Join-Path $script:Root 'mon'
        New-Item -ItemType Directory -Path (Join-Path $script:Mon 'Az.Keep') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $script:Mon 'Az.Gone') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $script:Mon 'Foreign') -Force | Out-Null
        foreach ($p in 'Az.Keep/Get-Keep.md', 'Az.Keep/Get-Stale.md', 'Az.Gone/Get-Gone.md', 'Foreign/readme.md', 'index.md') {
            Set-Content -Path (Join-Path $script:Mon $p) -Value 'x'
        }
        $script:KeepPlan = @([PSCustomObject]@{ Kind = 'CmdletMarkdown'; TargetPath = (Join-Path $script:Mon 'Az.Keep/Get-Keep.md') })
    }

    AfterEach {
        if (Test-Path $script:Root) { Remove-Item $script:Root -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'returns only files under Az.* folders that the plan does not produce' {
        $stale = @(Get-StaleTargetFile -Plan $script:KeepPlan -TargetRepoPath $script:Root -Moniker 'mon' |
            ForEach-Object { [System.IO.Path]::GetRelativePath($script:Mon, $_) -replace '\\', '/' })
        $stale | Should -Be @('Az.Gone/Get-Gone.md', 'Az.Keep/Get-Stale.md')
    }

    It 'limits reconciliation to the scoped target modules' {
        $stale = @(Get-StaleTargetFile -Plan $script:KeepPlan -TargetRepoPath $script:Root -Moniker 'mon' -ScopeTargetModules 'Az.Keep' |
            ForEach-Object { [System.IO.Path]::GetRelativePath($script:Mon, $_) -replace '\\', '/' })
        $stale | Should -Be @('Az.Keep/Get-Stale.md')
    }

    It 'returns nothing when the moniker folder is missing' {
        @(Get-StaleTargetFile -Plan $script:KeepPlan -TargetRepoPath $script:Root -Moniker 'missing').Count | Should -Be 0
    }
}

Describe 'New-ServiceGroupMap' {
    BeforeAll {
        $script:MapPlan = @(
            [PSCustomObject]@{ Kind = 'CmdletMarkdown'; TargetPath = 'm/Az.MsGraph/Get-AzMsGraphUser.md'; ModuleName = 'MsGraph' }
            [PSCustomObject]@{ Kind = 'CmdletMarkdown'; TargetPath = 'm/Az.Compute/New-AzVM.md'; ModuleName = 'Compute' }
            [PSCustomObject]@{ Kind = 'CmdletMarkdown'; TargetPath = 'm/Az.Compute/Get-AzVM.md'; ModuleName = 'Compute' }
            [PSCustomObject]@{ Kind = 'ModuleIndex'; TargetPath = 'm/Az.Compute/Az.Compute.md'; ModuleName = 'Compute' }
        )
        $script:MapConfig = [PSCustomObject]@{ serviceGroupNames = [PSCustomObject]@{ MsGraph = 'MS Graph' } }
    }

    It 'maps every cmdlet page to its module group, ordered by group then cmdlet' {
        $map = New-ServiceGroupMap -Plan $script:MapPlan -Config $script:MapConfig
        @($map.Keys) | Should -Be @('Get-AzVM', 'New-AzVM', 'Get-AzMsGraphUser')
        $map['Get-AzVM'] | Should -Be 'Compute'
        $map['Get-AzMsGraphUser'] | Should -Be 'MS Graph'
    }

    It 'keeps existing entries only for retained cmdlets' {
        $existing = @{ 'Get-AzOther' = 'Other'; 'Get-AzGone' = 'Gone'; 'Get-AzVM' = 'Stale Group' }
        $map = New-ServiceGroupMap -Plan $script:MapPlan -Config $script:MapConfig -Existing $existing -RetainCmdlets 'Get-AzOther', 'Get-AzVM'
        $map.Contains('Get-AzOther') | Should -Be $true
        $map.Contains('Get-AzGone') | Should -Be $false
        $map['Get-AzVM'] | Should -Be 'Compute'
    }

    It 'renders the hand-authored JSON layout with a blank line between groups' {
        $json = ConvertTo-ServiceGroupMapJson -Map (New-ServiceGroupMap -Plan $script:MapPlan -Config $script:MapConfig)
        $json | Should -Be "{`n  `"Get-AzVM`": `"Compute`",`n  `"New-AzVM`": `"Compute`",`n`n  `"Get-AzMsGraphUser`": `"MS Graph`"`n}`n"
        ($json | ConvertFrom-Json -AsHashtable).Count | Should -Be 3
    }
}

Describe 'Get-ServiceMapPath' {
    BeforeEach {
        $script:Root = Join-Path ([System.IO.Path]::GetTempPath()) ("azclips-pssync-map-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path (Join-Path $script:Root 'mapping') -Force | Out-Null
        Set-Content -Path (Join-Path $script:Root 'mapping/monikerMapping.json') -Value '{ "azps-preview": { "serviceMap": "mapping/az-groupMapping-preview.json" }, "aztools": { "packageRoot": "aztools" } }'
    }

    AfterEach {
        if (Test-Path $script:Root) { Remove-Item $script:Root -Recurse -Force -ErrorAction SilentlyContinue }
    }

    It 'resolves the serviceMap for the moniker' {
        Get-ServiceMapPath -TargetRepoPath $script:Root -Moniker 'azps-preview' |
            Should -Be (Join-Path $script:Root 'mapping/az-groupMapping-preview.json')
    }

    It 'returns null for a moniker without serviceMap or a missing mapping file' {
        Get-ServiceMapPath -TargetRepoPath $script:Root -Moniker 'aztools' | Should -BeNullOrEmpty
        Get-ServiceMapPath -TargetRepoPath $script:Root -Moniker 'unknown' | Should -BeNullOrEmpty
        Get-ServiceMapPath -TargetRepoPath (Join-Path $script:Root 'nope') -Moniker 'azps-preview' | Should -BeNullOrEmpty
    }
}
