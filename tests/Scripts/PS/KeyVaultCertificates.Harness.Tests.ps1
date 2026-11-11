$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'KeyVaultCertificates.Live.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($script, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
foreach ($name in @('Assert-CertificateState', 'Save-CertificateState', 'Register-TestCertificate', 'Remove-RunCertificates', 'Assert-CertificateNativeResult', 'Get-CertificateDiagnosticFiles')) {
    $functionAst = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name }, $true)
    . ([scriptblock]::Create($functionAst.Extent.Text))
}
$VaultName = 'fixture'
$ProfileName = 'fixture-profile'
$state = @{ schemaVersion = 1; runId = [Guid]::NewGuid().ToString('N'); vault = $VaultName; profile = $ProfileName; cleanupNames = @(); pendingNames = @() }
$directory = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $directory
$statePath = Join-Path $directory 'certificate-state.json'
$script:deletes = 0
$script:failDelete = $false
function Invoke-CertificateCli([string[]]$Arguments, [switch]$AllowNotFound) {
    if ($Arguments[0] -eq 'delete') {
        $script:deletes++
        if ($script:failDelete) { throw 'Synthetic cleanup failure' }
    }
    return $null
}
try {
    if (Assert-CertificateNativeResult 1 'The certificate file or password is invalid.' 'certificate file or password is invalid') { throw 'Expected failure was not recognized.' }
    $wrongFailureAccepted = $true
    try { $null = Assert-CertificateNativeResult 1 'Forbidden' 'certificate file or password is invalid' } catch { $wrongFailureAccepted = $false }
    if ($wrongFailureAccepted) { throw 'An unrelated failure was accepted as a password validation failure.' }
    $name = "clips-cert-cli-$($state.runId)"
    Register-TestCertificate $name
    Remove-RunCertificates -WhatIf
    if ($script:deletes -ne 0 -or $state.cleanupNames.Count -ne 1) { throw 'WhatIf changed ownership or sent a request.' }
    $bad = $state.Clone()
    $bad.cleanupNames = @('unowned-certificate')
    $rejected = $false
    try { Assert-CertificateState $bad } catch { $rejected = $true }
    if (-not $rejected) { throw 'Unowned certificate was accepted.' }
    $bad = $state.Clone()
    $bad.profile = 'other-profile'
    $rejected = $false
    try { Assert-CertificateState $bad } catch { $rejected = $true }
    if (-not $rejected) { throw 'Wrong profile was accepted.' }
    $script:failDelete = $true
    try { Remove-RunCertificates } catch { }
    if ($state.cleanupNames.Count -ne 1) { throw 'Failed cleanup lost ownership state.' }
    $script:failDelete = $false
    try { Remove-RunCertificates } catch { }
    if ($state.cleanupNames.Count -ne 1) { throw 'A missing certificate with pending issuance lost ownership state.' }
    $state.pendingNames = @()
    Save-CertificateState
    Remove-RunCertificates
    if ($state.cleanupNames.Count -ne 0) { throw 'Successful cleanup retained pending entries.' }
    & $script -Phase VerifyLocal
    $originalHome = $env:AZCLIPS_HOME
    try {
        $script:traceExists = $false
        $script:traceEnumerations = 0
        function Test-Path([string]$LiteralPath, [string]$PathType) {
            $script:checkedTracePath = $LiteralPath
            return $script:traceExists
        }
        function Get-ChildItem([string]$LiteralPath, [switch]$File, [switch]$Recurse) {
            $script:traceEnumerations++
            return [IO.FileInfo]::new((Join-Path $LiteralPath 'fixture.jsonl'))
        }
        $env:AZCLIPS_HOME = $null
        $files = @(Get-CertificateDiagnosticFiles)
        $expected = [IO.Path]::Combine(
            [Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile),
            [Core.Abstractions.HomePaths]::DefaultHomeDirectoryName,
            [Core.Abstractions.HomePaths]::TraceDirectoryName)
        if ($script:checkedTracePath -ne $expected -or $files.Count -ne 0 -or $script:traceEnumerations -ne 0) {
            throw 'Unset home must resolve the default trace path and tolerate a missing directory.'
        }
        $env:AZCLIPS_HOME = $directory
        $files = @(Get-CertificateDiagnosticFiles)
        if ($script:checkedTracePath -ne (Join-Path $directory 'traces') -or $files.Count -ne 0) {
            throw 'The explicit home override must be used for diagnostic files.'
        }
        $script:traceExists = $true
        $files = @(Get-CertificateDiagnosticFiles)
        if ($files.Count -ne 1 -or $script:traceEnumerations -ne 1) { throw 'Existing trace files were not enumerated.' }
        Write-Host 'PASS: default home, explicit home and missing diagnostic directory'
    }
    finally {
        $env:AZCLIPS_HOME = $originalHome
        Remove-Item Function:Test-Path, Function:Get-ChildItem
    }
    Write-Host 'PASS: certificate harness ownership, target binding, failure retention and local cmdlet checks'
}
finally { Remove-Item -LiteralPath $directory -Recurse -Force }