#requires -Version 7
[CmdletBinding()]
param(
    [string] $AzclipsExe = $env:AZCLIPS_EXE,
    [string] $OutputPath = (Join-Path ([IO.Path]::GetTempPath()) "azclips-agent-evaluation-$([guid]::NewGuid().ToString('N')).json"),
    [string] $TracePath = (Join-Path ([IO.Path]::GetTempPath()) "azclips-agent-evaluation-traces-$([guid]::NewGuid().ToString('N'))"),
    [ValidateSet('automatic', 'manual')]
    [string] $RecoveryMode = 'automatic',
    [ValidateRange(1, 2147483647)]
    [int] $Repeat = 1,
    [ValidateSet('refresh', 'existing')]
    [string] $ResourceCache = 'refresh',
    [switch] $Upload,
    [string] $ServiceUrl = 'https://azclis-evaluation-web-6b085460.azurewebsites.net'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

if ([string]::IsNullOrWhiteSpace($AzclipsExe) -or -not (Test-Path $AzclipsExe -PathType Leaf)) {
    throw 'Pass -AzclipsExe or set AZCLIPS_EXE to an installed azclips executable.'
}

$AzclipsExe = (Resolve-Path $AzclipsExe).Path
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$TracePath = [IO.Path]::GetFullPath($TracePath)
if (Test-Path -LiteralPath $OutputPath) {
    throw "Output already exists: $OutputPath. Choose a new path so a previous artifact cannot be uploaded as this run."
}
& node (Join-Path $PSScriptRoot 'judge\judge-azure.js') --check-config
if ($LASTEXITCODE -ne 0) {
    throw 'Semantic judge configuration is incomplete; no Agent evaluation was started. Set endpoint/deployment in Agent/eval/judge.config.local.json or configure AZURE_OPENAI_ENDPOINT and AZURE_OPENAI_DEPLOYMENT.'
}
$configuration = if ($RecoveryMode -eq 'manual') {
    [ordered]@{ 'agent.recovery' = 'manual' }
}
else {
    [ordered]@{
        'agent.enabled' = 'true'
        'agent.sessionStyle' = 'oneshot'
    }
}
$configuration['agent.trace.enabled'] = 'true'
$configuration['agent.trace.path'] = $TracePath

function Invoke-Azclips {
    param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)

    # Keep stderr diagnostics out of structured stdout such as agent tool env.
    $output = & $AzclipsExe @Arguments | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "azclips exited with $LASTEXITCODE while running: $($Arguments -join ' ')`n$output"
    }
    return $output
}

$runtimeEnvironment = Invoke-Azclips agent tool env | ConvertFrom-Json
$subscriptionId = [guid]::Empty
if ($runtimeEnvironment.signedIn -ne $true -or
    -not [guid]::TryParse($runtimeEnvironment.subscriptionId, [ref]$subscriptionId) -or
    $subscriptionId -eq [guid]::Empty) {
    throw 'The reference suite requires an Azure-signed-in active profile with a subscription.'
}

function Get-PackageFingerprint {
    param([string] $Root)

    # Framework-dependent packages keep product code beside a generic app host, so hash every file.
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
        $relativePath = [IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')
        $lines.Add("$relativePath`t$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())")
    }
    $lines.Sort([StringComparer]::Ordinal)
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    return [ordered]@{
        packageSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        packageFiles = $lines.Count
    }
}

function Get-LastJsonLine {
    param([string] $Text, [string] $Property)

    $lines = @($Text -split "\r?\n" | Where-Object { $_.Trim().StartsWith('{') -and $_.Trim().EndsWith('}') })
    [array]::Reverse($lines)
    foreach ($line in $lines) {
        try {
            $value = $line | ConvertFrom-Json
        }
        catch {
            continue
        }
        if ($null -ne $value.PSObject.Properties[$Property]) { return $value }
    }
    return $null
}

