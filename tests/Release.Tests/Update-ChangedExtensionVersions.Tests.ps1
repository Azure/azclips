BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '../../devops/scripts/publishing/Update-ChangedExtensionVersions.ps1'
    $releaseChangelogScriptPath = Join-Path $PSScriptRoot '../../devops/scripts/publishing/Update-ReleaseChangelog.ps1'
    $pwshPath = (Get-Process -Id $PID).Path

    function Write-TestFile {
        param(
            [string] $Root,
            [string] $RelativePath,
            [string] $Content
        )

        $path = Join-Path $Root $RelativePath
        New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
        [System.IO.File]::WriteAllText($path, $Content, [System.Text.UTF8Encoding]::new($false))
    }

    function New-TestRepository {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root | Out-Null

        Write-TestFile -Root $root -RelativePath 'src/Extensions/Accounts/Extensions.Accounts.csproj' -Content @'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Version>0.1.0</Version>
    </PropertyGroup>
</Project>
'@
        Write-TestFile -Root $root -RelativePath 'src/Extensions/Accounts/Processor.cs' -Content 'class AccountsProcessor { }'
        Write-TestFile -Root $root -RelativePath 'src/Extensions/Compute/Extensions.Compute.csproj' -Content @'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Version>0.1.0</Version>
    </PropertyGroup>
</Project>
'@
        Write-TestFile -Root $root -RelativePath 'src/Extensions/Compute/Processor.cs' -Content 'class ComputeProcessor { }'
        Write-TestFile -Root $root -RelativePath 'src/PS/Accounts/Accounts.psd1' -Content "@{`nModuleVersion = '0.1.0'`n}`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/Compute/Compute.psd1' -Content "@{`nModuleVersion = '0.1.0'`nRequiredModules = @(`n    @{ ModuleName = 'Accounts'; ModuleVersion = '0.1.0' }`n)`n}`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/Accounts/ChangeLog.md' -Content "## Upcoming Release`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/Compute/ChangeLog.md' -Content "## Upcoming Release`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/Az/Az.psd1' -Content @'
@{
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },
    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' }
)
}
'@

        git -C $root init --quiet
        git -C $root config user.name 'Release Test'
        git -C $root config user.email 'release-test@example.com'
        git -C $root add .
        git -C $root commit --quiet -m 'baseline'

        $root
    }
}

