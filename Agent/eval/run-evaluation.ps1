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

function Get-ConfigurationSnapshot([string] $Key) {
    $entry = Invoke-Azclips config get --key $Key --json | ConvertFrom-Json
    $value = if ($null -ne $entry.Value) { $entry.Value } else { $entry.value }
    $source = if ($null -ne $entry.Source) { $entry.Source } else { $entry.source }
    return [pscustomobject]@{
        Value = if ($null -eq $value) { $null } else { $value.ToString() }
        WasDefault = [string]::IsNullOrWhiteSpace($source) -or
            -not $source.Contains('config', [StringComparison]::OrdinalIgnoreCase)
    }
}

$runtimeEnvironment = Invoke-Azclips agent tool env | ConvertFrom-Json
$subscriptionId = [guid]::Empty
if ($runtimeEnvironment.signedIn -ne $true -or
    -not [guid]::TryParse($runtimeEnvironment.subscriptionId, [ref]$subscriptionId) -or
    $subscriptionId -eq [guid]::Empty) {
    throw 'The reference suite requires an Azure-signed-in active profile with a subscription.'
}

$snapshots = @{}
$evaluationExitCode = 1
$previousAzclipsExe = $env:AZCLIPS_EXE
$previousPromptfooTelemetry = $env:PROMPTFOO_DISABLE_TELEMETRY
$previousPromptfooUpdate = $env:PROMPTFOO_DISABLE_UPDATE
$previousSignedIn = $env:AZCLIPS_EVALUATION_SIGNED_IN
$previousSubscriptionId = $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID
$runFailure = $null
$restoreFailures = [Collections.Generic.List[string]]::new()
try {
    foreach ($entry in $configuration.GetEnumerator()) {
        $snapshots[$entry.Key] = Get-ConfigurationSnapshot $entry.Key
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
    foreach ($entry in $configuration.GetEnumerator()) {
        $snapshot = $snapshots[$entry.Key]
        if ($null -eq $snapshot) { continue }
        try {
            if ($snapshot.WasDefault -or $null -eq $snapshot.Value) {
                Invoke-Azclips config delete --key $entry.Key | Out-Null
            }
            else {
                Invoke-Azclips config set --key $entry.Key --value $snapshot.Value | Out-Null
            }
        }
        catch {
            $restoreFailures.Add("$($entry.Key): $($_.Exception.Message)")
        }
    }
    $env:AZCLIPS_EXE = $previousAzclipsExe
    $env:PROMPTFOO_DISABLE_TELEMETRY = $previousPromptfooTelemetry
    $env:PROMPTFOO_DISABLE_UPDATE = $previousPromptfooUpdate
    $env:AZCLIPS_EVALUATION_SIGNED_IN = $previousSignedIn
    $env:AZCLIPS_EVALUATION_SUBSCRIPTION_ID = $previousSubscriptionId
}

if ($runFailure -or $restoreFailures.Count -gt 0) {
    $messages = @()
    if ($runFailure) { $messages += $runFailure.Exception.Message }
    if ($restoreFailures.Count -gt 0) {
        $messages += "Failed to restore Agent configuration: $($restoreFailures -join '; ')"
    }
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