$targetVersion = [string]$runtimeEnvironment.azclipsVersion
if ([string]::IsNullOrWhiteSpace($targetVersion)) {
    throw 'agent tool env did not report azclipsVersion; the evaluated target build cannot be identified.'
}
$target = [ordered]@{ version = $targetVersion.Trim() }
$packageFingerprint = Get-PackageFingerprint (Split-Path -Parent $AzclipsExe)
$target.packageSha256 = $packageFingerprint.packageSha256
$target.packageFiles = $packageFingerprint.packageFiles

# Product lookups answer from the on-disk ARM cache and refresh it in the background.
# Refresh it synchronously first so every case sees the same near-real-time cache.
$fixtureGroup = if ([string]::IsNullOrWhiteSpace($env:AZCLIPS_EVALUATION_RESOURCE_GROUP)) {
    'azclips-test-rg'
}
else {
    $env:AZCLIPS_EVALUATION_RESOURCE_GROUP.Trim()
}
$resourceCacheState = [ordered]@{ mode = $ResourceCache }
if ($ResourceCache -eq 'refresh') {
    $refreshStarted = [DateTimeOffset]::UtcNow
    $refreshWatch = [Diagnostics.Stopwatch]::StartNew()
    $refreshStatus = Get-LastJsonLine (Invoke-Azclips agent tool resource refresh) 'status'
    $refreshWatch.Stop()
    if ($null -eq $refreshStatus -or $refreshStatus.status -ne 'ok') {
        throw 'The ARM resource cache refresh did not report status ok; no Agent evaluation was started.'
    }
    $lookup = Get-LastJsonLine (Invoke-Azclips agent tool resource find --name $fixtureGroup --kind resourceGroup --subscription-id $subscriptionId.ToString()) 'matchCount'
    $fixtureFound = $null -ne $lookup -and @($lookup.matches | Where-Object {
            $_.name -ieq $fixtureGroup -and $_.subscriptionId -ieq $subscriptionId.ToString()
        }).Count -gt 0
    if (-not $fixtureFound -or $lookup.cache.resources -le 0) {
        throw "The refreshed ARM cache does not contain fixture group '$fixtureGroup' in subscription $subscriptionId. Create or select the fixture group before evaluating."
    }
    $resourceCacheState.refreshStartedAtUtc = $refreshStarted.ToString('o')
    $resourceCacheState.refreshMs = $refreshWatch.ElapsedMilliseconds
    $resourceCacheState.resources = [long]$lookup.cache.resources
    $resourceCacheState.fixtureGroup = $fixtureGroup
    $resourceCacheState.fixtureGroupFound = $true
}

$configurationRoot = if ($env:AZCLIPS_HOME) {
    $env:AZCLIPS_HOME
}
else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.azclips'
}
$configurationPath = [IO.Path]::GetFullPath((Join-Path $configurationRoot 'config.json'), $PWD.ProviderPath)
$configurationExisted = Test-Path -LiteralPath $configurationPath
$configurationBackupPath = "$configurationPath.evaluation-$([guid]::NewGuid().ToString('N')).bak"
if ($configurationExisted) {
    try {
        [IO.File]::Copy($configurationPath, $configurationBackupPath)
    }
    catch {
        throw "Failed to back up Agent configuration '$configurationPath'; no temporary settings were applied: $($_.Exception.Message)"
    }
    Write-Host "Agent configuration backup: $configurationBackupPath"
}

