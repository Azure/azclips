[CmdletBinding(SupportsShouldProcess)]
param(
    [ValidateSet('VerifyLocal', 'Live', 'Cleanup')][string]$Phase = 'VerifyLocal',
    [string]$VaultName,
    [Alias('Profile')][string]$ProfileName,
    [string]$CliAssembly,
    [string]$EvidenceDirectory,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$state = $null
$statePath = $null
$checks = [Collections.Generic.List[object]]::new()
$sensitiveMarkers = [Collections.Generic.List[string]]::new()

function Assert-CertificateState($Loaded) {
    if ($Loaded.schemaVersion -ne 1 -or $Loaded.vault -cne $VaultName -or
        $Loaded.profile -cne $ProfileName -or $Loaded.runId -notmatch '^[a-f0-9]{32}$') {
        throw 'Certificate test state does not match the requested vault/profile or schema.'
    }
    $allowed = @("clips-cert-cli-$($Loaded.runId)", "clips-cert-ps-$($Loaded.runId)")
    foreach ($name in @($Loaded.cleanupNames) + @($Loaded.pendingNames)) {
        if ($name -cnotin $allowed) { throw 'Refusing cleanup of a certificate not owned by this run.' }
    }
}

function Save-CertificateState {
    $state | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $statePath -Encoding utf8
}

function Assert-CertificateNativeResult([int]$Code, [string]$ErrorText, [string]$ExpectedError, [switch]$AllowNotFound) {
    if ($ExpectedError) {
        if ($Code -eq 0 -or $ErrorText -notmatch $ExpectedError) { throw 'The command did not fail for the expected reason.' }
        return $false
    }
    if ($Code -ne 0) {
        if ($AllowNotFound -and $ErrorText -match '\b404\b') { return $false }
        throw "Certificate CLI command failed with exit code $Code. Output omitted; verify permissions and the selected build."
    }
    return $true
}

function Invoke-CertificateCli([string[]]$Arguments, [switch]$AllowNotFound, [string]$ExpectedError) {
    $start = [Diagnostics.ProcessStartInfo]::new($Dotnet)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in (@($CliAssembly, 'keyvault', 'certificate') + $Arguments + @('--vault-name', $VaultName, '--profile', $ProfileName, '--output', 'json'))) {
        $start.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(360000)) { $process.Kill($true); throw 'Certificate CLI test timed out.' }
        $output = $stdout.GetAwaiter().GetResult()
        $errorText = $stderr.GetAwaiter().GetResult()
        foreach ($marker in $sensitiveMarkers) {
            if ($output.Contains($marker) -or $errorText.Contains($marker)) { throw 'Sensitive fixture material appeared in CLI output or diagnostics.' }
        }
        if (-not (Assert-CertificateNativeResult $process.ExitCode $errorText $ExpectedError -AllowNotFound:$AllowNotFound)) { return $null }
        return ConvertFrom-Json -InputObject $output -AsHashtable -NoEnumerate
    }
    finally { $process.Dispose() }
}

function Register-TestCertificate([string]$Name) {
    Assert-CertificateState $state
    if ($Name -cnotin @("clips-cert-cli-$($state.runId)", "clips-cert-ps-$($state.runId)")) { throw 'Unexpected certificate test name.' }
    if (Invoke-CertificateCli @('show', '--name', $Name) -AllowNotFound) { throw 'Test name already exists; refusing to modify it.' }
    $state.cleanupNames = @($state.cleanupNames) + $Name
    $state.pendingNames = @($state.pendingNames) + $Name
    Save-CertificateState
}

function Remove-RunCertificates {
    [CmdletBinding(SupportsShouldProcess)]
    param()
    $loaded = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json -AsHashtable
    Assert-CertificateState $loaded
    $remaining = @()
    foreach ($name in @($loaded.cleanupNames)) {
        if (-not $PSCmdlet.ShouldProcess("$VaultName/$name", 'Soft-delete run-owned test certificate')) { $remaining += $name; continue }
        try {
            $deleted = Invoke-CertificateCli @('delete', '--name', $name, '--yes') -AllowNotFound
            if ($null -eq $deleted -and $name -cin $loaded.pendingNames) { throw 'Issuance may still be pending.' }
        }
        catch { $remaining += $name; Write-Warning "Cleanup remains pending for $name." }
    }
    $state.cleanupNames = $remaining
    $state.pendingNames = @($loaded.pendingNames | Where-Object { $_ -cin $remaining })
    Save-CertificateState
    if ($remaining.Count -and -not $WhatIfPreference) { throw 'Certificate cleanup incomplete. Rerun Cleanup with the same evidence directory, vault and profile. Nothing was purged.' }
}

