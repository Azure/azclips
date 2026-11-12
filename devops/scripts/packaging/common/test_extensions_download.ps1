[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExtensionsDirectory
)

$ErrorActionPreference = 'Stop'
$downloaderPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../../install/azcli-extensions-download.ps1'))
$testRoot = Join-Path ([IO.Path]::GetTempPath()) "azclips-extension-download-test-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $testRoot | Out-Null
$fixture = @{
    Scenario = 'valid'
    Tag = 'v-fixture.timestamp'
    Archive = (Join-Path $testRoot 'pipeline-bundle.tar.gz')
    Endpoint = ''
}
try {
    tar -czf $fixture.Archive -C $ExtensionsDirectory .
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the extensions fixture from pipeline packages.' }
    Push-Location $testRoot
    try {
        function Invoke-RestMethod {
            [CmdletBinding()]
            param([string]$Uri)
            if ($Uri -notin @('https://api.github.com/repos/Azure/azclips/releases/latest',
                "https://api.github.com/repos/Azure/azclips/releases/tags/$($fixture.Tag)")) {
                throw "Unexpected metadata URL: $Uri"
            }
            $fixture.Endpoint = $Uri
            if ($fixture.Scenario -eq 'metadata-failure') { throw 'Fixture HTTP 403 rate limit' }
            $asset = @{
                name = 'azcli-extensions-0.0.0.tar.gz'
                browser_download_url = "https://github.com/Azure/azclips/releases/download/$($fixture.Tag)/azcli-extensions-0.0.0.tar.gz"
            }
            if ($fixture.Scenario -eq 'invalid-url') { $asset.browser_download_url += '?download=1' }
            if ($fixture.Scenario -eq 'wrong-repo') { $asset.browser_download_url = $asset.browser_download_url.Replace('/Azure/', '/Other/') }
            if ($fixture.Scenario -eq 'unsafe-name') { $asset.name = '../azcli-extensions-0.0.0.tar.gz' }
            $assets = @($asset, @{ name = 'unrelated.tar.gz'; browser_download_url = 'https://example.invalid/wrong' })
            if ($fixture.Scenario -eq 'missing') { $assets = @() }
            if ($fixture.Scenario -eq 'duplicate') { $assets = @($asset, $asset) }
            $tag = switch ($fixture.Scenario) {
                'malformed' { '' }
                'mismatched-release' { 'v-other' }
                default { $fixture.Tag }
            }
            @{ tag_name = $tag; assets = $assets }
        }
        function Invoke-WebRequest {
            [CmdletBinding()]
            param([string]$Uri, [string]$OutFile)
            if ($Uri -ne "https://github.com/Azure/azclips/releases/download/$($fixture.Tag)/azcli-extensions-0.0.0.tar.gz") {
                throw "Unexpected asset URL: $Uri"
            }
            if ($fixture.Scenario -eq 'download-failure') {
                Set-Content -LiteralPath $OutFile -Value 'partial download'
                throw 'Fixture HTTP 503 download failure'
            }
            if ($fixture.Scenario -ne 'missing-download') {
                Copy-Item -LiteralPath $fixture.Archive -Destination $OutFile
            }
        }
        function jq { throw 'Download must not invoke jq.' }
        function Assert-DownloadCleanup {
            if (Get-ChildItem -Force -Filter '.azcli-extensions-download*') {
                throw 'Extensions downloader left staging files.'
            }
        }
        Set-Content -Path azcli-extensions.tar.gz -Value 'existing bundle sentinel'
        $before = (Get-FileHash azcli-extensions.tar.gz).Hash
        foreach ($scenario in 'missing', 'duplicate', 'malformed', 'mismatched-release', 'invalid-url', 'wrong-repo', 'unsafe-name', 'metadata-failure', 'download-failure', 'missing-download') {
            $fixture.Scenario = $scenario
            $expected = switch ($scenario) {
                'malformed' { 'valid matching tag_name' }
                'mismatched-release' { 'valid matching tag_name' }
                'invalid-url' { 'invalid GitHub download URL' }
                'wrong-repo' { 'invalid GitHub download URL' }
                'metadata-failure' { 'Fixture HTTP 403' }
                'download-failure' { 'Fixture HTTP 503' }
                'missing-download' { 'Download did not produce' }
                default { 'Expected exactly one' }
            }
            try {
                if ($scenario -eq 'mismatched-release') {
                    & $downloaderPath -Release $fixture.Tag
                }
                else {
                    Get-Content -Raw $downloaderPath | Invoke-Expression
                }
                throw "FAIL: $scenario unexpectedly succeeded."
            }
            catch {
                if ($_.Exception.Message -notlike "*$expected*") { throw }
            }
            if ((Get-FileHash azcli-extensions.tar.gz).Hash -ne $before) {
                throw "FAIL: $scenario changed the existing bundle."
            }
            Assert-DownloadCleanup
        }
        $fixture.Scenario = 'valid'
        $beforePath = $env:PATH
        $ErrorActionPreference = 'Continue'
        Get-Content -Raw $downloaderPath | Invoke-Expression
        if ($ErrorActionPreference -ne 'Continue' -or $env:PATH -ne $beforePath -or
            (Get-Variable metadata -Scope Local -ErrorAction SilentlyContinue)) {
            throw 'Download helper leaked caller state.'
        }
        $ErrorActionPreference = 'Stop'
        & $downloaderPath -Release $fixture.Tag
        if ($fixture.Endpoint -notlike '*/tags/v-fixture.timestamp') { throw 'Download ignored the pinned release.' }
        if ((Get-FileHash azcli-extensions.tar.gz).Hash -ne (Get-FileHash $fixture.Archive).Hash) {
            throw 'Downloaded bundle differs from the pipeline packages.'
        }
        Assert-DownloadCleanup
        if (@(Get-ChildItem -Force).Count -ne 2) { throw 'Helper extracted or created unexpected files.' }
        try {
            & $downloaderPath -Release '../invalid'
            throw 'FAIL: Invalid release tag was accepted.'
        }
        catch {
            if ($_.Exception.Message -notlike '*pattern*') { throw }
        }
        Remove-Item -LiteralPath azcli-extensions.tar.gz
        New-Item -ItemType Directory -Path azcli-extensions.tar.gz | Out-Null
        try {
            Get-Content -Raw $downloaderPath | Invoke-Expression
            throw 'FAIL: Directory destination was accepted.'
        }
        catch {
            if ($_.Exception.Message -notlike '*non-file destination*') { throw }
        }
        Write-Host 'PASS: PowerShell download-only extensions helper, pinned release, scope isolation, failure preservation, and cleanup.'
    }
    finally { Pop-Location }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction Stop
}
