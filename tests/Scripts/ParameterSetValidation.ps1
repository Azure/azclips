# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

<#
.SYNOPSIS
Invokes every exposed CLI command or PowerShell cmdlet without required parameters.
.DESCRIPTION
Build the solution first. The launcher creates an isolated home and starts a fresh
pwsh process with the Tests.Common startup hook. Real command routing and argument
binding run, but the hook stops accepted inputs before processor execution. No
login, browser, ARM request, secret access or resource mutation is performed.

Metadata probes cover every dispatch entry. Public probes cover every distinct
CLI invocation or exported cmdlet, including unchanged PowerShell counterparts.
The reports distinguish set rejection, native PS binding and accepted-but-blocked
input; an arbitrary nonzero exit is never considered a passing validation result.
.EXAMPLE
pwsh -NoProfile -File .\tests\Scripts\CLI\ParameterSetValidation.ps1
.EXAMPLE
pwsh -NoProfile -File .\tests\Scripts\PS\ParameterSetValidation.ps1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('CLI', 'PowerShell')]
    [string] $Client,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\parameter-set-validation'),
    [switch] $Worker
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $outputRoot -Force

function Invoke-ProbeProcess([System.Diagnostics.ProcessStartInfo] $StartInfo, [int] $TimeoutSeconds) {
    $StartInfo.UseShellExecute = $false
    $StartInfo.RedirectStandardOutput = $true
    $StartInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($StartInfo)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Validation process timed out: $($StartInfo.FileName) $($StartInfo.ArgumentList -join ' ')"
        }
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            StdOut = $stdout.GetAwaiter().GetResult()
            StdErr = $stderr.GetAwaiter().GetResult()
        }
    }
    finally { $process.Dispose() }
}

if (-not $Worker) {
    $hook = Join-Path $repoRoot 'tests\Common\bin\Debug\net10.0\Tests.Common.dll'
    if (-not (Test-Path -LiteralPath $hook)) { throw 'Build azclips.slnx first; the validation startup hook is missing.' }
    $validationHome = Join-Path ([System.IO.Path]::GetTempPath()) "azclips-parameter-validation-$([guid]::NewGuid().ToString('N'))"
    $null = New-Item -ItemType Directory -Path $validationHome
    try {
        @{
            telemetry = @{ enabled = $false }
            agent = @{ recovery = @{ trigger = 'off' } }
        } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $validationHome 'config.json') -Encoding utf8
        @{
            validation = @{
                name = 'validation'; subscriptionId = '00000000-0000-0000-0000-000000000001'
                tenantId = '00000000-0000-0000-0000-000000000000'; cloudName = 'AzureCloud'; authMethod = 0
            }
        } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $validationHome 'profiles.json') -Encoding utf8
        Set-Content (Join-Path $validationHome 'active-profile') 'validation' -NoNewline
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new((Get-Command pwsh).Source)
        foreach ($argument in @('-NoProfile', '-NonInteractive', '-File', $PSCommandPath, '-Client', $Client, '-OutputDirectory', $outputRoot, '-Worker')) {
            $startInfo.ArgumentList.Add($argument)
        }
        $startInfo.Environment['AZCLIPS_HOME'] = $validationHome
        $startInfo.Environment['AZCLIPS_TEST_PARAMETER_VALIDATION'] = '1'
        $startInfo.Environment['DOTNET_STARTUP_HOOKS'] = $hook
        $null = $startInfo.Environment.Remove('AZCLIPS_TEST_RECORDING')
        $probe = Invoke-ProbeProcess $startInfo 600
        Write-Host $probe.StdOut
        if ($probe.StdErr) { Write-Host $probe.StdErr }
        if ($probe.ExitCode -ne 0) { throw "$Client validation failed (exit $($probe.ExitCode)). See $outputRoot." }
    }
    finally { Remove-Item -LiteralPath $validationHome -Recurse -Force }
    return
}

if ($env:AZCLIPS_TEST_PARAMETER_VALIDATION_ACTIVE -ne '1') {
    throw 'The validation startup hook is not active. Refusing to invoke commands.'
}