function Assert-CertificateCheck([string]$Name, [bool]$Condition) {
    if (-not $Condition) { throw "FAIL: $Name" }
    $checks.Add(@{ name = $Name; result = 'pass'; time = [DateTimeOffset]::UtcNow.ToString('O') })
    if ($statePath) { $checks | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'checks.json') -Encoding utf8 }
    Write-Host "PASS: $Name"
}

Import-Module (Join-Path $repoRoot 'src/PS/Accounts/Accounts.psd1')
Import-Module (Join-Path $repoRoot 'src/PS/KeyVault/KeyVault.psd1')

if ($Phase -eq 'VerifyLocal') {
    Assert-CertificateCheck 'Seven certificate cmdlets exported' (@(Get-Command -Module KeyVault -Name '*KeyVaultCertificate*').Count -eq 7)
    $destination = Join-Path ([IO.Path]::GetTempPath()) ("clips-whatif-" + [Guid]::NewGuid().ToString('N') + '.cer')
    Add-AzKeyVaultCertificate -VaultName fixture -Name test -Policy '{}' -WhatIf
    Import-AzKeyVaultCertificate -VaultName fixture -Name test -FilePath missing.pfx -WhatIf
    Update-AzKeyVaultCertificate -VaultName fixture -Name test -Enabled $false -WhatIf
    Export-AzKeyVaultCertificate -VaultName fixture -Name test -FilePath $destination -WhatIf
    Remove-AzKeyVaultCertificate -VaultName fixture -Name test -WhatIf
    $recovery = Get-Command -Module KeyVault -Name Undo-AzKeyVaultCertificateRemoval
    Assert-CertificateCheck 'Recovery supports WhatIf and Confirm' ($recovery.Parameters.ContainsKey('WhatIf') -and $recovery.Parameters.ContainsKey('Confirm'))
    $preview = @(Undo-AzKeyVaultCertificateRemoval -VaultName fixture -Name test -Profile 'whatif-profile-does-not-exist' -WhatIf -ErrorAction Stop)
    Assert-CertificateCheck 'Recovery WhatIf returns no service output' ($preview.Count -eq 0)
    Assert-CertificateCheck 'WhatIf creates no download file' (-not (Test-Path -LiteralPath $destination))
    Assert-CertificateCheck 'Get has List, Get and Versions parameter sets' (((Get-Command Get-AzKeyVaultCertificate).ParameterSets.Name | Sort-Object) -join ',' -eq 'Get,List,Versions')
    return
}

