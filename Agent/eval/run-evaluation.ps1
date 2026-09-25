#requires -Version 7
[CmdletBinding()]
param(
    [string] $AzclipsExe = $env:AZCLIPS_EXE,
    [string] $OutputPath = (Join-Path ([IO.Path]::GetTempPath()) "azclips-agent-evaluation-$([guid]::NewGuid().ToString('N')).json"),
    [string] $TracePath = (Join-Path ([IO.Path]::GetTempPath()) "azclips-agent-evaluation-traces-$([guid]::NewGuid().ToString('N'))"),
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
$configuration = [ordered]@{
    'agent.enabled' = 'true'
    'agent.sessionStyle' = 'oneshot'
    'agent.trace.enabled' = 'true'
    'agent.trace.path' = $TracePath
}

function Invoke-Azclips {
    param([Parameter(ValueFromRemainingArguments)][string[]] $Arguments)

    $output = & $AzclipsExe @Arguments 2>&1 | Out-String
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

    Push-Location $PSScriptRoot
    try {
        & npx --no-install promptfoo eval `
            --config promptfooconfig.js `
            --no-cache `
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