function Get-ValidationPath([string] $Message) {
    if ($Message -match 'AZCLIPS_VALIDATION_ACCEPTED_EXECUTION_BLOCKED') { return 'BindingAcceptedExecutionBlocked' }
    if ($Message -match 'Invalid parameter set:') { return 'ParameterSetRejected' }
    if ($Message -match 'MissingMandatoryParameter|missing mandatory parameters') { return 'PowerShellMandatoryBinding' }
    if ($Message -match 'AmbiguousParameterSet|Parameter set cannot be resolved') { return 'PowerShellAmbiguousBinding' }
    return 'Unexpected'
}

$pipeline = [powershell]::Create()
try {
    $moduleRoot = Join-Path $repoRoot 'src\PS'
    $manifests = @((Join-Path $moduleRoot 'Accounts\Accounts.psd1')) + @(
        Get-ChildItem -LiteralPath $moduleRoot -Filter '*.psd1' -Recurse |
            Where-Object BaseName -NotIn @('Az', 'Accounts') |
            ForEach-Object FullName
    )
    foreach ($manifest in $manifests) {
        $null = $pipeline.AddCommand('Import-Module').AddParameter('Name', $manifest)
        $null = $pipeline.Invoke()
        if ($pipeline.HadErrors) { throw "Module import failed: $($pipeline.Streams.Error | Out-String)" }
        $pipeline.Commands.Clear()
    }

    $extensions = @(
        Get-ChildItem -LiteralPath (Join-Path $repoRoot 'src\Extensions') -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ExtensionIndex.cs') } |
            ForEach-Object Name
    ) + @('Agent')
    $indexes = foreach ($extension in $extensions) {
        $assemblyName = "Microsoft.Azure.Azclips.Extensions.$extension"
        $assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $repoRoot "artifacts\Debug\$assemblyName\$assemblyName.dll"))
        [pscustomobject]@{
            Extension = $extension
            Index = [System.Activator]::CreateInstance($assembly.GetType("$assemblyName.ExtensionIndex", $true))
        }
    }
    $clientType = [Core.Abstractions.ClientType]::$Client
    $logging = [Core.Runtime.ConsoleLoggingService]::new()
    if ($Client -eq 'CLI') {
        $cliRoot = Join-Path $repoRoot 'artifacts\Debug\Core.Cli'
        $cli = Join-Path $cliRoot $(if ($IsWindows) { 'az.exe' } else { 'az' })
        $null = [System.Reflection.Assembly]::LoadFrom((Join-Path $cliRoot 'az.dll'))
        $provider = [Core.Cli.CLIArgumentParsingProvider]::new([Core.Cli.PropertyParser]::new($logging), $logging)
    }
    else { $provider = [Microsoft.Azure.Azclips.Cmdlets.Accounts.PSArgumentParsingProvider]::new() }
    $binder = [Core.ArgumentParsing.ArgumentParsingService]::new(
        $provider, [Core.Runtime.ProfileService]::new(
            [Core.Runtime.ScopedLoggingService]::new(), [Core.Runtime.Persistence.FilePersistenceStore]::new()))
    $profile = [Core.Abstractions.Profile]::new()
    $profile.SubscriptionId = '00000000-0000-0000-0000-000000000001'
    $results = [System.Collections.Generic.List[object]]::new()
    $inventory = [System.Collections.Generic.List[object]]::new()

    foreach ($entry in $indexes) {
        $commands = @($entry.Index.GetAllCommands($clientType))
        foreach ($metadata in $commands) {
            $required = @($metadata.Parameters.Values | Where-Object IsRequired | ForEach-Object InvocationName)
            $sets = @(
                if ($null -ne $metadata.ParameterSets) {
                    foreach ($set in $metadata.ParameterSets.Values) {
                        [pscustomobject]@{
                            Name = $set.Name; IsDefault = $set.IsDefault
                            Required = @($set.Parameters.Values | Where-Object IsRequired | ForEach-Object Name)
                        }
                    }
                }
            )
            $inventory.Add([pscustomobject]@{
                Extension = $entry.Extension; Command = $metadata.InvocationName
                RequiredAtParameterLevel = $required; ParameterSets = $sets
            })
            $hasSets = $sets.Count -gt 0
            $acceptsEmpty = -not $hasSets -or @($sets | Where-Object { $_.Required.Count -eq 0 }).Count -gt 0
            $expected = if (-not $hasSets) { 'NoParameterSetValidation' } elseif ($acceptsEmpty) { 'BindingAccepted' } else { 'ParameterSetRejected' }
            $actual = if ($hasSets) { 'BindingAccepted' } else { 'NoParameterSetValidation' }
            $message = 'Binding accepted with a synthetic subscription profile. Processor not executed.'
            try {
                $arguments = [System.Collections.Generic.Dictionary[string, object]]::new()
                $null = $binder.BindArgumentsToProcessor(
                    [Core.Runtime.CommandLoadingExtension]::GetInstance($metadata), $arguments, $metadata, $profile)
            }
            catch [System.Management.Automation.MethodInvocationException] {
                $message = $_.Exception.InnerException.Message
                $actual = Get-ValidationPath $message
            }
            $results.Add([pscustomobject]@{
                Extension = $entry.Extension; Layer = 'MetadataBinding'; Command = $metadata.InvocationName
                Expected = $expected; Actual = $actual; Passed = $actual -eq $expected
                ExitCode = $null; Message = $message
            })
        }

        $publicCommands = @($commands | ForEach-Object { $_.InvocationName.Split('_')[0] } | Sort-Object -Unique)
        foreach ($command in $publicCommands) {
            if ($Client -eq 'CLI') {
                $metadata = $entry.Index.GetCommandMetadata($command, $clientType)
                $acceptsEmpty = @($metadata.ParameterSets.Values | Where-Object {
                    @($_.Parameters.Values | Where-Object IsRequired).Count -eq 0
                }).Count -gt 0
                $expectedPaths = @($(if ($acceptsEmpty) { 'BindingAcceptedExecutionBlocked' } else { 'ParameterSetRejected' }))
                $startInfo = [System.Diagnostics.ProcessStartInfo]::new($cli)
                foreach ($word in $command.Split(' ')) { $startInfo.ArgumentList.Add($word) }
                $probe = Invoke-ProbeProcess $startInfo 30
                $message = ($probe.StdOut + $probe.StdErr).Trim()
                $exitCode = $probe.ExitCode
            }
            else {
                $null = $pipeline.AddCommand('Get-Command').AddParameter('Name', "$($entry.Extension)\$command")
                $info = @($pipeline.Invoke())
                if ($pipeline.HadErrors -or $info.Count -ne 1) { throw "Cannot find public cmdlet $($entry.Extension)\$command" }
                $pipeline.Commands.Clear()
                $nativeSets = @($info[0].ParameterSets)
                $defaultSets = @($nativeSets | Where-Object IsDefault)
                $candidates = if ($defaultSets.Count -eq 1) { $defaultSets } else { $nativeSets }
                $hasEmptyNativeSet = @($candidates | Where-Object {
                    @($_.Parameters | Where-Object IsMandatory).Count -eq 0
                }).Count -gt 0
                $expectedPaths = if ($hasEmptyNativeSet) {
                    @('BindingAcceptedExecutionBlocked', 'PowerShellAmbiguousBinding')
                } else { @('PowerShellMandatoryBinding', 'PowerShellAmbiguousBinding') }
                $pipeline.Streams.Error.Clear()
                $null = $pipeline.AddCommand("$($entry.Extension)\$command").AddParameter('ErrorAction', 'Stop')
                $invocationError = ''
                try { $null = $pipeline.Invoke() }
                catch [System.Management.Automation.MethodInvocationException] {
                    $invocationError = $_.Exception.InnerException.Message
                }
                finally { $pipeline.Commands.Clear() }
                $message = ($pipeline.Streams.Error | ForEach-Object { "$($_.FullyQualifiedErrorId): $($_.Exception.Message)" }) -join "`n"
                if ($invocationError) { $message = "$message`n$invocationError".Trim() }
                $exitCode = if ($pipeline.HadErrors -or $invocationError) { 1 } else { 0 }
            }
            $actual = Get-ValidationPath $message
            $results.Add([pscustomobject]@{
                Extension = $entry.Extension; Layer = 'PublicInvocation'; Command = $command
                Expected = $expectedPaths -join ' or '; Actual = $actual
                Passed = $actual -in $expectedPaths -and $exitCode -ne 0
                ExitCode = $exitCode; Message = $message
            })
        }
    }

    $report = [ordered]@{
        GeneratedAt = [datetime]::UtcNow.ToString('o')
        Reference = 'https://github.com/Azure/azclips/pull/1379'
        Client = $Client
        Scope = 'All product ExtensionIndex CLI tables, including Agent; existing sets preserved. PowerShell metadata unchanged.'
        Safety = 'Synthetic isolated home/profile, telemetry/recovery off. Startup hook blocks accepted bindings before any processor execution.'
        Passed = @($results | Where-Object Passed).Count
        Total = $results.Count
        Inventory = $inventory
        Results = $results
    }
    $report | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $outputRoot "$Client-parameter-set-validation.json") -Encoding utf8
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# $Client parameter-set validation")
    $lines.Add('')
    $lines.Add("Passed: $($report.Passed) / $($report.Total). Reference: $($report.Reference)")
    $lines.Add('')
    $lines.Add($report.Scope)
    $lines.Add('')
    $lines.Add($report.Safety)
    $lines.Add('')
    $lines.Add('BindingAcceptedExecutionBlocked is NOT a missing-parameter rejection: inputs passed shared binding and the test hook deliberately stopped execution. MetadataBinding does not execute processors; PublicInvocation runs the real command/cmdlet. Native PowerShell sets and shared extension sets are distinct.')
    $lines.Add('')
    $lines.Add('| Extension | Layer | Observed path | Count |')
    $lines.Add('|---|---|---|---|')
    foreach ($group in ($results | Group-Object Extension, Layer, Actual)) {
        $sample = $group.Group[0]
        $lines.Add("| $($sample.Extension) | $($sample.Layer) | $($sample.Actual) | $($group.Count) |")
    }
    $lines.Add('')
    $lines.Add('## Command inventory')
    $lines.Add('')
    $lines.Add('| Extension | Command | Parameter-level required flags | Sets and required canonical members |')
    $lines.Add('|---|---|---|---|')
    foreach ($item in $inventory) {
        $setSummary = ($item.ParameterSets | ForEach-Object { "$($_.Name): [$($_.Required -join ', ')]" }) -join '; '
        $lines.Add("| $($item.Extension) | $($item.Command) | $($item.RequiredAtParameterLevel -join ', ') | $setSummary |")
    }
    $lines.Add('')
    $lines.Add('## Per-command evidence')
    foreach ($result in $results) {
        $status = if ($result.Passed) { 'PASS' } else { 'FAIL' }
        $lines.Add('')
        $lines.Add("### $($result.Extension) / $($result.Layer) / $($result.Command): $status")
        $lines.Add('')
        $lines.Add("Expected: $($result.Expected); actual: $($result.Actual); exit code: $($result.ExitCode).")
        $lines.Add('')
        $lines.Add('```text')
        $lines.Add($result.Message)
        $lines.Add('```')
    }
    $lines | Set-Content (Join-Path $outputRoot "$Client-parameter-set-validation.md") -Encoding utf8
    $results | Group-Object Layer, Actual | Select-Object Name, Count | Format-Table -AutoSize
    Write-Host "$Client passed $($report.Passed) / $($report.Total). Reports: $outputRoot"
    if ($report.Passed -ne $report.Total) { throw 'Unexpected validation paths. See per-command evidence in the report.' }
}
finally { $pipeline.Dispose() }