if (-not $VaultName -or -not $ProfileName -or -not $CliAssembly -or -not $EvidenceDirectory) {
    throw 'Live/Cleanup requires VaultName, ProfileName, CliAssembly and EvidenceDirectory. The profile needs certificate data-plane permissions.'
}
if (-not $PSCmdlet.ShouldProcess($VaultName, "$Phase certificate test workflow")) { return }
$statePath = Join-Path $EvidenceDirectory 'certificate-state.json'
if ($Phase -eq 'Cleanup') {
    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json -AsHashtable
    Assert-CertificateState $state
    Remove-RunCertificates
    return
}
if (Test-Path -LiteralPath $EvidenceDirectory) { throw 'Use a new evidence directory for a live run.' }
$null = New-Item -ItemType Directory -Path $EvidenceDirectory
if (-not $IsWindows) { [IO.File]::SetUnixFileMode($EvidenceDirectory, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite -bor [IO.UnixFileMode]::UserExecute) }
$state = @{ schemaVersion = 1; runId = [Guid]::NewGuid().ToString('N'); vault = $VaultName; profile = $ProfileName; cleanupNames = @(); pendingNames = @() }
Save-CertificateState
$cliName = "clips-cert-cli-$($state.runId)"
$psName = "clips-cert-ps-$($state.runId)"
$privateFile = Join-Path $EvidenceDirectory 'synthetic.pem'
$pfxFile = Join-Path $EvidenceDirectory 'synthetic.pfx'
$encryptedPemFile = Join-Path $EvidenceDirectory 'encrypted.pem'
$passwordFile = Join-Path $EvidenceDirectory 'password.txt'
$wrongPasswordFile = Join-Path $EvidenceDirectory 'wrong-password.txt'
$policyFile = Join-Path $EvidenceDirectory 'policy.json'
$password = 'clips-cert-password-' + [Guid]::NewGuid().ToString('N')
$sensitiveMarkers.Add($password)
$sensitiveMarkers.Add('-----BEGIN PRIVATE KEY-----')
$sensitiveMarkers.Add('-----BEGIN ENCRYPTED PRIVATE KEY-----')
$securePassword = ConvertTo-SecureString $password -AsPlainText -Force
$wrongSecurePassword = ConvertTo-SecureString 'synthetic-wrong-password' -AsPlainText -Force
$policy = '{"issuer":{"name":"Self"},"key_props":{"kty":"RSA","key_size":2048,"exportable":false},"secret_props":{"contentType":"application/x-pkcs12"},"x509_props":{"subject":"CN=clips-fixture","validity_months":1}}'
try {
    $null = Invoke-CertificateCli @('list')
    Register-TestCertificate $cliName
    $created = Invoke-CertificateCli @('create', '--name', $cliName, '--policy', $policy, '--timeout', '300')
    Assert-CertificateCheck 'CLI create completed' ($created.status -ieq 'completed')
    $state.pendingNames = @($state.pendingNames | Where-Object { $_ -cne $cliName })
    Save-CertificateState
    $shown = Invoke-CertificateCli @('show', '--name', $cliName)
    Assert-CertificateCheck 'CLI show returns public bytes' ([bool]$shown.cer)
    $firstVersion = ([string]$shown.id).Split('/')[-1]
    $state.pendingNames = @($state.pendingNames) + $cliName
    Save-CertificateState
    $created = Invoke-CertificateCli @('create', '--name', $cliName, '--policy', $policy)
    Assert-CertificateCheck 'CLI second version completed' ($created.status -ieq 'completed')
    $state.pendingNames = @($state.pendingNames | Where-Object { $_ -cne $cliName })
    Save-CertificateState
    $versions = Invoke-CertificateCli @('list-versions', '--name', $cliName, '--maxresults', '1')
    Assert-CertificateCheck 'CLI versions spans pages' (@($versions).Count -ge 2)
    $old = Invoke-CertificateCli @('show', '--name', $cliName, '--version', $firstVersion)
    Assert-CertificateCheck 'CLI explicit version returns original certificate' ($old.id -ceq $shown.id -and $old.cer -ceq $shown.cer)
    $null = Invoke-CertificateCli @('delete', '--name', $cliName) -ExpectedError 'Confirm with --yes'
    Assert-CertificateCheck 'CLI deletion requires confirmation' $true
    $afterRefusedDelete = Invoke-CertificateCli @('show', '--name', $cliName)
    Assert-CertificateCheck 'Unconfirmed deletion leaves certificate active' ([bool]$afterRefusedDelete.id)
    $updated = Invoke-CertificateCli @('set-attributes', '--name', $cliName, '--enabled', 'false')
    Assert-CertificateCheck 'CLI update preserves explicit false' ($updated.attributes.enabled -eq $false)
    $null = Invoke-CertificateCli @('set-attributes', '--name', $cliName, '--enabled', 'true')
    $null = Invoke-CertificateCli @('download', '--name', $cliName, '--file', (Join-Path $EvidenceDirectory 'cli.cer'), '--encoding', 'DER')
    $downloaded = [Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificateFromFile((Join-Path $EvidenceDirectory 'cli.cer'))
    try { Assert-CertificateCheck 'CLI download has no private key' (-not $downloaded.HasPrivateKey) } finally { $downloaded.Dispose() }
    $downloadHash = (Get-FileHash -LiteralPath (Join-Path $EvidenceDirectory 'cli.cer')).Hash
    $null = Invoke-CertificateCli @('download', '--name', $cliName, '--file', (Join-Path $EvidenceDirectory 'cli.cer')) -ExpectedError 'destination already exists'
    Assert-CertificateCheck 'CLI refuses overwrite without changing file' ((Get-FileHash -LiteralPath (Join-Path $EvidenceDirectory 'cli.cer')).Hash -ceq $downloadHash)
    [IO.File]::WriteAllText($policyFile, '{"x509_props":{"validity_months":2}}')
    $policyUpdated = Invoke-CertificateCli @('set-attributes', '--name', $cliName, '--policy-file', $policyFile)
    Assert-CertificateCheck 'CLI policy-only update returns updated policy' ($policyUpdated.policy.x509_props.validity_months -eq 2)

    Register-TestCertificate $psName
    $psCreated = Add-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Policy $policy -Confirm:$false
    Assert-CertificateCheck 'PowerShell create completed' ($psCreated.Status -ieq 'completed')
    $state.pendingNames = @($state.pendingNames | Where-Object { $_ -cne $psName })
    Save-CertificateState
    $key = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=clips-import-fixture', $key, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-1), [DateTimeOffset]::UtcNow.AddDays(2))
        try {
            [IO.File]::WriteAllText($privateFile, $certificate.ExportCertificatePem() + "`n" + $key.ExportPkcs8PrivateKeyPem())
            $pfxBytes = $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $password)
            [IO.File]::WriteAllBytes($pfxFile, $pfxBytes)
            $sensitiveMarkers.Add([Convert]::ToBase64String($pfxBytes))
            [Security.Cryptography.CryptographicOperations]::ZeroMemory($pfxBytes)
            $pbe = [Security.Cryptography.PbeParameters]::new([Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc, [Security.Cryptography.HashAlgorithmName]::SHA256, 1000)
            [IO.File]::WriteAllText($encryptedPemFile, $certificate.ExportCertificatePem() + "`n" + $key.ExportEncryptedPkcs8PrivateKeyPem($password, $pbe))
            $expectedThumbprint = $certificate.Thumbprint
        }
        finally { $certificate.Dispose() }
    }
    finally { $key.Dispose() }
    [IO.File]::WriteAllText($passwordFile, $password)
    [IO.File]::WriteAllText($wrongPasswordFile, 'synthetic-wrong-password')
    $null = Invoke-CertificateCli @('import', '--name', $cliName, '--file', $privateFile)
    $null = Import-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -FilePath $privateFile -Confirm:$false
    Assert-CertificateCheck 'Both frontends import unencrypted PEM' $true
    $beforeRejectedImport = Invoke-CertificateCli @('show', '--name', $cliName)
    $null = Invoke-CertificateCli @('import', '--name', $cliName, '--file', $pfxFile, '--password-file', $wrongPasswordFile) -ExpectedError 'certificate file or password is invalid'
    $afterRejectedImport = Invoke-CertificateCli @('show', '--name', $cliName)
    Assert-CertificateCheck 'CLI wrong PFX password creates no version' ($beforeRejectedImport.id -ceq $afterRejectedImport.id)
    $rejected = $false
    try { $null = Import-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -FilePath $pfxFile -Password $wrongSecurePassword -Confirm:$false -ErrorAction Stop }
    catch { $rejected = $_.ToString() -match 'certificate file or password is invalid' }
    Assert-CertificateCheck 'PowerShell rejects wrong PFX password for the expected reason' $rejected
    $pfxImported = Invoke-CertificateCli @('import', '--name', $cliName, '--file', $pfxFile, '--password-file', $passwordFile, '--debug')
    Assert-CertificateCheck 'CLI imports encrypted PFX' ([bool]$pfxImported.cer)
    $psPfx = Import-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -FilePath $pfxFile -Password $securePassword -Confirm:$false
    Assert-CertificateCheck 'PowerShell imports encrypted PFX' ([bool]$psPfx.Cer)
    $pemImported = Invoke-CertificateCli @('import', '--name', $cliName, '--file', $encryptedPemFile, '--password-file', $passwordFile, '--debug')
    Assert-CertificateCheck 'CLI imports encrypted PEM' ([bool]$pemImported.cer)
    $psPem = Import-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -FilePath $encryptedPemFile -Password $securePassword -Confirm:$false
    Assert-CertificateCheck 'PowerShell imports encrypted PEM' ([bool]$psPem.Cer)
    $psVersion = ([string]$psPem.Id).Split('/')[-1]
    $psExplicit = Get-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Version $psVersion
    Assert-CertificateCheck 'PowerShell explicit version matches imported certificate' ($psExplicit.Id -ceq $psPem.Id)
    Assert-CertificateCheck 'PowerShell show returns public bytes' ([bool](Get-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName).Cer)
    Assert-CertificateCheck 'PowerShell versions spans pages' (@(Get-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -IncludeVersions -MaxResults 1).Count -ge 2)
    Assert-CertificateCheck 'PowerShell list includes owned certificate with pending false' ([bool](@(Get-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -MaxResults 1 -IncludePending $false) | Where-Object Id -Like "*/$psName"))
    $cliList = Invoke-CertificateCli @('list', '--maxresults', '1', '--include-pending', 'false')
    Assert-CertificateCheck 'CLI list spans pages with explicit include-pending false' (@($cliList).Count -ge 2)
    $beforeWhatIf = Get-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName
    Update-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Enabled $false -WhatIf
    Remove-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -WhatIf
    $afterWhatIf = Get-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName
    Assert-CertificateCheck 'PowerShell WhatIf preserves live certificate state' ($beforeWhatIf.Id -ceq $afterWhatIf.Id -and $beforeWhatIf.AttributesEnabled -eq $afterWhatIf.AttributesEnabled)
    $psUpdated = Update-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Enabled $false -Tag @{} -Confirm:$false
    Assert-CertificateCheck 'PowerShell explicit false and empty tags' ($psUpdated.AttributesEnabled -eq $false -and $psUpdated.Tags.Count -eq 0)
    $null = Update-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Enabled $true -Confirm:$false
    $psPolicy = Update-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Policy '{"x509_props":{"validity_months":3}}' -Confirm:$false
    Assert-CertificateCheck 'PowerShell policy-only update returns updated policy' ($psPolicy.Policy.X509CertificatePropertiesValidityInMonths -eq 3)
    $null = Export-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -FilePath (Join-Path $EvidenceDirectory 'ps.pem') -Confirm:$false
    $exported = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([IO.File]::ReadAllText((Join-Path $EvidenceDirectory 'ps.pem')))
    try { Assert-CertificateCheck 'PowerShell PEM export matches import without private key' ($exported.Thumbprint -ceq $expectedThumbprint -and -not $exported.HasPrivateKey) }
    finally { $exported.Dispose() }
    $diagnosticFiles = @(Get-ChildItem -LiteralPath (Join-Path $env:AZCLIPS_HOME 'traces') -File -Recurse)
    foreach ($diagnostic in $diagnosticFiles) {
        $text = [IO.File]::ReadAllText($diagnostic.FullName)
        foreach ($marker in $sensitiveMarkers) { if ($text.Contains($marker)) { throw 'Sensitive certificate fixture material appeared in persisted diagnostics.' } }
    }
    Assert-CertificateCheck "No fixture leakage in $($diagnosticFiles.Count) diagnostic files" $true
    $deleted = Remove-AzKeyVaultCertificate -VaultName $VaultName -Profile $ProfileName -Name $psName -Confirm:$false
    Assert-CertificateCheck 'PowerShell deletion returns recovery metadata' ([bool]$deleted.RecoveryId)
    $deleted = Invoke-CertificateCli @('delete', '--name', $cliName, '--yes')
    Assert-CertificateCheck 'CLI deletion returns recovery metadata' ([bool]$deleted.recoveryId)
    Assert-CertificateCheck 'Deleted CLI certificate is absent' ($null -eq (Invoke-CertificateCli @('show', '--name', $cliName) -AllowNotFound))
    Assert-CertificateCheck 'Deleted PowerShell certificate is absent' ($null -eq (Invoke-CertificateCli @('show', '--name', $psName) -AllowNotFound))
}
finally {
    $securePassword.Dispose()
    $wrongSecurePassword.Dispose()
    foreach ($fixture in @($privateFile, $pfxFile, $encryptedPemFile, $passwordFile, $wrongPasswordFile)) {
        if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture -Force }
    }
    Remove-RunCertificates
}