Describe 'Update-ChangedExtensionVersions' {
    It 'patch-bumps changed extensions and transitive dependents while aligning metadata' {
        $root = New-TestRepository
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change Accounts extension code'

        & $scriptPath -RepoRoot $root -BaseRef 'HEAD^'

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') | Should -Match "ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/Extensions/Compute/Extensions.Compute.csproj') | Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Compute/Compute.psd1') | Should -Match "ModuleName = 'Accounts'; ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Compute/Compute\.psd1'; ModuleVersion = '0\.1\.1'"
    }

    It 'updates inline RequiredModules while preserving layout and commented dependencies' {
        $root = New-TestRepository
        $manifest = @'
@{
ModuleVersion = '0.1.0'
# RequiredModules = @(@{ ModuleName = 'Accounts'; ModuleVersion = '9.9.9' })
RequiredModules = @(@{ ModuleName = 'Accounts'; ModuleVersion = '0.1.0' }) # Keep inline
}
'@
        Write-TestFile -Root $root -RelativePath 'src/PS/Compute/Compute.psd1' -Content $manifest
        git -C $root add .
        git -C $root commit --quiet -m 'use inline module dependency'
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change Accounts extension code'

        & $scriptPath -RepoRoot $root -BaseRef 'HEAD^'

        [System.IO.File]::ReadAllText((Join-Path $root 'src/PS/Compute/Compute.psd1')) |
            Should -BeExactly ($manifest.Replace("ModuleVersion = '0.1.0'", "ModuleVersion = '0.1.1'"))
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') |
            Should -Match "Compute/Compute\.psd1'; ModuleVersion = '0\.1\.1'"
    }

    It 'does not double-bump an extension already set to the expected patch version' {
        $root = New-TestRepository
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        (Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj')).Replace(
            '<Version>0.1.0</Version>',
            '<Version>0.1.1</Version>'
        ) | Set-Content -NoNewline (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj')
        git -C $root add .
        git -C $root commit --quiet -m 'change and bump Accounts extension'

        & $scriptPath -RepoRoot $root -BaseRef 'HEAD^'

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Not -Match '<Version>0\.1\.2</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') | Should -Match "ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.1'"
    }

    It 'preserves a project UTF-8 BOM while updating its version' {
        $root = New-TestRepository
        $projectPath = Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj'
        $projectContent = Get-Content -Raw $projectPath
        [System.IO.File]::WriteAllText($projectPath, $projectContent, [System.Text.UTF8Encoding]::new($true))
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change Accounts extension with BOM project'

        & $scriptPath -RepoRoot $root -BaseRef 'HEAD^'

        $projectBytes = [System.IO.File]::ReadAllBytes($projectPath)
        @($projectBytes[0..2]) | Should -Be @(0xEF, 0xBB, 0xBF)
        Get-Content -Raw $projectPath | Should -Match '<Version>0\.1\.1</Version>'
    }

    It 'preserves a project without a final newline while updating its version' {
        $root = New-TestRepository
        $projectPath = Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj'
        $projectContent = (Get-Content -Raw $projectPath).TrimEnd("`r", "`n")
        [System.IO.File]::WriteAllText($projectPath, $projectContent, [System.Text.UTF8Encoding]::new($false))
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change Accounts extension without project final newline'

        & $scriptPath -RepoRoot $root -BaseRef 'HEAD^'

        Get-Content -Raw $projectPath | Should -Match '<Version>0\.1\.1</Version>'
        [System.IO.File]::ReadAllText($projectPath).EndsWith("`n") | Should -BeFalse
    }

    It 'skips updates when no stable tag exists' {
        $root = New-TestRepository
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change before first stable release'

        & $scriptPath -RepoRoot $root

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Match '<Version>0\.1\.0</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') | Should -Match "ModuleVersion = '0\.1\.0'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.0'"
    }

    It 'validates packaging metadata when no stable tag exists' {
        $root = New-TestRepository
        Remove-Item (Join-Path $root 'src/PS/Accounts/ChangeLog.md')
        git -C $root add --all
        git -C $root commit --quiet -m 'remove changelog before first stable release'

        { & $scriptPath -RepoRoot $root } |
            Should -Throw '*PowerShell changelog was not found for rollup extension Accounts*'
    }

    It 'aligns first-release metadata to the declared extension version' {
        $root = New-TestRepository
        $projectPath = Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj'
        (Get-Content -Raw $projectPath).Replace('0.1.0', '0.2.0') |
            Set-Content -NoNewline $projectPath
        git -C $root add --all
        git -C $root commit --quiet -m 'declare initial Accounts version'

        & $scriptPath -RepoRoot $root

        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') |
            Should -Match "ModuleVersion = '0\.2\.0'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') |
            Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.2\.0'"
    }

    It 'uses the latest stable tag when BaseRef is omitted' {
        $root = New-TestRepository
        git -C $root tag 'v0.1.0.20260101T000000Z'
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change after stable release'
        git -C $root tag 'v-not-a-stable-tag'

        & $scriptPath -RepoRoot $root

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Match '<Version>0\.1\.1</Version>'
    }

    It 'rejects a changed rollup extension without a child changelog' {
        $root = New-TestRepository
        Remove-Item (Join-Path $root 'src/PS/Accounts/ChangeLog.md')
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add --all
        git -C $root commit --quiet -m 'remove changelog and change Accounts extension'

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } |
            Should -Throw '*PowerShell changelog was not found for rollup extension Accounts*'
    }

    It 'rejects an unchanged rollup extension without a child changelog' {
        $root = New-TestRepository
        Remove-Item (Join-Path $root 'src/PS/Compute/ChangeLog.md')
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add --all
        git -C $root commit --quiet -m 'remove Compute changelog and change Accounts extension'

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } |
            Should -Throw '*PowerShell changelog was not found for rollup extension Compute*'
    }

    It 'patch-bumps an extension with PowerShell-only changes' {
        $root = New-TestRepository
        Write-TestFile -Root $root -RelativePath 'src/PS/Accounts/Constants.cs' -Content 'class Constants { }'
        git -C $root add --all
        git -C $root commit --quiet -m 'change Accounts PowerShell module'

        & $scriptPath -RepoRoot $root -BaseRef 'HEAD^'

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') |
            Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') |
            Should -Match "ModuleVersion = '0\.1\.1'"
    }

    It 'rejects a packageable extension missing its Az rollup dependency' {
        $root = New-TestRepository
        $rollupPath = Join-Path $root 'src/PS/Az/Az.psd1'
        (Get-Content -Raw $rollupPath).Replace(
            "    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },`n",
            ''
        ) | Set-Content -NoNewline $rollupPath
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add --all
        git -C $root commit --quiet -m 'remove rollup entry and change Accounts extension'

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } |
            Should -Throw '*Az rollup dependency was not found for packageable extension Accounts*'
    }

    It 'rejects a commented-out Az rollup dependency' {
        $root = New-TestRepository
        $rollupPath = Join-Path $root 'src/PS/Az/Az.psd1'
        (Get-Content -Raw $rollupPath).Replace(
            "    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },`n",
            "    # @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0' },`n"
        ) | Set-Content -NoNewline $rollupPath
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add --all
        git -C $root commit --quiet -m 'comment out rollup entry and change Accounts extension'

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } |
            Should -Throw '*Az rollup dependency was not found for packageable extension Accounts*'
        Get-Content -Raw $rollupPath | Should -Match "# @\{ ModuleName = '../Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.0' \}"
    }

    It 'rejects an Az-only rollup version mismatch' {
        $root = New-TestRepository
        $rollupPath = Join-Path $root 'src/PS/Az/Az.psd1'
        (Get-Content -Raw $rollupPath).Replace(
            "ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.0'",
            "ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.2.0'"
        ) | Set-Content -NoNewline $rollupPath
        git -C $root add --all
        git -C $root commit --quiet -m 'change only the Accounts rollup version'

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } |
            Should -Throw '*Version metadata is not aligned for packageable extension Accounts*'
    }

    It 'skips a changed extension project without a PowerShell child module' {
        $root = New-TestRepository
        Write-TestFile -Root $root -RelativePath 'src/Extensions/ExtensionRuntime/Extensions.ExtensionRuntime.csproj' -Content '<Project Sdk="Microsoft.NET.Sdk" />'
        Write-TestFile -Root $root -RelativePath 'src/Extensions/ExtensionRuntime/Runtime.cs' -Content 'class Runtime { }'
        git -C $root add .
        git -C $root commit --quiet -m 'change non-packageable extension runtime'

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } | Should -Not -Throw
    }

    It 'fails on a missing historical Git object instead of treating the extension as new' {
        $root = New-TestRepository
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change Accounts extension code'
        $objectId = git -C $root rev-parse 'HEAD^:src/Extensions/Accounts/Extensions.Accounts.csproj'
        $objectPath = Join-Path $root ".git/objects/$($objectId.Substring(0, 2))/$($objectId.Substring(2))"
        Remove-Item -LiteralPath $objectPath -Force

        { & $scriptPath -RepoRoot $root -BaseRef 'HEAD^' } |
            Should -Throw '*git show failed while reading src/Extensions/Accounts/Extensions.Accounts.csproj*'
    }

    It 'preserves a new extension version and succeeds under the pipeline exit-code wrapper' {
        $root = New-TestRepository
        $baseRef = git -C $root rev-parse HEAD
        $identityProjectPath = 'src/Extensions/ManagedServiceIdentity/Extensions.ManagedServiceIdentity.csproj'
        Write-TestFile -Root $root -RelativePath $identityProjectPath -Content @'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Version>0.1.0</Version>
    </PropertyGroup>
</Project>
'@
        Write-TestFile -Root $root -RelativePath 'src/Extensions/ManagedServiceIdentity/Processor.cs' -Content 'class ManagedServiceIdentityProcessor { }'
        Write-TestFile -Root $root -RelativePath 'src/PS/ManagedServiceIdentity/ManagedServiceIdentity.psd1' -Content "@{`nModuleVersion = '0.1.0'`n}`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/ManagedServiceIdentity/ChangeLog.md' -Content "## Upcoming Release`n"
        $rollupPath = Join-Path $root 'src/PS/Az/Az.psd1'
        $rollupContent = Get-Content -Raw $rollupPath
        $rollupContent = $rollupContent.Replace(
            "    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' }`n)",
            "    @{ ModuleName = '../Compute/Compute.psd1'; ModuleVersion = '0.1.0' },`n    @{ ModuleName = '../ManagedServiceIdentity/ManagedServiceIdentity.psd1'; ModuleVersion = '0.1.0' }`n)"
        )
        [System.IO.File]::WriteAllText($rollupPath, $rollupContent, [System.Text.UTF8Encoding]::new($false))
        git -C $root add .
        git -C $root commit --quiet -m 'add ManagedServiceIdentity extension'

        Write-TestFile -Root $root -RelativePath 'invoke-release-update.ps1' -Content @'
param([string] $ScriptPath, [string] $RepoRoot, [string] $BaseRef)
$ErrorActionPreference = 'Stop'
. $ScriptPath -RepoRoot $RepoRoot -BaseRef $BaseRef
if (Test-Path -LiteralPath variable:\LASTEXITCODE) {
    exit $LASTEXITCODE
}
'@
        $output = & $pwshPath -NoLogo -NoProfile -File (Join-Path $root 'invoke-release-update.ps1') $scriptPath $root $baseRef 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join "`n")

        Get-Content -Raw (Join-Path $root 'src/Extensions/ManagedServiceIdentity/Extensions.ManagedServiceIdentity.csproj') | Should -Match '<Version>0\.1\.0</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/ManagedServiceIdentity/ManagedServiceIdentity.psd1') | Should -Match "ModuleVersion = '0\.1\.0'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "ManagedServiceIdentity/ManagedServiceIdentity\.psd1'; ModuleVersion = '0\.1\.0'"
        @(git -C $root diff --name-only -- $identityProjectPath) | Should -BeNullOrEmpty
    }
}

