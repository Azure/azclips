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
$dispatcher = Join-Path $repoRoot 'devops/scripts/packaging/powershell/run_ps_validation_phase.ps1'
$completeReport = Join-Path $repoRoot 'devops/scripts/packaging/powershell/complete_ps_validation_artifacts.ps1'
$lifecycleScenarios = @(
    'complete-lifecycle', 'validate-hard-fails', 'validate-soft-fails',
    'dependency-lifecycle-hard-fails', 'dependency-lifecycle-soft-fails',
    'multiple-phase-hard-fails', 'multiple-phase-soft-fails'
)
$dependencyFailureScenarios = @(
    'dependency-hard-fails', 'dependency-soft-fails',
    'dependency-lifecycle-hard-fails', 'dependency-lifecycle-soft-fails',
    'multiple-phase-hard-fails', 'multiple-phase-soft-fails'
)
$validateFailureScenarios = @(
    'validate-hard-fails', 'validate-soft-fails',
    'multiple-phase-hard-fails', 'multiple-phase-soft-fails'
)

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
            $Scenario -in @('phase-soft-fails', 'phase-hard-fails', 'dispatcher-validate-fails') -or
            ($Scenario -in $dependencyFailureScenarios -and
                $Arguments -contains '-Phase' -and $Arguments -contains 'Dependencies') -or
            ($Scenario -in $validateFailureScenarios -and
                $Arguments -contains '-Phase' -and $Arguments -contains 'Validate'))
        {
            $global:LASTEXITCODE = 42
        }
        if ($Scenario -in 'dispatcher-dependencies-fails', 'dispatcher-dependencies-package-fails' -and
            $Arguments -contains (Join-Path $repoRoot 'devops/scripts/packaging/powershell/test_ps_dependencies.ps1'))
        {
            Write-Host 'FIXTURE_PACKAGE_DEPENDENCY_FAILURE'
            $global:LASTEXITCODE = 42
        }
        if ($Scenario -in 'dispatcher-dependencies-fails', 'dispatcher-dependencies-runtime-fails' -and
            $Arguments -contains (Join-Path $repoRoot 'devops/scripts/packaging/powershell/test_ps_dependency_runtime.ps1'))
        {
            Write-Host 'FIXTURE_RUNTIME_DEPENDENCY_FAILURE'
            $global:LASTEXITCODE = 43
        }
    }

    function global:docker { Invoke-FakeNative -Command 'docker' -Arguments $args }
    function global:pwsh { Invoke-FakeNative -Command 'pwsh' -Arguments $args }
    function global:sh { Invoke-FakeNative -Command 'sh' -Arguments $args }
    function global:Invoke-FakePowerShell { Invoke-FakeNative -Command 'tested-pwsh' -Arguments $args }
    Set-Item -LiteralPath 'Function:\global:Invoke-FakePowerShell With Spaces' -Value {
        Invoke-FakeNative -Command 'tested-pwsh-with-spaces' -Arguments $args
    }

    if ($Scenario -like 'dispatcher-*')
    {
        $dispatchParameters = @{
            Phase = 'Validate'
            SourceRoot = Join-Path $ScratchRoot 'source with spaces'
            PwshPath = 'Invoke-FakePowerShell With Spaces'
        }
        if ($Scenario -ne 'dispatcher-validate-no-report')
        {
            $dispatchParameters.ReportRoot = Join-Path $ScratchRoot 'reports with spaces'
        }
        if ($Scenario -like 'dispatcher-dependencies*')
        {
            $dispatchParameters.Phase = 'Dependencies'
            $dispatchParameters.RepositoryRoot = Join-Path $ScratchRoot 'gallery with spaces'
            $null = New-Item -ItemType Directory -Path $dispatchParameters.RepositoryRoot
            $env:DOTNET_ROOT = Join-Path $ScratchRoot 'dotnet with spaces'
        }
        & $dispatcher @dispatchParameters
        exit $LASTEXITCODE
    }

    if ($Scenario -eq 'install-transcript-fails')
    {
        function global:Stop-Transcript
        {
            Microsoft.PowerShell.Host\Stop-Transcript @args | Out-Null
            throw 'FIXTURE_TRANSCRIPT_FINALIZATION_FAILURE'
        }
    }

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
    if ($Scenario -in @('phase-soft-fails', 'phase-hard-fails', 'native-install', 'missing-command', 'no-report-root', 'report-write-fails', 'native-output', 'native-output-fails', 'install-transcript-fails') -or
        $Scenario -in $dependencyFailureScenarios -or $Scenario -in $validateFailureScenarios)
    {
        $parameters.Phase = 'Install'
        $parameters.ValidationMode = 'native'
        $parameters.DistributionFamily = 'windows'
        $parameters.PwshPath = 'Invoke-FakePowerShell'
    }
    if ($Scenario -in 'dependency-hard-fails', 'dependency-soft-fails') { $parameters.Phase = 'Dependencies' }
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

    if ($Scenario -in $lifecycleScenarios)
    {
        $lifecycleExitCode = 0
        foreach ($phase in 'Bootstrap', 'Dependencies', 'Install', 'Upgrade', 'Validate', 'Uninstall')
        {
            $parameters.Phase = $phase
            & $runner @parameters
            if ($LASTEXITCODE -ne 0)
            {
                if ($phase -notin 'Dependencies', 'Validate') { exit $LASTEXITCODE }
                if ($lifecycleExitCode -eq 0) { $lifecycleExitCode = $LASTEXITCODE }
            }
        }
        exit $lifecycleExitCode
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
    $match = [regex]::Match($remaining, '^parameters\.([A-Za-z]+)')
    if ($match.Success)
    {
        $Position.Value += $match.Length
        $name = $match.Value
        if (-not $Variables.ContainsKey($name)) { throw "Unknown condition parameter: $name" }
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
        'succeeded' {
            return -not $Variables.ContainsKey('Agent.JobStatus') -or
                $Variables['Agent.JobStatus'] -in @('Succeeded', 'SucceededWithIssues')
        }
        'succeededOrFailed' {
            return -not $Variables.ContainsKey('Agent.JobStatus') -or
                $Variables['Agent.JobStatus'] -in @('Succeeded', 'SucceededWithIssues', 'Failed')
        }
        'always' { return $true }
        'and' { return -not ($arguments.Contains($false)) }
        'or' { return $arguments.Contains($true) }
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

function Get-PhaseCondition
{
    param([string]$Template, [string]$Phase)

    $branches = [regex]::Matches($Template,
        '(?m)^  \$\{\{ (?<selector>(?:if|elseif) (?<expression>.+)|else) \}\}:\r?\n    condition: (?<condition>.+)$')
    Assert-Equal 4 $branches.Count 'Phase task selects a condition for its actual prerequisites'
    Assert-Equal 'else' $branches[-1].Groups['selector'].Value 'Default phase condition follows the prerequisite branches'
    foreach ($branch in $branches)
    {
        if ($branch.Groups['selector'].Value -eq 'else' -or
            (Test-Condition $branch.Groups['expression'].Value @{ 'parameters.phase' = $Phase }))
        {
            return $branch.Groups['condition'].Value.Trim()
        }
    }
    throw "No condition found for phase $Phase."
}

$template = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/steps/ps-validation-phase.yml') -Raw
$scratch = Join-Path ([System.IO.Path]::GetTempPath()) "ps-pipeline-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $scratch | Out-Null
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$count = 0
$installedByScenario = @{}
try
{
    $scenarios = @(
        'bootstrap-success', 'pull-fails', 'run-fails', 'bootstrap-hard-fails',
        'bootstrap-soft-fails', 'windows-bootstrap', 'macos-bootstrap',
        'container-Dependencies', 'container-Install', 'container-Upgrade', 'container-Validate', 'container-Uninstall',
        'native-install', 'phase-hard-fails', 'phase-soft-fails', 'missing-command',
        'complete-lifecycle', 'no-report-root', 'report-write-fails', 'native-output', 'native-output-fails',
        'validate-hard-fails', 'validate-soft-fails', 'dependency-hard-fails', 'dependency-soft-fails', 'install-transcript-fails',
        'dependency-lifecycle-hard-fails', 'dependency-lifecycle-soft-fails',
        'multiple-phase-hard-fails', 'multiple-phase-soft-fails',
        'dispatcher-validate', 'dispatcher-validate-no-report', 'dispatcher-validate-fails',
        'dispatcher-dependencies', 'dispatcher-dependencies-fails',
        'dispatcher-dependencies-package-fails', 'dispatcher-dependencies-runtime-fails'
    )
    foreach ($name in $scenarios)
    {
        $caseRoot = Join-Path $scratch $name
        New-Item -ItemType Directory -Path $caseRoot | Out-Null
        $output = & $pwsh -NoProfile -File $PSCommandPath -Scenario $name -ScratchRoot $caseRoot 2>&1
        $exitCode = $LASTEXITCODE
        $text = ($output | ForEach-Object { "$_" }) -join "`n"
        $expectedExit = if ($name -eq 'dispatcher-dependencies-runtime-fails') { 43 }
        elseif ($name -in @('pull-fails', 'run-fails', 'bootstrap-hard-fails', 'phase-hard-fails', 'native-output-fails', 'validate-hard-fails', 'dependency-hard-fails', 'dependency-lifecycle-hard-fails', 'multiple-phase-hard-fails', 'dispatcher-validate-fails', 'dispatcher-dependencies-fails', 'dispatcher-dependencies-package-fails')) {
            42
        } elseif ($name -in 'missing-command', 'report-write-fails', 'install-transcript-fails') { 1 } else { 0 }
        Assert-Equal $expectedExit $exitCode "$name exit code`n$text"
        if ($name -like 'dispatcher-*')
        {
            $calls = @(Get-Content -LiteralPath (Join-Path $caseRoot 'calls.json') -Raw | ConvertFrom-Json)
            if ($name -like 'dispatcher-dependencies*')
            {
                Assert-Equal 2 $calls.Count 'Dependencies always runs both independent suites'
                Assert-Contains ($calls[0].Arguments -join '|') 'test_ps_dependencies.ps1' 'Dependencies invokes the real package suite'
                foreach ($call in $calls)
                {
                    Assert-Equal 'tested-pwsh-with-spaces' $call.Command 'Dependency dispatch uses the tested executable'
                    Assert-Contains ($call.Arguments -join '|') "-RepositoryRoot|$(Join-Path $caseRoot 'gallery with spaces')" 'Dependency repository path is preserved'
                    Assert-Contains ($call.Arguments -join '|') "-ReportRoot|$(Join-Path $caseRoot 'reports with spaces')" 'Dependency report path is preserved'
                }
                Assert-Contains ($calls[1].Arguments -join '|') 'test_ps_dependency_runtime.ps1' 'Dependencies invokes the real runtime suite'
                Assert-Contains ($calls[1].Arguments -join '|') "-DotnetRootPath|$(Join-Path $caseRoot 'dotnet with spaces')" 'Runtime validation receives the primary toolchain'
                if ($name -in 'dispatcher-dependencies-fails', 'dispatcher-dependencies-package-fails')
                {
                    Assert-Contains $text 'FIXTURE_PACKAGE_DEPENDENCY_FAILURE' 'Package failure remains visible after runtime validation'
                }
                if ($name -in 'dispatcher-dependencies-fails', 'dispatcher-dependencies-runtime-fails')
                {
                    Assert-Contains $text 'FIXTURE_RUNTIME_DEPENDENCY_FAILURE' 'Runtime failure remains visible alongside any package failure'
                }
                Write-Host "PASS: $name"
                $count++
                continue
            }
            Assert-Equal 1 $calls.Count 'Validate dispatches exactly one smoke runner'
            Assert-Equal 'tested-pwsh-with-spaces' $calls[0].Command 'Smoke dispatch preserves the requested executable with spaces'
            $expectedArguments = @(
                '-NoProfile', '-File',
                (Join-Path $repoRoot 'devops/scripts/packaging/powershell/test_ps_smoke.ps1'),
                '-SourcePsRoot', (Join-Path $caseRoot 'source with spaces/src/PS'),
                '-PwshPath', 'Invoke-FakePowerShell With Spaces',
                '-SkipCoverageWarnings'
            )
            if ($name -ne 'dispatcher-validate-no-report')
            {
                $expectedArguments += @('-ReportRoot', (Join-Path $caseRoot 'reports with spaces'))
            }
            Assert-Equal ($expectedArguments -join '|') ($calls[0].Arguments -join '|') 'Smoke runner receives the exact CLI contract without source writes'
            Assert-Equal $false (Test-Path -LiteralPath (Join-Path $caseRoot 'source with spaces')) 'Dispatcher does not mutate the source tree'
            Write-Host "PASS: $name"
            $count++
            continue
        }
        $isLifecycle = $name -in $lifecycleScenarios
        $bootstrappedValues = @([regex]::Matches($text, 'variable=PsValidationBootstrapped\](true|false)') |
            ForEach-Object { $_.Groups[1].Value })
        $expectedBootstrappedValues = if ($isLifecycle -or $name -in 'bootstrap-success', 'windows-bootstrap', 'macos-bootstrap') { 'false,true' }
            elseif ($name -in 'pull-fails', 'run-fails', 'bootstrap-hard-fails', 'bootstrap-soft-fails') { 'false' }
            else { '' }
        Assert-Equal $expectedBootstrappedValues ($bootstrappedValues -join ',') "$name publishes readiness only after a successful bootstrap"
        $installedValues = @([regex]::Matches($text, 'variable=PsValidationInstalled\](true|false)') |
            ForEach-Object { $_.Groups[1].Value })
        $expectedInstalledValues = if ($isLifecycle) { 'false,true,false' }
            elseif ($name -eq 'container-Uninstall' -or $name -in @('bootstrap-success', 'pull-fails', 'run-fails', 'bootstrap-hard-fails', 'bootstrap-soft-fails', 'windows-bootstrap', 'macos-bootstrap')) { 'false' }
            elseif ($name -in @('container-Install', 'native-install', 'no-report-root', 'native-output', 'install-transcript-fails')) { 'true' }
            else { '' }
        Assert-Equal $expectedInstalledValues ($installedValues -join ',') "$name publishes only actual installation/cleanup transitions"
        $installedByScenario[$name] = if ($installedValues.Count -gt 0) { $installedValues[-1] } else { 'false' }
        if ($name -eq 'install-transcript-fails')
        {
            Assert-Contains $text 'FIXTURE_TRANSCRIPT_FINALIZATION_FAILURE' 'Original transcript error remains visible'
            Assert-Equal $true ($text.IndexOf('variable=PsValidationInstalled]true') -lt $text.IndexOf('FIXTURE_TRANSCRIPT_FINALIZATION_FAILURE')) 'Successful Install authorizes cleanup before diagnostics can fail'
        }

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
            Assert-Equal $(if ($isLifecycle) { 6 } else { 1 }) $executed.Count 'Only executed phases have start timestamps'
            $phaseVariables = @{
                'Agent.JobStatus' = 'Succeeded'
                PsValidationBootstrapped = 'false'
                PsValidationInstalled = 'false'
                PsValidationSoftFailed = 'false'
            }
            foreach ($entry in $executed)
            {
                $phaseFailed = if ($isLifecycle) {
                    ($entry.phase -eq 'Dependencies' -and $name -in $dependencyFailureScenarios) -or
                    ($entry.phase -eq 'Validate' -and $name -in $validateFailureScenarios)
                } else { $expectedExit -ne 0 -or $name -like '*soft-fails' }
                $expectedStatus = if ($phaseFailed -and $name -like '*soft-fails') { 'SucceededWithIssues' }
                    elseif ($phaseFailed) { 'Failed' }
                    elseif ($entry.phase -eq 'Upgrade') { 'Skipped' }
                    else { 'Succeeded' }
                Assert-Equal $expectedStatus $entry.status "$name records the real phase status"
                $phaseExit = if ($phaseFailed) { if ($name -like '*soft-fails') { 42 } else { $expectedExit } } else { 0 }
                Assert-Equal $phaseExit $entry.exitCode 'Native failure code is retained even for a soft failure'
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
                if ($isLifecycle)
                {
                    $condition = Get-PhaseCondition -Template $template -Phase $entry.phase
                    Assert-Equal $true (Test-Condition $condition $phaseVariables) "$name allows $($entry.phase) after earlier phase outcomes"
                    foreach ($match in [regex]::Matches($log, 'variable=(PsValidationBootstrapped|PsValidationInstalled)\](true|false)'))
                    {
                        $phaseVariables[$match.Groups[1].Value] = $match.Groups[2].Value
                    }
                    if ($entry.status -eq 'Failed') { $phaseVariables['Agent.JobStatus'] = 'Failed' }
                    elseif ($entry.status -eq 'SucceededWithIssues')
                    {
                        $phaseVariables.PsValidationSoftFailed = 'true'
                        if ($phaseVariables['Agent.JobStatus'] -ne 'Failed')
                        {
                            $phaseVariables['Agent.JobStatus'] = 'SucceededWithIssues'
                        }
                    }
                }
            }
            $jobStatus = if ($name -like '*soft-fails') { 'SucceededWithIssues' }
                elseif ($expectedExit -ne 0) { 'Failed' } else { 'Succeeded' }
            & $completeReport -ReportRoot $reportRoot -ValidationKey $matrix.validationKey -JobStatus $jobStatus
            $completed = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
            $expectedStatus = if ($name -eq 'complete-lifecycle') { 'Succeeded' }
                elseif ($jobStatus -ne 'Succeeded') { $jobStatus } else { 'Incomplete' }
            Assert-Equal $expectedStatus $completed.status 'Overall result never treats missing required phases as success'
            if ($isLifecycle -and $name -ne 'complete-lifecycle')
            {
                $failedPhases = @($completed.phases | Where-Object { $_.status -in 'Failed', 'SucceededWithIssues' })
                $expectedFailedPhases = @(
                    if ($name -in $dependencyFailureScenarios) { 'Dependencies' }
                    if ($name -in $validateFailureScenarios) { 'Validate' }
                )
                Assert-Equal ($expectedFailedPhases -join ',') (($failedPhases | ForEach-Object phase) -join ',') 'All independent validation failures are retained'
                Assert-Equal 'Succeeded' ($completed.phases | Where-Object phase -eq Uninstall).status 'Strict Uninstall completes after validation failures'
                foreach ($failedPhase in $failedPhases)
                {
                    Assert-Equal 42 $failedPhase.exitCode 'Successful later phases retain each original validation failure'
                }
                & $completeReport -ReportRoot $reportRoot -JobStatus Succeeded
                $completedAgain = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
                Assert-Equal $expectedStatus $completedAgain.status 'Cleanup success cannot erase hard or soft validation failures from the summary'
            }
            Assert-Equal 6 @(Get-ChildItem -LiteralPath (Join-Path $reportRoot 'logs') -Filter '*.log').Count 'All phases have a log or an explicit skip explanation'
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
            Assert-Contains $text 'variable=PsValidationSoftFailed]true' "$name records a warning without suppressing independent phases"
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
        if ($isLifecycle -and $name -ne 'complete-lifecycle')
        {
            $phaseCalls = @($calls | Where-Object { $_.Arguments -contains '-Phase' })
            Assert-Equal 'Dependencies,Install,Upgrade,Validate,Uninstall' (($phaseCalls | ForEach-Object {
                $_.Arguments[[array]::IndexOf($_.Arguments, '-Phase') + 1]
            }) -join ',') 'All independent native phases execute even after dependency or smoke failures'
            Assert-Equal 'tested-pwsh' $phaseCalls[-1].Command 'Cleanup uses the same tested PowerShell'
            Assert-Equal 1 @($phaseCalls | Where-Object { $_.Arguments -contains 'Uninstall' }).Count 'Strict cleanup runs exactly once'
        }
        Write-Host "PASS: $name"
        $count++
    }

    $nativeDispatcherRoot = Join-Path $scratch 'native dispatcher'
    $null = New-Item -ItemType Directory -Path $nativeDispatcherRoot
    Copy-Item -LiteralPath $dispatcher -Destination $nativeDispatcherRoot
    foreach ($suite in 'package', 'runtime')
    {
        $fileName = if ($suite -eq 'package') { 'test_ps_dependencies.ps1' } else { 'test_ps_dependency_runtime.ps1' }
        $suiteExitCode = if ($suite -eq 'package') { 42 } else { 43 }
        Set-Content -LiteralPath (Join-Path $nativeDispatcherRoot $fileName) -Value @"
[Console]::Out.WriteLine('$suite dependency stdout')
[Console]::Error.WriteLine('$suite dependency stderr')
exit $suiteExitCode
"@
    }
    $nativeHarness = Join-Path $nativeDispatcherRoot 'invoke-dispatcher.ps1'
    Set-Content -LiteralPath $nativeHarness -Value @'
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$env:DOTNET_ROOT = $PSScriptRoot
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
& (Join-Path $PSScriptRoot 'run_ps_validation_phase.ps1') -Phase Dependencies `
    -RepositoryRoot $PSScriptRoot -SourceRoot $PSScriptRoot -PwshPath $pwsh
exit $LASTEXITCODE
'@
    $output = & $pwsh -NoProfile -File $nativeHarness 2>&1
    Assert-Equal 42 $LASTEXITCODE 'Real child processes preserve the first failure with inherited native-error handling enabled'
    $text = ($output | ForEach-Object { "$_" }) -join "`n"
    foreach ($suite in 'package', 'runtime')
    {
        Assert-Contains $text "$suite dependency stdout" 'Each failed suite retains stdout'
        Assert-Contains $text "$suite dependency stderr" 'Each failed suite retains stderr'
    }
    Write-Host 'PASS: independent dependency failures from real native processes'
    $count++

    foreach ($jobStatus in 'Failed', 'Canceled', 'Succeeded')
    {
        $reportRoot = Join-Path $scratch "never-started-$jobStatus"
        & $completeReport -ReportRoot $reportRoot -ValidationKey 'never_started' -JobStatus $jobStatus
        $report = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
        Assert-Equal $(if ($jobStatus -eq 'Succeeded') { 'Incomplete' } else { $jobStatus }) $report.status 'Collection handles jobs that never reached Bootstrap'
        Assert-Equal 6 @($report.phases | Where-Object status -eq 'Skipped').Count 'Missing phases are not fabricated as passed'
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

    foreach ($validateStatus in 'NotRun', 'Skipped', 'Running')
    {
        $reportRoot = Join-Path $scratch "missing-validate-$validateStatus"
        Initialize-PSValidationReport -ReportRoot $reportRoot -Metadata @{ validationKey = 'missing_validate' }
        foreach ($phase in 'Bootstrap', 'Dependencies', 'Install', 'Uninstall')
        {
            Set-PSValidationPhaseResult -ReportRoot $reportRoot -Phase $phase -Status Succeeded
        }
        Set-PSValidationPhaseResult -ReportRoot $reportRoot -Phase Upgrade -Status Skipped
        if ($validateStatus -ne 'NotRun')
        {
            Set-PSValidationPhaseResult -ReportRoot $reportRoot -Phase Validate -Status $validateStatus
        }
        & $completeReport -ReportRoot $reportRoot -JobStatus Succeeded
        $report = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
        Assert-Equal 'Incomplete' $report.status 'Install and cleanup alone cannot substitute for successful smoke validation'
    }
    Write-Host 'PASS: successful smoke validation is required'
    $count++

    $reportRoot = Join-Path $scratch 'missing-dependencies'
    Initialize-PSValidationReport -ReportRoot $reportRoot -Metadata @{ validationKey = 'missing_dependencies' }
    foreach ($phase in 'Bootstrap', 'Install', 'Validate', 'Uninstall')
    {
        Set-PSValidationPhaseResult -ReportRoot $reportRoot -Phase $phase -Status Succeeded
    }
    Set-PSValidationPhaseResult -ReportRoot $reportRoot -Phase Upgrade -Status Skipped
    & $completeReport -ReportRoot $reportRoot -JobStatus Succeeded
    $report = Get-Content -LiteralPath (Join-Path $reportRoot 'results.json') -Raw | ConvertFrom-Json
    Assert-Equal 'Incomplete' $report.status 'Successful lifecycle cannot substitute for dependency validation'
    Assert-Equal 2 $report.schemaVersion 'The required dependency phase uses the new phase-report schema'
    Write-Host 'PASS: dependency validation is required for success'
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

    Assert-Equal 1 ([regex]::Matches($template, '(?m)^- (pwsh|bash|task):').Count) 'Exactly one visible task per phase'
    Assert-Contains $template 'invoke_ps_validation_step.ps1' 'Template invokes the exercised phase runner'
    foreach ($phase in 'Bootstrap', 'Dependencies', 'Install', 'Upgrade', 'Validate', 'Uninstall')
    {
        $condition = Get-PhaseCondition -Template $template -Phase $phase
        foreach ($jobStatus in 'Succeeded', 'SucceededWithIssues', 'Failed', 'Canceled')
        {
            foreach ($installed in 'true', 'false', '')
            {
                foreach ($softFailed in 'true', 'false')
                {
                    foreach ($bootstrapped in 'true', 'false', '')
                    {
                        $variables = @{
                            'Agent.JobStatus' = $jobStatus
                            PsValidationSoftFailed = $softFailed
                            PsValidationInstalled = $installed
                            PsValidationBootstrapped = $bootstrapped
                        }
                        $expected = if ($phase -eq 'Uninstall') { $installed -eq 'true' }
                            elseif ($phase -eq 'Bootstrap') { $jobStatus -in 'Succeeded', 'SucceededWithIssues' }
                            elseif ($phase -in 'Dependencies', 'Install') { $jobStatus -ne 'Canceled' -and $bootstrapped -eq 'true' }
                            else { $jobStatus -ne 'Canceled' -and $installed -eq 'true' }
                        Assert-Equal $expected (Test-Condition $condition $variables) "$phase gate after $jobStatus; bootstrapped=$bootstrapped; installed=$installed; softFailed=$softFailed"
                    }
                }
            }
        }
    }
    $uninstallCondition = Get-PhaseCondition -Template $template -Phase Uninstall
    foreach ($name in 'phase-hard-fails', 'phase-soft-fails', 'dependency-hard-fails', 'dependency-soft-fails', 'missing-command', 'report-write-fails', 'install-transcript-fails')
    {
        $variables = @{
            'Agent.JobStatus' = if ($name -like '*soft-fails') { 'SucceededWithIssues' } else { 'Failed' }
            PsValidationSoftFailed = if ($name -like '*soft-fails') { 'true' } else { 'false' }
            PsValidationInstalled = $installedByScenario[$name]
        }
        Assert-Equal ($name -eq 'install-transcript-fails') (Test-Condition $uninstallCondition $variables) 'Only an actually successful Install authorizes Uninstall, even when reporting fails'
    }
    $job = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/jobs/validate-ps-build.yml') -Raw
    Assert-Equal 'Bootstrap,Dependencies,Install,Upgrade,Validate,Uninstall' (([regex]::Matches($job, '(?m)^\s+phase: (\w+)') |
        ForEach-Object { $_.Groups[1].Value }) -join ',') 'All phases retain their order'
    $preMatrixJob = [regex]::Match($job, '(?ms)^- job: GeneratePowerShellValidationMatrix\r?\n(?<body>.*?)(?=^- job:|\z)').Groups['body'].Value
    $matrixJob = [regex]::Match($job, '(?ms)^- job: PowerShellValidation\r?\n(?<body>.*?)(?=^- job:|\z)').Groups['body'].Value
    Assert-Equal 1 ([regex]::Matches($job, '(?m)^\s+-CoverageOnly\s*`?\s*$').Count) 'Production smoke coverage runs exactly once'
    Assert-Contains $preMatrixJob '-CoverageOnly' 'Coverage validation runs in the pre-matrix job'
    Assert-Equal $false ($preMatrixJob.Contains('strategy:')) 'Coverage job is not expanded into matrix legs'
    Assert-Contains $preMatrixJob '& pwsh -NoProfile -File ./devops/scripts/packaging/powershell/test_ps_smoke.ps1' 'Coverage uses the hosted PowerShell without installing target modules'
    Assert-Equal $true ([regex]::IsMatch($preMatrixJob, '(?s)-CoverageOnly.*?exit \$LASTEXITCODE\s+displayName: Validate Azure PS Smoke-Test Coverage')) 'Coverage preserves native failures and zero-exit warning success'
    Assert-Equal $true ($preMatrixJob.IndexOf('-CoverageOnly') -lt $preMatrixJob.IndexOf('name: GenerateMatrix')) 'Coverage completes before publishing matrix outputs'
    Assert-Contains $preMatrixJob 'artifact: Validate_PS_Smoke_Coverage' 'Coverage report publishes once outside matrix artifacts'
    Assert-Equal $true ([regex]::IsMatch($preMatrixJob, '(?s)displayName: Publish PS Smoke-Test Coverage\s+condition: always\(\)')) 'Coverage diagnostics publish even on configuration failure'
    Assert-Contains $matrixJob 'dependsOn: GeneratePowerShellValidationMatrix' 'Matrix waits for coverage and generation to finish'
    Assert-Equal $false ($matrixJob.Contains('SmokeCoverage') -or $matrixJob.Contains('-CoverageOnly')) 'Matrix legs do not rerun coverage'
    Assert-Equal $false ($template.Contains('SmokeCoverage')) 'Coverage is not a per-distribution phase'
    Assert-Contains $matrixJob 'phase: Validate' 'Every distribution still executes its smoke commands'
    Assert-Contains $job "PsValidationBootstrapped: 'false'" 'A skipped Bootstrap never inherits a usable toolchain marker'
    Assert-Contains $job "PsValidationInstalled: 'false'" 'A skipped Install never inherits an installed marker'
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
    Assert-Contains $job 'PSSmokeFixtures.ps1' 'Smoke fixtures are wired into CI'
    Assert-Contains $job 'PSValidationPipelineFixtures.ps1' 'Pipeline fixtures are wired into CI'
    Assert-Contains $job 'artifact: Validate_PS_Sim_$(ValidationKey)' 'Each matrix leg has a unique validation artifact'
    Assert-Equal $true ([regex]::IsMatch($job, '(?s)displayName: Publish PS Validation Artifacts\s+condition: always\(\)')) 'Artifacts publish after validation failures'
    Assert-Equal $true ([regex]::IsMatch($job, '(?s)condition: always\(\)\s+displayName: Finalize PS Validation Artifacts')) 'Finalization also runs after failures'
    Assert-Equal $true ($job.IndexOf('complete_ps_validation_artifacts.ps1') -lt $job.IndexOf('artifact: Validate_PS_Sim_')) 'Results are finalized before publication'
    Assert-Contains $template '-ReportRoot (Join-Path $env:AGENT_TEMPDIRECTORY "azclips-ps-validation-reports-$env:SYSTEM_JOBID")' 'Reports stay out of source and package directories'
    Assert-Contains $job 'targetPath: $(Agent.TempDirectory)/azclips-ps-validation-reports-$(System.JobId)' 'Published root matches the host report mount'
    Write-Host 'PASS: task shape, ordering, cleanup and fixture wiring'
    $count++

    $buildJob = Get-Content -LiteralPath (Join-Path $repoRoot '.azure-pipelines/templates/dist/jobs/build-ps-modules.yml') -Raw
    Assert-Equal 'Install,Upgrade,Uninstall' (([regex]::Matches($buildJob, '(?m)^\s+-Phase (\w+)') |
        ForEach-Object { $_.Groups[1].Value }) -join ',') 'The common build retains package lifecycle checks but must not run smoke validation'
    Assert-Equal $false ($buildJob.Contains('test_ps_smoke.ps1') -or $buildJob.Contains('ps_smoke_tests.psd1')) 'Production smoke coverage is not a build prerequisite'
    Assert-Contains $buildJob 'Publish Final PS Module Outputs' 'The build publishes the input packages for validation'
    $validationStage = [regex]::Match($stage, '(?ms)^- stage: ValidatePS\r?\n(?<body>.*?)(?=^- stage:|\z)').Groups['body'].Value
    Assert-Contains $validationStage '- BuildPSModules' 'The dedicated PS validation stage runs after package build'
    Assert-Contains $validationStage '../jobs/validate-ps-build.yml' 'The dedicated stage owns the distribution validation jobs'
    Assert-Contains $job 'phase: Validate' 'Production smoke validation remains enabled in the matrix'
    Write-Host 'PASS: production smoke validation runs only after package build'
    $count++

    $workflow = Get-Content -LiteralPath (Join-Path $repoRoot '.github/workflows/build-and-test.yml') -Raw
    Assert-Contains $job 'UsePythonVersion@0' 'Azure Pipelines provisions Python'
    Assert-Contains $workflow 'actions/setup-python@' 'GitHub Actions provisions Python'
    foreach ($pipeline in @($job, $workflow))
    {
        Assert-Contains $pipeline 'PSSmokeFixtures.ps1' 'Both pipelines run the offline module smoke fixtures'
        Assert-Contains $pipeline 'PSDependencyFixtures.ps1' 'Both pipelines run real local-package dependency fixtures'
        Assert-Contains $pipeline 'PSDependencyRuntimeFixtures.ps1' 'Both pipelines run offline runtime dependency fixtures'
        Assert-Contains $pipeline '& pwsh -NoProfile -File ./tests/Scripts/PS/PSSmokeFixtures.ps1' 'Smoke fixtures run as a native process rather than relying on a stale LASTEXITCODE'
        Assert-Equal $true ([regex]::IsMatch($pipeline, 'PSSmokeFixtures\.ps1\r?\n\s+(?:if \(\$LASTEXITCODE -ne 0\) \{ exit \$LASTEXITCODE \}|exit \$LASTEXITCODE)')) 'Smoke fixture native failures propagate in both pipelines'
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
