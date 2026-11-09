BeforeAll {
    . (Join-Path $PSScriptRoot '../../build/Convert-PSHelpSyntax.ps1')
}

Describe 'PlatyPS syntax round-trip compatibility' {
    BeforeAll {
        Import-Module Microsoft.PowerShell.PlatyPS -RequiredVersion 1.0.1
    }

    It 'preserves parsed help and MAML for <Cmdlet>' -ForEach @(
        @{ Cmdlet = 'Set-AzConfig'; Module = 'Config'; SyntaxCount = 1; SetName = '__AllParameterSets' }
        @{ Cmdlet = 'New-AzVM'; Module = 'Compute'; SyntaxCount = 2; SetName = 'NewNetworkInterface' }
    ) {
        $source = Get-Content (Join-Path $PSScriptRoot "../../src/PS/$Module/help/$Cmdlet.md") -Raw
        $baseline = Convert-PSHelpSyntax -Content $source -ForPlatyPS
        $normalized = Convert-PSHelpSyntax -Content $baseline
        $roundTrip = Convert-PSHelpSyntax -Content $normalized -ForPlatyPS
        $exports = @()
        foreach ($content in @($baseline, $roundTrip)) {
            $dir = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
            New-Item -ItemType Directory -Path $dir | Out-Null
            $path = Join-Path $dir "$Cmdlet.md"
            Set-Content -LiteralPath $path -Value $content -NoNewline
            $help = Import-MarkdownCommandHelp -LiteralPath $path
            @($help.Syntax).Count | Should -Be $SyntaxCount
            $help.Syntax[0].ParameterSetName | Should -BeExactly $SetName
            $help.Aliases | Should -BeNullOrEmpty
            $help | Export-MamlCommandHelp -OutputFolder $dir -Force | Out-Null
            $xml = Get-ChildItem -Path $dir -Recurse -Filter *.xml -File
            $exports += [IO.File]::ReadAllText($xml.FullName)
        }
        $exports[1] | Should -BeExactly $exports[0]
    }
}

Describe 'PowerShell syntax heading normalization' {
    It 'removes only the internal syntax heading and preserves <Ending> line endings' -ForEach @(
        @{ Ending = 'LF'; Newline = "`n" }
        @{ Ending = 'CRLF'; Newline = "`r`n" }
    ) {
        $lines = @('## SYNTAX', '', '### __AllParameterSets', '', '```', 'Set-Fixture -Key <string>', '```', '', '## DESCRIPTION', 'Text.')
        $content = ($lines -join $Newline) + $Newline
        $expected = (($lines | Where-Object { $_ -ne '### __AllParameterSets' }) -join $Newline) + $Newline
        $actual = Convert-PSHelpSyntax -Content $content
        $actual | Should -BeExactly $expected
        Convert-PSHelpSyntax -Content $actual | Should -BeExactly $actual
    }

    It 'preserves named sets, example headings and fenced heading text' {
        $content = @'
## SYNTAX
### NewNetworkInterface (Default)
```
New-Fixture -Name <string>
### __AllParameterSets
```
### ExistingNetworkInterface
~~~
New-Fixture -Id <string>
### __AllParameterSets
~~~
## EXAMPLES
### __AllParameterSets
Example text.
'@
        Convert-PSHelpSyntax -Content $content | Should -BeExactly $content
    }

    It 'preserves a real Default set and distinguishes the exact internal name' {
        $content = "## SYNTAX`n### Default (Default)`n### __AllParameterSetsCustom`n"
        Convert-PSHelpSyntax -Content $content | Should -BeExactly $content
    }

    It 'preserves empty content and a file without a final newline' {
        Convert-PSHelpSyntax -Content '' | Should -BeExactly ''
        Convert-PSHelpSyntax -Content "## SYNTAX`n### __AllParameterSets" | Should -BeExactly "## SYNTAX`n"
    }

    It 'restores the internal name for tool input without changing named sets' {
        $content = "## SYNTAX`r`n`r`n``````powershell`r`nSet-Fixture`r`n```````r`n## DESCRIPTION`r`nText."
        $restored = Convert-PSHelpSyntax -Content $content -ForPlatyPS
        $restored | Should -Match '### __AllParameterSets\r\n\r\n'
        Convert-PSHelpSyntax -Content $restored -ForPlatyPS | Should -BeExactly $restored
        $named = "## SYNTAX`n### ByName (Default)`n```````nGet-Fixture`n```````n"
        Convert-PSHelpSyntax -Content $named -ForPlatyPS | Should -BeExactly $named
    }
}