Describe 'Update-ReleaseChangelog extension versions' {
    It 'preserves child module versions while bumping the Az rollup version' {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $root | Out-Null
        Write-TestFile -Root $root -RelativePath 'CHANGELOG.md' -Content "## Upcoming Release`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/Accounts/ChangeLog.md' -Content @'
## Upcoming Release

- Added account support.
'@
        Write-TestFile -Root $root -RelativePath 'src/PS/Accounts/Accounts.psd1' -Content "@{`nModuleVersion = '0.1.1'`n}`n"
        Write-TestFile -Root $root -RelativePath 'src/PS/Az/Az.psd1' -Content @'
@{
ModuleVersion = '0.1.2'
RequiredModules = @(
    @{ ModuleName = '../Accounts/Accounts.psd1'; ModuleVersion = '0.1.1' }
)
}
'@

        git -C $root init --quiet
        git -C $root config user.name 'Release Test'
        git -C $root config user.email 'release-test@example.com'
        git -C $root add .
        git -C $root commit --quiet -m 'release fixture'

        & $releaseChangelogScriptPath -RepoRoot $root -Version '0.1.3' -ReleaseTimestamp '2026-09-01'

        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') | Should -Match "ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/ChangeLog.md') | Should -Match '## Version 0\.1\.1'
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "ModuleVersion = '0\.1\.3'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'CHANGELOG.md') | Should -Match '#### Az\.Accounts 0\.1\.1'
    }
}