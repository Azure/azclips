<#
.SYNOPSIS
Runs one Azure Pipelines PowerShell validation step on a container or native agent.

.DESCRIPTION
Bootstrap invokes the distribution-specific PowerShell installer. All subsequent
phases use run_ps_validation_phase.ps1 with the same toolchain and module directory.
Unsupported combinations report native command failures as SucceededWithIssues and
set PsValidationSoftFailed so later phase tasks can be skipped.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Bootstrap', 'Install', 'Upgrade', 'Validate', 'Uninstall')]
    [string]$Phase,

    [Parameter(Mandatory)]
    [ValidateSet('container', 'native')]
    [string]$ValidationMode,

    [Parameter(Mandatory)]
    [string]$DistributionFamily,

    [Parameter(Mandatory)]
    [string]$SourceRoot,

    [Parameter(Mandatory)]
    [string]$ArtifactsRoot,

    [Parameter(Mandatory)]
    [string]$WorkRoot,

    [string]$DistributionScript,
    [string]$DistributionName,
    [string]$PowerShellVersion,
    [string]$DotnetRuntimeVersion,
    [string]$ContainerImage,
    [string]$ContainerName,
    [string]$PwshPath,
    [string]$DotnetRootPath,

    [ValidateSet('true', 'false')]
    [string]$ContinueOnError = 'false'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:POWERSHELL_TELEMETRY_OPTOUT = '1'
$script:exitCode = 0

function Invoke-ValidationCommand
{
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $global:LASTEXITCODE = 0
    & $FilePath @Arguments
    $script:exitCode = $LASTEXITCODE
}

function Set-ValidationVariable
{
    param([string]$Name, [string]$Value)

    $escaped = $Value.Replace('%', '%AZP25').Replace("`r", '%0D').Replace("`n", '%0A')
    Write-Host "##vso[task.setvariable variable=$Name]$escaped"
}

if ($Phase -eq 'Bootstrap')
{
    if ($ValidationMode -eq 'container')
    {
        Invoke-ValidationCommand -FilePath 'docker' -Arguments @('pull', $ContainerImage)
        if ($script:exitCode -eq 0)
        {
            Invoke-ValidationCommand -FilePath 'docker' -Arguments @(
                'run', '--detach', '--name', $ContainerName, '--entrypoint', '/bin/sh',
                '-e', 'DOTNET_CLI_TELEMETRY_OPTOUT=1',
                '-e', 'POWERSHELL_TELEMETRY_OPTOUT=1',
                '-e', 'DOTNET_ROOT=/opt/dotnet',
                '-v', "${SourceRoot}:/src:ro", '-v', "${ArtifactsRoot}:/artifacts:ro",
                $ContainerImage, '-c', 'while true; do sleep 3600; done'
            )
        }
        if ($script:exitCode -eq 0)
        {
            # A successful exec also establishes that the detached container is running.
            Invoke-ValidationCommand -FilePath 'docker' -Arguments @(
                'exec', $ContainerName, 'sh', "/src/$DistributionScript",
                '/src', $PowerShellVersion, $DotnetRuntimeVersion, '/opt'
            )
        }
        $PwshPath = '/opt/pwsh/pwsh'
        $DotnetRootPath = '/opt/dotnet'
    }
    else
    {
        $installer = Join-Path $SourceRoot $DistributionScript
        if ($DistributionFamily -eq 'windows')
        {
            Invoke-ValidationCommand -FilePath 'pwsh' -Arguments @(
                '-NoProfile', '-File', $installer, '-SourceRoot', $SourceRoot,
                '-PowerShellVersion', $PowerShellVersion,
                '-DotnetRuntimeVersion', $DotnetRuntimeVersion, '-WorkRoot', $WorkRoot
            )
            $pwshName = 'pwsh.exe'
        }
        else
        {
            Invoke-ValidationCommand -FilePath 'sh' -Arguments @(
                $installer, $SourceRoot, $PowerShellVersion, $DotnetRuntimeVersion, $WorkRoot
            )
            $pwshName = 'pwsh'
        }
        $PwshPath = Join-Path (Join-Path $WorkRoot 'pwsh') $pwshName
        $DotnetRootPath = Join-Path $WorkRoot 'dotnet'
    }

    if ($script:exitCode -eq 0)
    {
        Set-ValidationVariable -Name 'PwshPath' -Value $PwshPath
        Set-ValidationVariable -Name 'DotnetRootPath' -Value $DotnetRootPath
    }
}
else
{
    $phaseScript = 'devops/scripts/packaging/powershell/run_ps_validation_phase.ps1'
    if ($ValidationMode -eq 'container')
    {
        # Each exec starts with the container's environment, not the bootstrap's PATH.
        # The PowerShell global-tool shim needs to find dotnet by name.
        Invoke-ValidationCommand -FilePath 'docker' -Arguments @(
            'exec', $ContainerName, 'sh', '-c',
            'export PATH="$DOTNET_ROOT:$PATH"; exec "$@"', '_',
            $PwshPath, '-NoProfile', '-File', "/src/$phaseScript", '-Phase', $Phase,
            '-RepositoryRoot', '/artifacts/artifacts/PSGallery/local-repo',
            '-SourceRoot', '/src', '-PwshPath', $PwshPath
        )
    }
    else
    {
        $env:DOTNET_ROOT = $DotnetRootPath
        $env:PATH = $DotnetRootPath + [System.IO.Path]::PathSeparator + $env:PATH
        Invoke-ValidationCommand -FilePath $PwshPath -Arguments @(
            '-NoProfile', '-File', (Join-Path $SourceRoot $phaseScript),
            '-Phase', $Phase,
            '-RepositoryRoot', (Join-Path $ArtifactsRoot 'artifacts/PSGallery/local-repo'),
            '-SourceRoot', $SourceRoot, '-PwshPath', $PwshPath
        )
    }
}

if ($script:exitCode -ne 0 -and $ContinueOnError -eq 'true')
{
    Write-Host "##vso[task.logissue type=warning]$DistributionName failed phase '$Phase' with exit code $script:exitCode."
    Set-ValidationVariable -Name 'PsValidationSoftFailed' -Value 'true'
    Write-Host '##vso[task.complete result=SucceededWithIssues;]'
    exit 0
}

exit $script:exitCode
