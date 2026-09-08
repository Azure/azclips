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

if ($Scenario)
{
    $calls = [System.Collections.Generic.List[object]]::new()
    function Invoke-FakeNative
    {
        param([string]$Command, [object[]]$Arguments)
        $calls.Add([pscustomobject]@{
            Command = $Command
            Arguments = @($Arguments)
            DotnetRoot = $env:DOTNET_ROOT
            Path = $env:PATH
        })
        ConvertTo-Json -InputObject @($calls.ToArray()) -Depth 5 |
            Set-Content -LiteralPath (Join-Path $ScratchRoot 'calls.json')

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
    }
    if ($Scenario -like '*soft-fails') { $parameters.ContinueOnError = 'true' }
    if ($Scenario -like 'container-*')
    {
        $parameters.Phase = $Scenario.Substring('container-'.Length)
    }
    if ($Scenario -in @('phase-soft-fails', 'phase-hard-fails', 'native-install', 'missing-command'))
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

    & $runner @parameters
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
        'native-install', 'phase-hard-fails', 'phase-soft-fails', 'missing-command'
    )
    foreach ($name in $scenarios)
    {
        $caseRoot = Join-Path $scratch $name
        New-Item -ItemType Directory -Path $caseRoot | Out-Null
        $output = & $pwsh -NoProfile -File $PSCommandPath -Scenario $name -ScratchRoot $caseRoot 2>&1
        $exitCode = $LASTEXITCODE
        $text = ($output | ForEach-Object { "$_" }) -join "`n"
        $expectedExit = if ($name -in @('pull-fails', 'run-fails', 'bootstrap-hard-fails', 'phase-hard-fails')) {
            42
        } elseif ($name -eq 'missing-command') { 1 } else { 0 }
        Assert-Equal $expectedExit $exitCode "$name exit code`n$text"

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
        }
        Write-Host "PASS: $name"
        $count++
    }

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
