BeforeAll {
    $generator = Join-Path $PSScriptRoot '../../build/New-PSHelpMarkdown.ps1'
    $pwshPath = (Get-Process -Id $PID).Path
}

Describe 'PowerShell help date generation' {
    It 'uses invariant dates for scaffolding and refresh with <Culture> and dash separators' -ForEach @(
        @{ Culture = 'en-US' }
        @{ Culture = 'ar-SA' }
    ) {
        $root = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $build = Join-Path $root 'build'
        $module = Join-Path $root 'src/PS/DateProbe'
        New-Item -ItemType Directory -Path $build, $module -Force | Out-Null
        Copy-Item -LiteralPath $generator -Destination (Join-Path $build 'New-PSHelpMarkdown.ps1')
        Set-Content -LiteralPath (Join-Path $module 'DateProbe.psd1') -Value @'
@{
    RootModule = 'DateProbe.psm1'
    ModuleVersion = '1.0.0'
    GUID = '477fa467-3e81-4aaf-bfee-facfb5c1606a'
    FunctionsToExport = @('Get-DateProbe')
    CmdletsToExport = @()
}
'@
        Set-Content -LiteralPath (Join-Path $module 'DateProbe.psm1') -Value @'
function Get-DateProbe {
    [CmdletBinding()]
    param()
}
'@
        $runner = Join-Path $root 'run.ps1'
        Set-Content -LiteralPath $runner -Value @'
param([string]$Generator, [string]$HelpFile, [string]$Culture)
$ErrorActionPreference = 'Stop'
function pwsh {
    param([switch]$NoProfile, [scriptblock]$Command, [Alias('Args')][object[]]$ArgumentList)
    $original = [System.Globalization.CultureInfo]::CurrentCulture
    $culture = [System.Globalization.CultureInfo]::GetCultureInfo($Culture).Clone()
    $culture.DateTimeFormat.DateSeparator = '-'
    [System.Globalization.CultureInfo]::CurrentCulture = $culture
    try { & $Command @ArgumentList }
    finally { [System.Globalization.CultureInfo]::CurrentCulture = $original }
}
$expected = @([DateTime]::Now.ToString('MM/dd/yyyy', [System.Globalization.CultureInfo]::InvariantCulture))
& $Generator -Module DateProbe
$expected += [DateTime]::Now.ToString('MM/dd/yyyy', [System.Globalization.CultureInfo]::InvariantCulture)
$scaffold = (Select-String -LiteralPath $HelpFile -Pattern '^ms.date:').Line
& $Generator -Module DateProbe -Refresh
$expected += [DateTime]::Now.ToString('MM/dd/yyyy', [System.Globalization.CultureInfo]::InvariantCulture)
$refresh = (Select-String -LiteralPath $HelpFile -Pattern '^ms.date:').Line
if ($scaffold -notin @($expected | ForEach-Object { "ms.date: $_" })) { throw "Invalid scaffold date: $scaffold" }
if ($refresh -notin @($expected | ForEach-Object { "ms.date: $_" })) { throw "Invalid refresh date: $refresh" }
'@
        $output = & $pwshPath -NoProfile -File $runner (Join-Path $build 'New-PSHelpMarkdown.ps1') (Join-Path $module 'help/Get-DateProbe.md') $Culture 2>&1
        $LASTEXITCODE | Should -Be 0 -Because ($output -join "`n")
    }
}
