# Offline fixtures for the actual phase runner and Azure Pipelines conditions.
# Usage: pwsh -NoProfile -File ./tests/Scripts/PS/PSValidationPipelineFixtures.ps1
[CmdletBinding()]
param(
    [string]$Scenario,
    [string]$ScratchRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$runner = Join-Path $repoRoot 'devops/scripts/packaging/powershell/invoke_ps_validation_step.ps1'
$completeReport = Join-Path $repoRoot 'devops/scripts/packaging/powershell/complete_ps_validation_artifacts.ps1'

if ($Scenario)
{
    $calls = [System.Collections.Generic.List[object]]::new()
    function Invoke-FakeNative
    {
        param([string]$Command, [object[]]$Arguments)
        Write-Host "FAKE NATIVE OUTPUT: $Command"
        $calls.Add([pscustomobject]@{
            Command = $Command
            Arguments = @($Arguments)
            DotnetRoot = $env:DOTNET_ROOT
            Path = $env:PATH
        })
        ConvertTo-Json -InputObject @($calls.ToArray()) -Depth 5 |
            Set-Content -LiteralPath (Join-Path $ScratchRoot 'calls.json')

        if ($Scenario -in 'native-output', 'native-output-fails')
        {
            $executable = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
            $command = '[Console]::Out.WriteLine("NATIVE_STDOUT_MARKER"); [Console]::Error.WriteLine("NATIVE_STDERR_MARKER")'
            if ($Scenario -eq 'native-output-fails') { $command += '; exit 42' }
            & $executable -NoProfile -Command $command
            return
        }
        $global:LASTEXITCODE = 0
        if (($Scenario -eq 'pull-fails' -and $Arguments[0] -eq 'pull') -or
            ($Scenario -eq 'run-fails' -and $Arguments[0] -eq 'run') -or
            ($Scenario -in @('bootstrap-soft-fails', 'bootstrap-hard-fails') -and $Arguments[0] -eq 'exec') -or
            $Scenario -in @('phase-soft-fails', 'phase-hard-fails'))
        {
            $global:LASTEXITCODE = 42
        }
    }

    function global:docker { Invoke-FakeNative -Command 'docker' -Arguments $args }
    function global:pwsh { Invoke-FakeNative -Command 'pwsh' -Arguments $args }
    function global:sh { Invoke-FakeNative -Command 'sh' -Arguments $args }
    function global:Invoke-FakePowerShell { Invoke-FakeNative -Command 'tested-pwsh' -Arguments $args }

    $parameters = @{
        Phase = 'Bootstrap'
        ValidationMode = 'container'
        DistributionFamily = 'alpine'
        DistributionName = 'Fixture distribution'
        SourceRoot = $repoRoot
        ArtifactsRoot = Join-Path $ScratchRoot 'artifacts with spaces'
        WorkRoot = Join-Path $ScratchRoot 'work with spaces'
        DistributionScript = 'devops/scripts/packaging/powershell/linux/alpine_install_script.sh'
        PowerShellVersion = '7.6'
        DotnetRuntimeVersion = '10.0'
        ContainerImage = 'fixture:latest'
        ContainerName = 'azclips-psval-fixture'
        PwshPath = '/opt/pwsh/pwsh'
        DotnetRootPath = Join-Path $ScratchRoot 'dotnet with spaces'
        ContinueOnError = 'false'
        ReportRoot = Join-Path $ScratchRoot 'reports with spaces'
        ValidationKey = 'alpine_3_23_ps7_6'
        AzVersion = '1.2.3'
    }
    if ($Scenario -like '*soft-fails') { $parameters.ContinueOnError = 'true' }
    if ($Scenario -like 'container-*')
    {
        $parameters.Phase = $Scenario.Substring('container-'.Length)
    }
    if ($Scenario -in @('phase-soft-fails', 'phase-hard-fails', 'native-install', 'missing-command', 'no-report-root', 'report-write-fails', 'native-output', 'native-output-fails'))
    {
        $parameters.Phase = 'Install'
        $parameters.ValidationMode = 'native'
        $parameters.DistributionFamily = 'windows'
        $parameters.PwshPath = 'Invoke-FakePowerShell'
    }
    if ($Scenario -eq 'missing-command')
    {
        $parameters.PwshPath = 'Nonexistent-ValidationFixtureCommand'
    }
    if ($Scenario -eq 'no-report-root') { $parameters.Remove('ReportRoot') }
    if ($Scenario -eq 'report-write-fails')
    {
        $parameters.ReportRoot = Join-Path $ScratchRoot 'blocked-report-root'
        Set-Content -LiteralPath $parameters.ReportRoot -Value 'Existing file'
    }
    if ($Scenario -in @('windows-bootstrap', 'macos-bootstrap'))
    {
        $parameters.ValidationMode = 'native'
        $parameters.DistributionFamily = if ($Scenario -eq 'windows-bootstrap') { 'windows' } else { 'macos' }
        $parameters.DistributionScript = if ($Scenario -eq 'windows-bootstrap') {
            'devops/scripts/packaging/powershell/windows/windows_install_script.ps1'
        } else {
            'devops/scripts/packaging/powershell/macos/macos_install_script.sh'
        }
    }

    if ($Scenario -eq 'complete-lifecycle')
    {
        foreach ($phase in 'Bootstrap', 'Install', 'Upgrade', 'Validate', 'Uninstall')
        {
            $parameters.Phase = $phase
            & $runner @parameters
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }
    }
    else
    {
        & $runner @parameters
    }
    exit $LASTEXITCODE
}

function Assert-Equal
{
    param($Expected, $Actual, [string]$Message)
    if ($Expected -cne $Actual)
    {
        throw "$Message`: expected '$Expected', got '$Actual'."
    }
}

function Assert-Contains
{
    param([string]$Text, [string]$Expected, [string]$Message)
    if (-not $Text.Contains($Expected)) { throw "$Message`: missing '$Expected'." }
}

# Evaluate the small expression subset used by the real publication gates, rather
# than testing a copy of their success policy.
function Read-Condition
{
    param([string]$Text, [ref]$Position, [hashtable]$Variables)
    while ($Position.Value -lt $Text.Length -and [char]::IsWhiteSpace($Text[$Position.Value]))
    {
        $Position.Value++
    }
    $remaining = $Text.Substring($Position.Value)
    $match = [regex]::Match($remaining, "^'([^']*)'")
    if ($match.Success)
    {
        $Position.Value += $match.Length
        return $match.Groups[1].Value
    }
    $match = [regex]::Match($remaining, "^variables\['([^']+)'\]")
    if ($match.Success)
    {
        $Position.Value += $match.Length
        $name = $match.Groups[1].Value
        if (-not $Variables.ContainsKey($name)) { throw "Unknown condition variable: $name" }
        return $Variables[$name]
    }
    $match = [regex]::Match($remaining, '^([A-Za-z]+)\(')
    if (-not $match.Success) { throw "Unsupported condition at: $remaining" }
    $functionName = $match.Groups[1].Value
    $Position.Value += $match.Length
    $arguments = [System.Collections.Generic.List[object]]::new()
    while ($Text[$Position.Value] -ne ')')
    {
        $arguments.Add((Read-Condition -Text $Text -Position $Position -Variables $Variables))
        while ([char]::IsWhiteSpace($Text[$Position.Value])) { $Position.Value++ }
        if ($Text[$Position.Value] -eq ',') { $Position.Value++ }
        elseif ($Text[$Position.Value] -ne ')') { throw "Invalid condition: $Text" }
    }
    $Position.Value++
    switch ($functionName)
    {
        'succeeded' { return $true }
        'and' { return -not ($arguments.Contains($false)) }
        'eq' { return $arguments[0] -eq $arguments[1] }
        'ne' { return $arguments[0] -ne $arguments[1] }
        'in' { return $arguments.GetRange(1, $arguments.Count - 1).Contains($arguments[0]) }
        default { throw "Unsupported condition function: $functionName" }
    }
}

function Test-Condition
{
    param([string]$Text, [hashtable]$Variables)
    $position = 0
    $result = Read-Condition -Text $Text -Position ([ref]$position) -Variables $Variables
    Assert-Equal $Text.Length $position 'Entire condition must be evaluated'
    return $result
}

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) "ps-pipeline-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $scratch | Out-Null
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$count = 0
try
{
    $scenarios = @(
        'bootstrap-success', 'pull-fails', 'run-fails', 'bootstrap-hard-fails',
        'bootstrap-soft-fails', 'windows-bootstrap', 'macos-bootstrap',
        'container-Install', 'container-Upgrade', 'container-Validate', 'container-Uninstall',
        'native-install', 'phase-hard-fails', 'phase-soft-fails', 'missing-command',
        'complete-lifecycle', 'no-report-root', 'report-write-fails', 'native-output', 'native-output-fails'
    )
    foreach ($name in $scenarios)
    {
        $caseRoot = Join-Path $scratch $name
        New-Item -ItemType Directory -Path $caseRoot | Out-Null
        $output = & $pwsh -NoProfile -File $PSCommandPath -Scenario $name -ScratchRoot $caseRoot 2>&1
        $exitCode = $LASTEXITCODE
        $text = ($output | ForEach-Object { "$_" }) -join "`n"
        $expectedExit = if ($name -in @('pull-fails', 'run-fails', 'bootstrap-hard-fails', 'phase-hard-fails', 'native-output-fails')) {
            42
        } elseif ($name -in 'missing-command', 'report-write-fails') { 1 } else { 0 }
        Assert-Equal $expectedExit $exitCode "$name exit code`n$text"

        $reportRoot = Join-Path $caseRoot 'reports with spaces'
        if ($name -eq 'report-write-fails')
        {
            Assert-Equal $false (Test-Path -LiteralPath (Join-Path $caseRoot 'calls.json')) 'Capture initialization failure prevents native mutations'
            $count++
            continue
        }
        if ($name -eq 'no-report-root')
        {
            Assert-Equal $false (Test-Path -LiteralPath $reportRoot) 'Legacy callers do not create report files'
        }
        else
        {
            $report = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
            $matrix = Get-Content -LiteralPath (Join-Path $reportRoot 'matrix.json') -Raw | ConvertFrom-Json
            Assert-Equal 'alpine_3_23_ps7_6' $matrix.validationKey 'Report retains its matrix identity'
            Assert-Equal '7.6' $matrix.requestedPowerShellVersion 'Requested version is distinguished from the host version'
            Assert-Equal '1.2.3' $matrix.azVersion 'Report records the Azure PS version'
            $executed = @($report.phases | Where-Object { $null -ne $_.startedAtUtc })
            Assert-Equal $(if ($name -eq 'complete-lifecycle') { 5 } else { 1 }) $executed.Count 'Only executed phases have start timestamps'
            foreach ($entry in $executed)
            {
                $expectedStatus = if ($name -like '*soft-fails') { 'SucceededWithIssues' }
                    elseif ($expectedExit -ne 0) { 'Failed' }
                    elseif ($entry.phase -in 'Upgrade', 'Validate') { 'Skipped' }
                    else { 'Succeeded' }
                Assert-Equal $expectedStatus $entry.status "$name records the real phase status"
                Assert-Equal $(if ($name -like '*soft-fails') { 42 } else { $expectedExit }) $entry.exitCode 'Native failure code is retained even for a soft failure'
                Assert-Equal $true ($null -ne $entry.finishedAtUtc) 'Completed phases have finish timestamps'
                $log = Get-Content -LiteralPath (Join-Path $reportRoot "logs/$($entry.phase.ToLowerInvariant()).log") -Raw
                Assert-Contains $log "PowerShell validation: $($entry.phase)" 'Phase log survives process exit'
                if ($name -ne 'missing-command')
                {
                    Assert-Contains $log 'FAKE NATIVE OUTPUT:' 'Transcript contains command output, not only status metadata'
                }
                if ($name -in 'native-output', 'native-output-fails')
                {
                    Assert-Contains $log 'NATIVE_STDOUT_MARKER' 'Transcript includes stdout from an actual native process'
                    Assert-Contains $log 'NATIVE_STDERR_MARKER' 'Transcript includes stderr from an actual native process'
                }
            }
            $jobStatus = if ($name -like '*soft-fails') { 'SucceededWithIssues' }
                elseif ($expectedExit -ne 0) { 'Failed' } else { 'Succeeded' }
            & $completeReport -ReportRoot $reportRoot -ValidationKey $matrix.validationKey -JobStatus $jobStatus
            $completed = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
            $expectedStatus = if ($name -eq 'complete-lifecycle') { 'Succeeded' }
                elseif ($jobStatus -ne 'Succeeded') { $jobStatus } else { 'Incomplete' }
            Assert-Equal $expectedStatus $completed.status 'Overall result never treats missing required phases as success'
            Assert-Equal 5 @(Get-ChildItem -LiteralPath (Join-Path $reportRoot 'logs') -Filter '*.log').Count 'All phases have a log or an explicit skip explanation'
            Assert-Equal 0 @($completed.phases | Where-Object status -eq 'NotRun').Count 'Never-started phases are explicitly skipped'
        }

        if ($name -eq 'missing-command')
        {
            Assert-Contains $text 'Nonexistent-ValidationFixtureCommand' 'Missing executable is surfaced'
            $count++
            continue
        }
        $calls = @(Get-Content -LiteralPath (Join-Path $caseRoot 'calls.json') -Raw | ConvertFrom-Json)
        if ($name -like '*soft-fails')
        {
            Assert-Contains $text 'variable=PsValidationSoftFailed]true' "$name marks later phases skipped"
            Assert-Contains $text 'result=SucceededWithIssues;' "$name is nonblocking"
        }
        if ($name -in @('bootstrap-success', 'bootstrap-hard-fails', 'bootstrap-soft-fails'))
        {
            Assert-Equal 'pull,run,exec' (($calls | ForEach-Object { $_.Arguments[0] }) -join ',') 'Container lifecycle order'
            Assert-Contains ($calls[1].Arguments -join '|') 'DOTNET_ROOT=/opt/dotnet' 'Container runtime persists'
            Assert-Contains ($calls[1].Arguments -join '|') 'artifacts with spaces:/artifacts:ro' 'Mount path remains one argument'
            Assert-Contains ($calls[1].Arguments -join '|') 'reports with spaces:/reports' 'Reports use a separate persistent writable mount'
            Assert-Equal $false (($calls[1].Arguments -join '|').Contains('reports with spaces:/reports:ro')) 'Report mount is writable'
            Assert-Equal 8 $calls[2].Arguments.Count 'Bootstrap argument count'
            if ($name -eq 'bootstrap-success')
            {
                Assert-Contains $text 'variable=PwshPath]/opt/pwsh/pwsh' 'Bootstrap publishes path'
            }
            else
            {
                Assert-Equal $false ($text.Contains('variable=PwshPath]')) 'Failed bootstrap publishes no usable path'
            }
        }
        if ($name -eq 'pull-fails') { Assert-Equal 1 $calls.Count 'Failed pull stops bootstrap' }
        if ($name -eq 'run-fails') { Assert-Equal 2 $calls.Count 'Failed run stops bootstrap' }
        if ($name -like 'container-*')
        {
            Assert-Equal 1 $calls.Count 'One exec per module phase'
            Assert-Equal 'export PATH="$DOTNET_ROOT:$PATH"; exec "$@"' $calls[0].Arguments[4] 'Container reconstructs its own PATH'
            Assert-Equal '_' $calls[0].Arguments[5] 'sh argument-zero placeholder'
            Assert-Contains ($calls[0].Arguments -join '|') ("-Phase|" + $name.Substring(10)) 'Requested phase is forwarded'
            Assert-Contains ($calls[0].Arguments -join '|') '/artifacts/artifacts/PSGallery/local-repo' 'Downloaded gallery location'
            Assert-Contains ($calls[0].Arguments -join '|') '-ReportRoot|/reports' 'Container phase writes to the host-backed report root'
        }
        if ($name -eq 'windows-bootstrap')
        {
            Assert-Equal 'pwsh' $calls[0].Command 'Windows bootstrap uses hosted PowerShell'
            Assert-Contains ($calls[0].Arguments -join '|') '-WorkRoot|' 'Native toolchain root is forwarded'
            Assert-Contains $text 'pwsh.exe' 'Windows publishes the executable path'
        }
        if ($name -eq 'macos-bootstrap')
        {
            Assert-Equal 'sh' $calls[0].Command 'macOS bootstrap uses POSIX shell'
            Assert-Equal 5 $calls[0].Arguments.Count 'macOS bootstrap signature'
        }
        if ($name -in @('native-install', 'phase-hard-fails', 'phase-soft-fails'))
        {
            Assert-Equal 'tested-pwsh' $calls[0].Command 'Native phase uses requested PowerShell'
            $dotnetRoot = Join-Path $caseRoot 'dotnet with spaces'
            Assert-Equal $dotnetRoot $calls[0].DotnetRoot 'Native DOTNET_ROOT restored'
            Assert-Equal $true ($calls[0].Path.StartsWith($dotnetRoot + [System.IO.Path]::PathSeparator)) 'Native PATH restored'
            Assert-Contains ($calls[0].Arguments -join '|') "-ReportRoot|$reportRoot" 'Native report root is forwarded as one argument'
        }
        Write-Host "PASS: $name"
        $count++
    }

    foreach ($jobStatus in 'Failed', 'Canceled', 'Succeeded')
    {
        $reportRoot = Join-Path $scratch "never-started-$jobStatus"
        & $completeReport -ReportRoot $reportRoot -ValidationKey 'never_started' -JobStatus $jobStatus
        $report = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
        Assert-Equal $(if ($jobStatus -eq 'Succeeded') { 'Incomplete' } else { $jobStatus }) $report.status 'Collection handles jobs that never reached Bootstrap'
        Assert-Equal 5 @($report.phases | Where-Object status -eq 'Skipped').Count 'Missing phases are not fabricated as passed'
        Assert-Equal $false $report.runtimeEnvironmentCaptured 'Missing target PowerShell environment is explicit'
    }
    . (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/phase_artifacts.ps1')
    $interruptedRoot = Join-Path $scratch 'interrupted'
    Initialize-PSValidationReport -ReportRoot $interruptedRoot -Metadata @{ validationKey = 'interrupted' }
    Set-PSValidationPhaseResult -ReportRoot $interruptedRoot -Phase Install -Status Running
    & $completeReport -ReportRoot $interruptedRoot -JobStatus Succeeded
    $report = Get-Content -LiteralPath (Join-Path $interruptedRoot 'results.json') -Raw | ConvertFrom-Json
    Assert-Equal 'Incomplete' $report.status 'Interrupted execution is not treated as success'
    Assert-Equal 'Interrupted' ($report.phases | Where-Object phase -eq Install).status 'Interrupted phase is retained'
    Write-Host 'PASS: incomplete and interrupted artifact collection'
    $count++

    foreach ($corruption in 'empty', 'duplicate', 'unknown-status')
    {
        $reportRoot = Join-Path $scratch "invalid-report-$corruption"
        Initialize-PSValidationReport -ReportRoot $reportRoot -Metadata @{ validationKey = 'invalid' }
        $reportPath = Join-Path $reportRoot 'results.json'
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json -AsHashtable
        switch ($corruption)
        {
            'empty' { $report.phases = @() }
            'duplicate' { $report.phases[1].phase = 'Bootstrap' }
            'unknown-status' { $report.phases[0].status = 'Unknown' }
        }
        Write-PSValidationJson -Path $reportPath -Value $report
        $caught = $null
        try { & $completeReport -ReportRoot $reportRoot -JobStatus Succeeded }
        catch { $caught = $_ }
        Assert-Equal $true ($null -ne $caught) 'Malformed results must not become a successful summary'
        Assert-Contains $caught.Exception.Message 'Invalid PowerShell validation report' 'Malformed report has an explicit diagnostic'
    }
    Write-Host 'PASS: malformed phase reports fail explicitly'
    $count++

    $summary = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/jobs/finalize-build-summary.yml')
    $gates = @($summary | Where-Object { $_ -match '^\s*condition:.*ValidatePSResult' } |
        ForEach-Object { ($_ -split 'condition:\s*', 2)[1] })
    Assert-Equal 16 $gates.Count 'Every download, collect and publish gate is covered'
    foreach ($gate in $gates)
    {
        $variables = @{}
        foreach ($match in [regex]::Matches($gate, "variables\['([^']+)'\]"))
        {
            $variables[$match.Groups[1].Value] = 'Succeeded'
        }
        foreach ($psResult in @('Succeeded', 'SucceededWithIssues', 'Failed', 'Canceled', 'Skipped'))
        {
            $variables.ValidatePSResult = $psResult
            Assert-Equal ($psResult -in @('Succeeded', 'SucceededWithIssues')) (Test-Condition $gate $variables) "Publication after PS $psResult"
        }
        $variables.ValidatePSResult = 'SucceededWithIssues'
        $variables.BuildCommonResult = 'Failed'
        Assert-Equal $false (Test-Condition $gate $variables) 'Canary warning does not override another build failure'
    }
    Write-Host 'PASS: final publication result policy'
    $count++

    $template = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/steps/ps-validation-phase.yml') -Raw
    Assert-Equal 1 ([regex]::Matches($template, '(?m)^- (pwsh|bash|task):').Count) 'Exactly one visible task per phase'
    Assert-Contains $template 'invoke_ps_validation_step.ps1' 'Template invokes the exercised phase runner'
    $condition = [regex]::Match($template, '(?m)^\s+condition: (.+)$').Groups[1].Value.Trim()
    Assert-Equal $false (Test-Condition $condition @{ PsValidationSoftFailed = 'true' }) 'Soft failure skips remaining phases'
    Assert-Equal $true (Test-Condition $condition @{ PsValidationSoftFailed = 'false' }) 'Normal phase can proceed'
    $job = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/jobs/validate-ps-build.yml') -Raw
    Assert-Equal 'Bootstrap,Install,Upgrade,Validate,Uninstall' (([regex]::Matches($job, '(?m)^\s+phase: (\w+)') |
        ForEach-Object { $_.Groups[1].Value }) -join ',') 'All phases retain their order'
    Assert-Contains $job 'and(always(), eq(variables[''ValidationMode''], ''container''))' 'Containers are removed after failures'
    Assert-Contains $job 'matrix_args+=(--limit-testing)' 'Limit reaches the generator before matrix expansion'
    Assert-Equal $true ([regex]::IsMatch($job, '(?m)- name: limit_testing\r?\n  type: boolean\r?\n  default: false')) 'Full matrix is the typed default'
    $stage = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/stages/build-unsigned-distribution.yml') -Raw
    Assert-Contains $stage 'limit_testing: ${{ parameters.limit_ps_testing }}' 'Shared stage forwards the selected limit'
    Assert-Equal $true ([regex]::IsMatch($stage, '(?m)- name: limit_ps_testing\r?\n  type: boolean\r?\n  default: false')) 'Release stages default to full coverage'
    $alpha = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/azclips-alpha-build.yml') -Raw
    Assert-Contains $alpha 'limit_ps_testing: false' 'Alpha temporarily runs the full matrix for PR validation'
    Assert-Contains $job 'test_generate_ps_validation_matrix.sh' 'Matrix fixtures are wired into CI'
    Assert-Contains $job 'PSModuleLifecycleFixtures.ps1' 'Module fixtures are wired into CI'
    Assert-Contains $job 'PSValidationPipelineFixtures.ps1' 'Pipeline fixtures are wired into CI'
    Assert-Contains $job 'artifact: Validate_PS_Sim_$(ValidationKey)' 'Each matrix leg has a unique validation artifact'
    Assert-Equal $true ([regex]::IsMatch($job, '(?s)displayName: Publish PS Validation Artifacts\s+condition: always\(\)')) 'Artifacts publish after validation failures'
    Assert-Equal $true ([regex]::IsMatch($job, '(?s)condition: always\(\)\s+displayName: Finalize PS Validation Artifacts')) 'Finalization also runs after failures'
    Assert-Equal $true ($job.IndexOf('complete_ps_validation_artifacts.ps1') -lt $job.IndexOf('artifact: Validate_PS_Sim_')) 'Results are finalized before publication'
    Assert-Contains $template '-ReportRoot (Join-Path $env:AGENT_TEMPDIRECTORY "azclips-ps-validation-reports-$env:SYSTEM_JOBID")' 'Reports stay out of source and package directories'
    Assert-Contains $job 'targetPath: $(Agent.TempDirectory)/azclips-ps-validation-reports-$(System.JobId)' 'Published root matches the host report mount'
    Write-Host 'PASS: task shape, ordering, cleanup and fixture wiring'
    $count++

    $workflow = Get-Content -LiteralPath (Join-Path $repoRoot '.github/workflows/build-and-test.yml') -Raw
    Assert-Contains $job 'UsePythonVersion@0' 'Azure Pipelines provisions Python'
    Assert-Contains $workflow 'actions/setup-python@' 'GitHub Actions provisions Python'
    foreach ($pipeline in @($job, $workflow))
    {
        Assert-Contains $pipeline 'python3 -m venv "$tools_root"' 'YAML dependency uses an isolated environment'
        Assert-Contains $pipeline '"$tools_root/bin/python" -m pip install' 'YAML dependency uses the selected Python'
        $requirements = 'devops/scripts/packaging/powershell/requirements.txt'
        Assert-Contains $pipeline $requirements 'Both pipelines use the pinned YAML dependency'
        Assert-Equal $true ($pipeline.IndexOf($requirements) -lt $pipeline.IndexOf('test_generate_ps_validation_matrix.sh')) 'YAML dependency precedes matrix fixtures'
    }
    Assert-Contains $job '##vso[task.prependpath]$tools_root/bin' 'Azure Pipelines exposes the YAML environment to later tasks'
    Assert-Contains $workflow 'echo "$tools_root/bin" >> "$GITHUB_PATH"' 'GitHub Actions exposes the YAML environment to later steps'
    Write-Host 'PASS: YAML dependency provisioning'
    $count++
}
finally
{
    Remove-Item -LiteralPath $scratch -Recurse -Force
}

Write-Host "PASS: $count PowerShell validation pipeline fixture groups."
exit 0
