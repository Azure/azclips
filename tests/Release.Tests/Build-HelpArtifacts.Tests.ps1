BeforeAll {
    $buildScriptPath = Join-Path $PSScriptRoot '../../build/build.ps1'
    $dependencyScriptPath = Join-Path $PSScriptRoot '../../build/Install-BuildDependencies.ps1'
    $pwshPath = (Get-Process -Id $PID).Path
}

Describe 'Build help failure propagation' {
    It 'returns <ExpectedExitCode> for <Scenario>' -ForEach @(
        @{ Scenario = 'Dependency installation failure'; HelpToolExitCode = 0; ExpectedExitCode = 1; Message = 'Fixture dependency installation failed' }
        @{ Scenario = 'MAML conversion failure'; HelpToolExitCode = 0; ExpectedExitCode = 1; Message = 'PlatyPS pipeline failed' }
        @{ Scenario = 'Successful help validation'; HelpToolExitCode = 0; ExpectedExitCode = 0; Message = 'Fixture help validation output' }
        @{ Scenario = 'Failed help validation'; HelpToolExitCode = 17; ExpectedExitCode = 17; Message = 'Fixture help validation output' }
    ) {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $buildRoot = Join-Path $root 'build'
        $helpRoot = Join-Path $root 'src/PS/Accounts/help'
        New-Item -ItemType Directory -Path $buildRoot, $helpRoot -Force | Out-Null
        Copy-Item -Path $buildScriptPath -Destination (Join-Path $buildRoot 'build.ps1')
        Set-Content -Path (Join-Path $buildRoot 'Set-ModuleConfiguration.ps1') -Value ''
        Set-Content -Path (Join-Path $helpRoot 'Get-Fixture.md') -Value 'fixture'
        $dependencyStub = if ($Scenario -eq 'Dependency installation failure') { "throw 'Fixture dependency installation failed'" } else { '' }
        Set-Content -Path (Join-Path $buildRoot 'Install-BuildDependencies.ps1') -Value $dependencyStub
        $runnerPath = Join-Path $root 'run-build.ps1'
        Set-Content -Path $runnerPath -Value @'
param([string]$BuildScript, [string]$Scenario, [int]$HelpToolExitCode)
$ErrorActionPreference = 'Continue'
function dotnet {
    $global:LASTEXITCODE = 0
    if ($args[0] -eq 'run') {
        Write-Output 'Fixture help validation output'
        $global:LASTEXITCODE = $HelpToolExitCode
    }
}
function pwsh {
    if ($Scenario -eq 'MAML conversion failure') {
        $global:LASTEXITCODE = 9
        return
    }
    Set-Content -Path (Join-Path $args[-1] 'Microsoft.Azure.Azclips.Cmdlets.Accounts.dll-Help.xml') -Value '<helpItems />'
    $global:LASTEXITCODE = 0
}
& $BuildScript -Configuration Release -GenerateHelpArtifacts
exit $LASTEXITCODE
'@

        $output = & $pwshPath -NoLogo -NoProfile -File $runnerPath (Join-Path $buildRoot 'build.ps1') $Scenario $HelpToolExitCode 2>&1
        $exitCode = $LASTEXITCODE

        $exitCode | Should -Be $ExpectedExitCode
        ($output -join "`n") | Should -Match $Message
        if ($ExpectedExitCode -eq 1) {
            ($output -join "`n") | Should -Not -Match 'Generated MAML help'
        }
    }
}

Describe 'Packaged module dependencies' {
    It 'aligns the KeyVault Accounts dependency with the shipped package' {
        $psRoot = Join-Path $PSScriptRoot '../../src/PS'
        $keyVault = Import-PowerShellDataFile (Join-Path $psRoot 'KeyVault/KeyVault.psd1')
        $accounts = Import-PowerShellDataFile (Join-Path $psRoot 'Accounts/Accounts.psd1')

        ($keyVault.RequiredModules | Where-Object ModuleName -eq 'Accounts').ModuleVersion | Should -Be $accounts.ModuleVersion
    }
}

Describe 'Pinned build dependencies' {
    BeforeAll {
        Import-Module Microsoft.PowerShell.PSResourceGet
        function Install-Module { throw 'The legacy package installer must not be used.' }
    }

    BeforeEach {
        Mock Get-Module { @() }
        Mock Install-PSResource { }
    }

    It 'skips an already installed pinned version' {
        Mock Get-Module { [pscustomobject]@{ Version = [version]'1.0.1'; Path = 'fixture' } }

        & $dependencyScriptPath

        Should -Invoke Install-PSResource -Times 0 -Exactly
    }

    It 'installs the exact pinned version' {
        & $dependencyScriptPath

        Should -Invoke Install-PSResource -Times 1 -Exactly -ParameterFilter {
            $Name -eq 'Microsoft.PowerShell.PlatyPS' -and $Version -eq '1.0.1' -and
            $Repository -eq 'PSGallery' -and $Scope -eq 'CurrentUser' -and $TrustRepository
        }
    }

    It 'propagates installation failures' {
        Mock Install-PSResource { throw 'Fixture package download failed' }

        { & $dependencyScriptPath } | Should -Throw '*Fixture package download failed*'
    }

}