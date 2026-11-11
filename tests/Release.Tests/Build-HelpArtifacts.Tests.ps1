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

Describe 'MAML example normalization' {
    BeforeAll {
        $tokens = $null
        $parseErrors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($buildScriptPath, [ref]$tokens, [ref]$parseErrors)
        $parseErrors | Should -BeNullOrEmpty
        $repair = $ast.Find({
            param($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -eq 'Repair-MamlExampleCodeBlocks'
        }, $true)
        . ([scriptblock]::Create($repair.Extent.Text))

        $mamlTemplate = @'
<helpItems schema="maml">
  <command:command xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10"
    xmlns:maml="http://schemas.microsoft.com/maml/2004/10"
    xmlns:dev="http://schemas.microsoft.com/maml/dev/2004/10">
    <command:details>
      <command:name>__NAME__</command:name>
      <maml:description><maml:para>Fixture help.</maml:para></maml:description>
      <command:verb>Get</command:verb><command:noun>Fixture</command:noun>
    </command:details>
    <maml:description><maml:para>Fixture help.</maml:para></maml:description>
    <command:syntax />
    <command:examples>
      <command:example>
        <maml:title>Fixture example</maml:title>
        __EXAMPLE__
        <dev:remarks />
      </command:example>
    </command:examples>
  </command:command>
</helpItems>
'@
    }

    BeforeEach {
        $commandName = 'Get-Fixture' + [guid]::NewGuid().ToString('N')
        $path = Join-Path $TestDrive "$commandName.xml"
    }

    It 'normalizes <Scenario> without losing content and is idempotent' -ForEach @(
        @{
            Scenario = 'an exported entity separator and tagged code fence'
            Example = '<maml:introduction><maml:para>Description.</maml:para><maml:para>&#x80;</maml:para><maml:para>```powershell' + "`nGet-Fixture`n" + '```</maml:para></maml:introduction><dev:code />'
            ExpectedParagraphs = @("Description.`n`n")
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'literal separators between multiple description paragraphs'
            Example = '<maml:introduction><maml:para>First paragraph.</maml:para><maml:para>' + [char]0x80 + '</maml:para><maml:para>Second paragraph.</maml:para><maml:para>' + [char]0x80 + '</maml:para></maml:introduction><dev:code>Get-Fixture</dev:code>'
            ExpectedParagraphs = @("First paragraph.`n", "Second paragraph.`n`n")
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'multiple fenced code blocks'
            Example = '<maml:introduction><maml:para>Description.</maml:para><maml:para>&#x80;</maml:para><maml:para>```powershell' + "`nGet-Fixture`n" + '```</maml:para><maml:para>&#x80;</maml:para><maml:para>```' + "`nGet-Fixture -Full`n" + '```</maml:para></maml:introduction>'
            ExpectedParagraphs = @("Description.`n`n")
            ExpectedCode = "Get-Fixture`n`nGet-Fixture -Full"
        }
        @{
            Scenario = 'an already populated code node'
            Example = '<maml:introduction><maml:para>Description.</maml:para><maml:para>&#x80;</maml:para></maml:introduction><dev:code>Get-Fixture -Full</dev:code>'
            ExpectedParagraphs = @("Description.`n`n")
            ExpectedCode = 'Get-Fixture -Full'
        }
        @{
            Scenario = 'a code-only example'
            Example = '<maml:introduction><maml:para>```powershell' + "`nGet-Fixture`n" + '```</maml:para></maml:introduction><dev:code />'
            ExpectedParagraphs = @()
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'an example without an introduction'
            Example = '<dev:code>Get-Fixture</dev:code>'
            ExpectedParagraphs = @()
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'an existing description without a separator'
            Example = '<maml:introduction><maml:para>Description.</maml:para></maml:introduction><dev:code>Get-Fixture</dev:code>'
            ExpectedParagraphs = @("Description.`n`n")
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'a description with an existing line break'
            Example = '<maml:introduction><maml:para>Description.' + "`n" + '</maml:para></maml:introduction><dev:code>Get-Fixture</dev:code>'
            ExpectedParagraphs = @("Description.`n`n")
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'a description with an existing blank line'
            Example = '<maml:introduction><maml:para>Description.' + "`n`n" + '</maml:para></maml:introduction><dev:code>Get-Fixture</dev:code>'
            ExpectedParagraphs = @("Description.`n`n")
            ExpectedCode = 'Get-Fixture'
        }
        @{
            Scenario = 'an inline control character that is not a generated separator'
            Example = '<maml:introduction><maml:para>Keep ' + [char]0x80 + ' this text.</maml:para></maml:introduction><dev:code>Get-Fixture</dev:code>'
            ExpectedParagraphs = @("Keep $([char]0x80) this text.`n`n")
            ExpectedCode = 'Get-Fixture'
        }
    ) {
        $xml = $mamlTemplate.Replace('__NAME__', $commandName).Replace('__EXAMPLE__', $Example)
        Set-Content -LiteralPath $path -Value $xml -Encoding utf8NoBOM

        Repair-MamlExampleCodeBlocks -Path $path

        [xml]$doc = Get-Content -LiteralPath $path -Raw
        $ns = [System.Xml.XmlNamespaceManager]::new($doc.NameTable)
        $ns.AddNamespace('maml', 'http://schemas.microsoft.com/maml/2004/10')
        $ns.AddNamespace('dev', 'http://schemas.microsoft.com/maml/dev/2004/10')
        $paragraphs = $doc.SelectNodes('//maml:introduction/maml:para', $ns)
        $paragraphs.Count | Should -Be $ExpectedParagraphs.Count
        for ($i = 0; $i -lt $paragraphs.Count; $i++) {
            $paragraphs[$i].InnerText | Should -BeExactly $ExpectedParagraphs[$i]
        }
        $doc.SelectSingleNode('//dev:code', $ns).InnerText | Should -BeExactly $ExpectedCode

        $firstResult = Get-Content -LiteralPath $path -Raw
        Repair-MamlExampleCodeBlocks -Path $path
        (Get-Content -LiteralPath $path -Raw) | Should -BeExactly $firstResult
    }

    It 'renders a blank line before code without control characters in <View> with <Separator> separators' -ForEach @(
        @{ View = 'Examples'; Separator = 'entity' }
        @{ View = 'Full'; Separator = 'entity' }
        @{ View = 'Examples'; Separator = 'literal' }
        @{ View = 'Full'; Separator = 'literal' }
    ) {
        $example = @'
<maml:introduction>
  <maml:para>First description paragraph.</maml:para>
  <maml:para>&#x80;</maml:para>
  <maml:para>Second description paragraph.</maml:para>
  <maml:para>&#x80;</maml:para>
  <maml:para>```powershell
Get-Fixture -Name example
```</maml:para>
</maml:introduction>
<dev:code />
'@
        if ($Separator -eq 'literal') {
            $example = $example.Replace('&#x80;', [string][char]0x80)
        }
        $xml = $mamlTemplate.Replace('__NAME__', $commandName).Replace('__EXAMPLE__', $example)
        Set-Content -LiteralPath $path -Value $xml -Encoding utf8NoBOM
        Repair-MamlExampleCodeBlocks -Path $path

        $functionBody = [scriptblock]::Create("<#`n.EXTERNALHELP $path`n#>`n[CmdletBinding()]param()")
        Set-Item -Path "Function:\$commandName" -Value $functionBody
        try {
            $parameters = @{ Name = $commandName }
            $parameters[$View] = $true
            $help = Get-Help @parameters
            foreach ($width in @(60, 100)) {
                $formatted = $help | Out-String -Width $width
                $formatted.Contains([char]0x80) | Should -BeFalse
                $formatted | Should -Match 'First description paragraph\.\r?\n[ \t]+Second description paragraph\.\r?\n[ \t]*\r?\n[ \t]+Get-Fixture -Name example'
                $formatted | Should -Not -Match '```'
            }
        }
        finally {
            Remove-Item -LiteralPath "Function:\$commandName"
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