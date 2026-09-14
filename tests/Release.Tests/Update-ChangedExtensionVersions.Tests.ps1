BeforeAll {
    $scriptPath = Join-Path $PSScriptRoot '../../devops/scripts/publishing/Update-ChangedExtensionVersions.ps1'

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
        Write-TestFile -Root $root -RelativePath 'src/PS/Compute/Compute.psd1' -Content "@{`nModuleVersion = '0.1.0'`n}`n"
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
    It 'patch-bumps only extensions with code changes and aligns PowerShell metadata' {
        $root = New-TestRepository
        Add-Content -Path (Join-Path $root 'src/Extensions/Accounts/Processor.cs') -Value "`nclass Changed { }"
        git -C $root add .
        git -C $root commit --quiet -m 'change Accounts extension code'

        & $scriptPath -RepoRoot $root -Version '0.1.1' -BaseRef 'HEAD^'

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') | Should -Match "ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/Extensions/Compute/Extensions.Compute.csproj') | Should -Match '<Version>0\.1\.0</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Compute/Compute\.psd1'; ModuleVersion = '0\.1\.0'"
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

        & $scriptPath -RepoRoot $root -Version '0.1.1' -BaseRef 'HEAD^'

        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/Extensions/Accounts/Extensions.Accounts.csproj') | Should -Not -Match '<Version>0\.1\.2</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Accounts/Accounts.psd1') | Should -Match "ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Accounts/Accounts\.psd1'; ModuleVersion = '0\.1\.1'"
    }

    It 'versions a new rollup extension that did not exist at the stable baseline' {
        $root = New-TestRepository
        $baseRef = git -C $root rev-parse HEAD
        Write-TestFile -Root $root -RelativePath 'src/Extensions/Identity/Extensions.Identity.csproj' -Content @'
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <Version>0.1.0</Version>
    </PropertyGroup>
</Project>
'@
        Write-TestFile -Root $root -RelativePath 'src/Extensions/Identity/Processor.cs' -Content 'class IdentityProcessor { }'
        Write-TestFile -Root $root -RelativePath 'src/PS/Identity/Identity.psd1' -Content "@{`nModuleVersion = '0.1.0'`n}`n"
        Add-Content -Path (Join-Path $root 'src/PS/Az/Az.psd1') -Value "@{ ModuleName = '../Identity/Identity.psd1'; ModuleVersion = '0.1.0' }"
        git -C $root add .
        git -C $root commit --quiet -m 'add Identity extension'

        & $scriptPath -RepoRoot $root -Version '0.1.1' -BaseRef $baseRef

        Get-Content -Raw (Join-Path $root 'src/Extensions/Identity/Extensions.Identity.csproj') | Should -Match '<Version>0\.1\.1</Version>'
        Get-Content -Raw (Join-Path $root 'src/PS/Identity/Identity.psd1') | Should -Match "ModuleVersion = '0\.1\.1'"
        Get-Content -Raw (Join-Path $root 'src/PS/Az/Az.psd1') | Should -Match "Identity/Identity\.psd1'; ModuleVersion = '0\.1\.1'"
    }
}