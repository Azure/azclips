# Pester tests for build/Sync-AzclipsPowerShellDocs.ps1 (Phase 1: discovery + module-index
# generation + dry-run path resolution). See scratch/CLIPS-588-ps-sync-shim-PR-PLAN.md.
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

    It 'discovers all four modules against the real src/PS tree' {
        $repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
        $result = @(Get-PublishableModule -PsRoot (Join-Path $repoRoot 'src/PS'))
        ($result | ForEach-Object Name) | Sort-Object | Should -Be @('Compute', 'Config', 'Login', 'Resources')
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
        $repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
        $script:Modules = @(Get-PublishableModule -PsRoot (Join-Path $repoRoot 'src/PS'))
        $script:Config = Get-Content -Raw (Join-Path $repoRoot 'build/ps-docs-sync.config.json') | ConvertFrom-Json
        $script:Plan = @(New-SyncPlan -Modules $script:Modules -Moniker 'test-moniker' `
            -TargetRepoPath 'C:/fake/target' -TargetModuleNamePattern 'Az.{ModuleName}' `
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
        $script:Body | Should -Match 'content_git_url:.+src/PS/Compute/help/Az\.Compute\.md'
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