$evaluationExitCode = 1
$previousAzclipsExe = $env:AZCLIPS_EXE
$previousPromptfooTelemetry = $env:PROMPTFOO_DISABLE_TELEMETRY
$previousPromptfooUpdate = $env:PROMPTFOO_DISABLE_UPDATE
$previousSignedIn = $env:AZCLIPS_EVALUATION_SIGNED_IN
$previousSubscriptionId = $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID
$previousRecoveryMode = $env:AZCLIPS_EVALUATION_RECOVERY_MODE
$previousTarget = $env:AZCLIPS_EVALUATION_TARGET
$previousResourceCache = $env:AZCLIPS_EVALUATION_RESOURCE_CACHE
$runFailure = $null
$restoreFailure = $null
try {
    foreach ($entry in $configuration.GetEnumerator()) {
        Invoke-Azclips config set --key $entry.Key --value $entry.Value | Out-Null
    }

    New-Item -ItemType Directory -Path (Split-Path $OutputPath) -Force | Out-Null
    New-Item -ItemType Directory -Path $TracePath -Force | Out-Null
    $env:AZCLIPS_EXE = $AzclipsExe
    $env:PROMPTFOO_DISABLE_TELEMETRY = 'true'
    $env:PROMPTFOO_DISABLE_UPDATE = 'true'
    $env:AZCLIPS_EVALUATION_SIGNED_IN = 'true'
    $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID = $subscriptionId.ToString()
    $env:AZCLIPS_EVALUATION_RECOVERY_MODE = $RecoveryMode
    $env:AZCLIPS_EVALUATION_TARGET = $target | ConvertTo-Json -Compress
    $env:AZCLIPS_EVALUATION_RESOURCE_CACHE = $resourceCacheState | ConvertTo-Json -Compress

    Push-Location $PSScriptRoot
    try {
        & npx --no-install promptfoo eval `
            --config promptfooconfig.js `
            --no-cache `
            --repeat $Repeat `
            --output $OutputPath
        $evaluationExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
catch {
    $runFailure = $_
}
finally {
    try {
        if ($configurationExisted) {
            [IO.File]::Move($configurationBackupPath, $configurationPath, $true)
        }
        else {
            [IO.File]::Delete($configurationPath)
        }
    }
    catch {
        $restoreFailure = "Failed to restore Agent configuration '$configurationPath': $($_.Exception.Message)"
        if ($configurationExisted) {
            $restoreFailure += " Backup path: '$configurationBackupPath'. Restore it before running evaluation again."
        }
        else {
            $restoreFailure += " The file did not exist before this run; remove '$configurationPath' to restore the original state."
        }
    }
    $env:AZCLIPS_EXE = $previousAzclipsExe
    $env:PROMPTFOO_DISABLE_TELEMETRY = $previousPromptfooTelemetry
    $env:PROMPTFOO_DISABLE_UPDATE = $previousPromptfooUpdate
    $env:AZCLIPS_EVALUATION_SIGNED_IN = $previousSignedIn
    $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID = $previousSubscriptionId
    $env:AZCLIPS_EVALUATION_RECOVERY_MODE = $previousRecoveryMode
    $env:AZCLIPS_EVALUATION_TARGET = $previousTarget
    $env:AZCLIPS_EVALUATION_RESOURCE_CACHE = $previousResourceCache
}

if ($runFailure -or $restoreFailure) {
    $messages = @()
    if ($runFailure) { $messages += $runFailure.Exception.Message }
    if ($restoreFailure) { $messages += $restoreFailure }
    throw ($messages -join "`n")
}

if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
    throw "Promptfoo did not produce an evaluation artifact (exit code $evaluationExitCode): $OutputPath"
}
try {
    $artifact = Get-Content -LiteralPath $OutputPath -Raw | ConvertFrom-Json -Depth 100
}
catch {
    throw "Promptfoo produced invalid JSON: $($_.Exception.Message)"
}
if ($null -eq $artifact.results -or $artifact.results.results -isnot [array] -or $artifact.results.results.Count -eq 0) {
    throw 'Promptfoo artifact contains no evaluation results.'
}

Write-Host "Evaluation artifact: $OutputPath"
if ($Upload) {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
    Push-Location $repositoryRoot
    try {
        $uploadOutput = & node (Join-Path $PSScriptRoot 'upload-result.js') `
            --artifact $OutputPath `
            --service-url $ServiceUrl
        if ($LASTEXITCODE -ne 0) {
            throw "Uploading the evaluation artifact failed with exit code $LASTEXITCODE."
        }
        $uploadResult = $uploadOutput | ConvertFrom-Json
        Write-Host "Evaluation result: $($uploadResult.dashboardUrl)"
    }
    finally {
        Pop-Location
    }
}

exit $evaluationExitCode