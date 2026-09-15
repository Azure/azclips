<#
.SYNOPSIS
Offline assertion fixtures for runtime dependency validation; no older runtime is installed.
.DESCRIPTION
Uses synthetic command results for production orchestration and real local children
for deadlines, stream capture, manifest guards, and an available .NET native diagnostic.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $repoRoot 'devops/scripts/packaging/powershell/shared/dependency_runtime.ps1')
$nativeProcess = ${function:Invoke-PSRuntimeProcess}
$scratchParent = Get-PSRuntimeScratchRoot
$scratch = Join-Path $scratchParent ".ps-runtime-fixtures-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $scratch
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$count = 0

function Assert-RuntimeFixture
{
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw "Runtime fixture failed: $Message" }
}

function Assert-RuntimeFixtureThrows
{
    param([scriptblock]$Action, [string]$Pattern)
    $caught = $null
    try { & $Action | Out-Null }
    catch { $caught = $_ }
    Assert-RuntimeFixture ($null -ne $caught) "Expected failure matching '$Pattern'."
    Assert-RuntimeFixture ($caught.Exception.Message -match $Pattern) "Wrong failure: $($caught.Exception.Message)"
}

function New-RuntimeFixtureResult
{
    param([int]$ExitCode = 0, [string]$StdOut = '', [string]$StdErr = '', [bool]$TimedOut = $false)
    [pscustomobject]@{
        ExitCode = $ExitCode; StdOut = $StdOut; StdErr = $StdErr
        Output = "$StdOut`n$StdErr"; TimedOut = $TimedOut; Truncated = $false; DurationSeconds = 0.01
    }
}

try
{
    foreach ($kind in 'MissingFramework', 'InstallerVersion', 'ManifestVersion')
    {
        Assert-RuntimeFixtureThrows { Assert-PSRuntimeResult (New-RuntimeFixtureResult) $kind -ModuleName Az.Fixture } 'unexpectedly succeeded'
        Assert-RuntimeFixtureThrows { Assert-PSRuntimeResult (New-RuntimeFixtureResult -ExitCode 1 -StdErr 'Permission denied loading Microsoft.NETCore.App Az.Fixture PowerShell 7.6') $kind -ModuleName Az.Fixture } 'unrelated reason'
        Assert-RuntimeFixtureThrows { Assert-PSRuntimeResult (New-RuntimeFixtureResult -ExitCode 1 -TimedOut $true) $kind -ModuleName Az.Fixture } 'timed out'
    }
    $missing = "You must install or update .NET to run this application.`nFramework: 'Microsoft.NETCore.App', version '10.0.0' (x64)`nNo frameworks were found."
    $installerError = 'Azclips PowerShell modules require PowerShell 7.6 or later. Current PowerShell version is 7.5.7. Install or update PowerShell from https://aka.ms/powershell.'
    $manifestError = 'Modules_InsufficientPowerShellVersion,Microsoft.PowerShell.Commands.ImportModuleCommand: Az.Fixture requires PowerShell 7.6.'
    Assert-PSRuntimeResult (New-RuntimeFixtureResult -ExitCode 150 -StdErr $missing) MissingFramework
    Assert-PSRuntimeResult (New-RuntimeFixtureResult -ExitCode 1 -StdErr $installerError) InstallerVersion
    Assert-PSRuntimeResult (New-RuntimeFixtureResult -ExitCode 1 -StdErr $manifestError) ManifestVersion -ModuleName Az.Fixture
    Assert-RuntimeFixtureThrows {
        Assert-PSRuntimeResult (New-RuntimeFixtureResult -ExitCode 1 -StdErr 'Modules_ModuleNotFound: Az.Fixture requires PowerShell 7.6; assembly missing') ManifestVersion -ModuleName Az.Fixture
    } 'unrelated reason'
    Write-Host 'PASS: intended diagnostics only; unrelated failures, unexpected success and timeouts fail'
    $count++

    $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand',
        (ConvertTo-PSRuntimeEncodedCommand '[Console]::Out.WriteLine("STDOUT"); [Console]::Error.WriteLine("STDERR"); exit 42'))
    $result = Invoke-PSRuntimeProcess -FilePath $pwsh -Arguments $arguments -WorkingDirectory $scratch -TimeoutSeconds 30
    Assert-RuntimeFixture ($result.ExitCode -eq 42 -and $result.StdOut -match 'STDOUT' -and $result.StdErr -match 'STDERR') 'Native exit code and both output streams are retained.'
    $arguments[-1] = ConvertTo-PSRuntimeEncodedCommand 'Start-Sleep -Seconds 30'
    $result = Invoke-PSRuntimeProcess -FilePath $pwsh -Arguments $arguments -WorkingDirectory $scratch -TimeoutSeconds 1
    Assert-RuntimeFixture ($result.TimedOut -and $result.DurationSeconds -lt 10) 'Child process is killed at its deadline.'
    $arguments[-1] = ConvertTo-PSRuntimeEncodedCommand '[Console]::Out.Write(("x" * 2200000)); [Console]::Error.Write(("y" * 2200000))'
    $result = Invoke-PSRuntimeProcess -FilePath $pwsh -Arguments $arguments -WorkingDirectory $scratch -TimeoutSeconds 30
    Assert-RuntimeFixture ($result.ExitCode -eq 0 -and $result.Truncated -and $result.StdOut.Length -eq 2MB -and $result.StdErr.Length -eq 2MB) 'Large streams are drained concurrently with bounded capture.'
    Assert-RuntimeFixtureThrows {
        Invoke-PSRuntimeProcess -FilePath (Join-Path $scratch 'no-such-executable') -WorkingDirectory $scratch -TimeoutSeconds 1 -WarningAction SilentlyContinue
    } 'starting process|start'
    Write-Host 'PASS: real bounded native execution, stream draining and capture limits'
    $count++

    $source = Join-Path $scratch 'source'
    $gallery = Join-Path $scratch 'gallery'
    $packageRoot = Join-Path $scratch 'package'
    $toolRoot = Join-Path $scratch 'primary/dotnet'
    $fxr = Join-Path $toolRoot 'host/fxr/10.0.0'
    $null = New-Item -ItemType Directory -Path $source, $gallery, $packageRoot, $fxr -Force
    Set-Content -LiteralPath (Join-Path $source 'Fixture.psd1') -Value "@{ ModuleVersion = '1.2.3'; PowerShellVersion = '7.5' }"
    Set-Content -LiteralPath (Join-Path $packageRoot 'Az.Fixture.psd1') -Value "@{ ModuleVersion = '1.2.3'; PowerShellVersion = '7.6'; RootModule = 'missing.dll'; RequiredModules = @('Absent.Dependency') }"
    $packagePath = Join-Path $gallery 'Az.Fixture.1.2.3.nupkg'
    [IO.Compression.ZipFile]::CreateFromDirectory($packageRoot, $packagePath)
    $manifest = @(Get-PSRuntimePackageManifests $source $gallery (Join-Path $scratch 'extracted'))[0]
    Assert-RuntimeFixture ($manifest.MinimumPowerShellVersion -eq '7.6') 'Minimum runtime comes from the package, not the legacy source manifest.'
    Assert-RuntimeFixture ((Get-FileHash $manifest.ManifestPath).Hash -eq (Get-FileHash (Join-Path $packageRoot 'Az.Fixture.psd1')).Hash) 'Extracted manifest bytes are unchanged.'

    $future = "$($PSVersionTable.PSVersion.Major).$($PSVersionTable.PSVersion.Minor + 1)"
    $futureManifest = Join-Path $scratch 'Az.Future.psd1'
    Set-Content -LiteralPath $futureManifest -Value "@{ ModuleVersion = '1.0.0'; PowerShellVersion = '$future'; RootModule = 'missing.dll'; RequiredModules = @('Absent.Dependency') }"
    $escaped = $futureManifest.Replace("'", "''")
    $command = "try { Import-Module '$escaped' -ErrorAction Stop; exit 0 } catch { [Console]::Error.WriteLine(`"`$(`$_.FullyQualifiedErrorId): `$(`$_.Exception.Message)`"); exit 1 }"
    $arguments[-1] = ConvertTo-PSRuntimeEncodedCommand $command
    $result = Invoke-PSRuntimeProcess -FilePath $pwsh -Arguments $arguments -WorkingDirectory $scratch -TimeoutSeconds 30
    Assert-PSRuntimeResult $result ManifestVersion -ModuleName Az.Future -MinimumVersion $future
    Write-Host 'PASS: packaged manifest identity and real engine version guard precedes missing assemblies/dependencies'
    $count++

    $dotnetCommand = Get-Command dotnet -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($dotnetCommand)
    {
        $nativeDotnet = Split-Path $dotnetCommand.Source -Parent
        # Some Unix installations expose a symlink in PATH; resolve it only to locate the installed root.
        $dotnetItem = Get-Item -LiteralPath $dotnetCommand.Source
        if ($dotnetItem.LinkType) { $nativeDotnet = Split-Path ($dotnetItem.ResolveLinkTarget($true).FullName) -Parent }
        $privateRoot = Join-Path $scratch 'native-frameworkless'
        $hostPath = New-PSRuntimeFrameworklessHost $nativeDotnet $privateRoot
        $config = Join-Path $scratch 'framework-dependent.runtimeconfig.json'
        [IO.File]::WriteAllText($config, '{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}')
        $environment = Get-PSRuntimeEnvironment $privateRoot $scratch
        $result = Invoke-PSRuntimeProcess -FilePath $hostPath -Arguments @('exec', '--runtimeconfig', $config, (Join-Path $PSHOME 'pwsh.dll')) `
            -Environment $environment -WorkingDirectory $scratch -TimeoutSeconds 30
        Assert-PSRuntimeResult $result MissingFramework
        Assert-RuntimeFixture (-not (Test-Path (Join-Path $privateRoot 'shared'))) 'Private native host has no shared frameworks.'
        Assert-RuntimeFixture (@(Get-ChildItem $privateRoot -File -Recurse).Count -eq 2) 'Frameworkless copy contains only dotnet and one hostfxr library.'
        Write-Host 'PASS: actual native Microsoft.NETCore.App missing-framework diagnostic (explicit fixture runtimeconfig)'
        $count++
    }
    else { Write-Host 'SKIP: native missing-framework fixture; no local dotnet executable is available' }

    $executableName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $libraryName = if ($IsWindows) { 'hostfxr.dll' } elseif ($IsMacOS) { 'libhostfxr.dylib' } else { 'libhostfxr.so' }
    Set-Content -LiteralPath (Join-Path $toolRoot $executableName) -Value 'offline executable placeholder'
    Set-Content -LiteralPath (Join-Path $fxr $libraryName) -Value 'offline hostfxr placeholder'
    $installerName = if ($IsWindows) { 'dotnet-install.ps1' } else { 'dotnet-install.sh' }
    Set-Content -LiteralPath (Join-Path (Split-Path $toolRoot -Parent) $installerName) -Value 'offline installer placeholder'
    $beforeFiles = @(Get-ChildItem $toolRoot -File -Recurse | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress)
    $beforePackages = (Get-FileHash $packagePath).Hash
    $beforeEnvironment = "$env:DOTNET_ROOT|$env:PATH|$env:PSModulePath"
    $beforeScratch = @(Get-ChildItem $scratchParent -Directory -Filter '.ps-runtime-dependencies-*').FullName -join '|'
    $script:runtimeFixtureMode = 'Success'
    $script:runtimeFixtureCalls = [Collections.Generic.List[object]]::new()

    function Invoke-PSRuntimeProcess
    {
        param($FilePath, $Arguments, $WorkingDirectory, $Environment, $TimeoutSeconds)
        $script:runtimeFixtureCalls.Add(@{ FilePath = $FilePath; Arguments = $Arguments; Environment = $Environment; WorkingDirectory = $WorkingDirectory })
        Assert-RuntimeFixture ((Split-Path $WorkingDirectory -Parent) -eq $scratchParent) 'Runtime scratch uses the temp directory, never the read-only checkout.'
        $encodedIndex = [array]::IndexOf($Arguments, '-EncodedCommand')
        $decoded = if ($encodedIndex -ge 0) { [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($Arguments[$encodedIndex + 1])) } else { '' }
        if ($decoded -match 'Invoke-WebRequest')
        {
            Assert-RuntimeFixture ($decoded -match 'https://dot\.net/v1/dotnet-install\.(ps1|sh)') 'Installer fallback uses the official URL.'
            if ($script:runtimeFixtureMode -eq 'DownloadFailure') { return New-RuntimeFixtureResult -ExitCode 1 -StdErr 'Installer download failed' }
            Set-Content -LiteralPath (Join-Path $WorkingDirectory $installerName) -Value 'offline downloaded installer placeholder'
            return New-RuntimeFixtureResult
        }
        if ($FilePath -eq (Join-Path $toolRoot $executableName))
        {
            Assert-RuntimeFixture ($Arguments -contains '--tool-path' -and $Arguments -contains '7.5.*') 'Tool install uses primary SDK with a private explicit tool path.'
            Assert-RuntimeFixture ($Environment.DOTNET_ROOT -eq $toolRoot) 'Primary SDK install uses primary root, unchanged.'
            if ($script:runtimeFixtureMode -eq 'ProvisionFailure') { return New-RuntimeFixtureResult -ExitCode 1 -StdErr 'NuGet download failed' }
            return New-RuntimeFixtureResult
        }
        if ($FilePath -match 'frameworkless')
        {
            Assert-RuntimeFixture ($Environment.DOTNET_MULTILEVEL_LOOKUP -eq '0' -and $Environment.DOTNET_ROOT_X64 -eq $Environment.DOTNET_ROOT) 'Framework probe disables fallback and sets architecture root.'
            Assert-RuntimeFixture ($Arguments[0] -eq (Join-Path $PSHOME 'pwsh.dll')) 'Framework probe launches actual discovered target DLL.'
            switch ($script:runtimeFixtureMode)
            {
                'UnexpectedSuccess' { return New-RuntimeFixtureResult }
                'UnrelatedFailure' { return New-RuntimeFixtureResult -ExitCode 1 -StdErr 'hostfxr library could not be loaded' }
                'Timeout' { return New-RuntimeFixtureResult -ExitCode 1 -StdErr $missing -TimedOut $true }
                default { return New-RuntimeFixtureResult -ExitCode 150 -StdErr $missing }
            }
        }
        if ($Arguments -contains $installerName -or ($Arguments -join ' ') -match 'dotnet-install\.(ps1|sh)')
        {
            Assert-RuntimeFixture ($Arguments -contains '9.0') 'Older .NET provision requests runtime channel 9.0.'
            Assert-RuntimeFixture (($Arguments -join ' ') -match 'dotnet9') 'Installer destination is private dotnet9 scratch.'
            return New-RuntimeFixtureResult
        }
        if ($decoded -match 'PowerShellVersion =')
        {
            $older = $FilePath -match 'powershell75'
            $runtime = @{
                PowerShellVersion = if ($older) { '7.5.7' } else { '7.6.0' }
                DotnetVersion = if ($older) { '9.0.5' } else { '10.0.0' }
                Framework = if ($older) { '.NET 9.0.5' } else { '.NET 10.0.0' }
                Architecture = 'X64'
                PSHome = if ($older) { Join-Path $WorkingDirectory 'powershell75/.store/powershell' } else { $PSHOME }
                RuntimeDirectory = if ($older) { Join-Path $WorkingDirectory 'dotnet9/shared/Microsoft.NETCore.App/9.0.5' } else { Join-Path $toolRoot 'shared/Microsoft.NETCore.App/10.0.0' }
            }
            if ($older)
            {
                Assert-RuntimeFixture ($Environment.DOTNET_ROOT -eq (Join-Path $WorkingDirectory 'dotnet9')) 'Older child uses only the private runtime root.'
                Assert-RuntimeFixture ($Environment.PATH.StartsWith($Environment.DOTNET_ROOT)) 'Older root is first on PATH.'
                if ($script:runtimeFixtureMode -eq 'WrongOlderVersion') { $runtime.PowerShellVersion = '7.6.0' }
                if ($script:runtimeFixtureMode -eq 'AmbientOlderRuntime') { $runtime.RuntimeDirectory = Join-Path $toolRoot 'shared/Microsoft.NETCore.App/9.0.5' }
            }
            if (-not $older -and $script:runtimeFixtureMode -eq 'UnsupportedTarget') { $runtime.PowerShellVersion = '7.5.7' }
            return New-RuntimeFixtureResult -StdOut (ConvertTo-Json $runtime -Compress)
        }
        if ($decoded -match 'InstallModule\.ps1')
        {
            Assert-RuntimeFixture ($decoded -match 'RUNTIME_PROBE_SIDE_EFFECT_BLOCKED') 'Installer guard probe blocks real side effects on regression.'
            if ($script:runtimeFixtureMode -eq 'InstallerSuccess') { return New-RuntimeFixtureResult }
            return New-RuntimeFixtureResult -ExitCode 1 -StdErr $installerError
        }
        if ($decoded -match 'Import-Module')
        {
            Assert-RuntimeFixture ($decoded -match 'manifests' -and $decoded -match 'Az\.Fixture\.psd1') 'Older import uses extracted package manifest.'
            if ($script:runtimeFixtureMode -eq 'ManifestUnrelated') { return New-RuntimeFixtureResult -ExitCode 1 -StdErr 'Required assembly missing' }
            return New-RuntimeFixtureResult -ExitCode 1 -StdErr $manifestError
        }
        throw "Unexpected offline command: $FilePath $($Arguments -join ' ') $decoded"
    }

    $parameters = @{
        SourcePsRoot = $source; RepositoryRoot = $gallery; PwshPath = $pwsh; DotnetRootPath = $toolRoot
        InstallScriptPath = Join-Path $repoRoot 'devops/scripts/packaging/powershell/InstallModule.ps1'
        TimeoutSeconds = 10
    }
    $reports = Join-Path $scratch 'reports'
    $report = Invoke-PSRuntimeValidation @parameters -ReportRoot $reports
    Assert-RuntimeFixture ($report.status -eq 'Succeeded' -and $report.cases.Count -eq 5) 'All five runtime cases execute with strict expected rejections.'
    $persisted = Get-Content (Join-Path $reports 'runtime-dependencies.json') -Raw | ConvertFrom-Json
    Assert-RuntimeFixture ($persisted.status -eq 'Succeeded' -and $persisted.completedAt) 'Aggregate result persists.'
    foreach ($case in $persisted.cases)
    {
        foreach ($observation in $case.observations)
        {
            Assert-RuntimeFixture ((Test-Path (Join-Path $reports $observation.stdout)) -and (Test-Path (Join-Path $reports $observation.stderr))) 'Each command retains both stream logs.'
        }
    }
    $null = Invoke-PSRuntimeValidation @parameters
    Remove-Item -LiteralPath (Join-Path (Split-Path $toolRoot -Parent) $installerName)
    $null = Invoke-PSRuntimeValidation @parameters
    $script:runtimeFixtureMode = 'DownloadFailure'
    Assert-RuntimeFixtureThrows { Invoke-PSRuntimeValidation @parameters -ReportRoot $reports } 'Installer download failed'
    Set-Content -LiteralPath (Join-Path (Split-Path $toolRoot -Parent) $installerName) -Value 'offline installer placeholder'
    Write-Host 'PASS: complete offline orchestration, optional reporting, separate logs and private environment contract'
    $count++

    foreach ($mode in 'UnexpectedSuccess', 'UnrelatedFailure', 'Timeout', 'ProvisionFailure', 'WrongOlderVersion', 'AmbientOlderRuntime', 'UnsupportedTarget', 'InstallerSuccess', 'ManifestUnrelated')
    {
        $script:runtimeFixtureMode = $mode
        Assert-RuntimeFixtureThrows { Invoke-PSRuntimeValidation @parameters -ReportRoot $reports } 'runtime dependency validation failed'
        $persisted = Get-Content (Join-Path $reports 'runtime-dependencies.json') -Raw | ConvertFrom-Json
        Assert-RuntimeFixture ($persisted.status -eq 'Failed') "$mode must persist a failed aggregate."
        if ($mode -in 'ProvisionFailure', 'WrongOlderVersion', 'AmbientOlderRuntime')
        {
            Assert-RuntimeFixture (($persisted.cases | Where-Object name -eq 'older-installer-rejection').status -eq 'Failed') 'Provisioning failure cannot count as installer rejection.'
        }
        Assert-RuntimeFixture ((@(Get-ChildItem $scratchParent -Directory -Filter '.ps-runtime-dependencies-*').FullName -join '|') -eq $beforeScratch) "$mode cleans only its own generated scratch."
    }
    $script:runtimeFixtureMode = 'UnexpectedSuccess'
    Assert-RuntimeFixtureThrows { Invoke-PSRuntimeValidation @parameters } 'unexpectedly succeeded'
    Assert-RuntimeFixtureThrows { Invoke-PSRuntimeValidation @parameters -ReportRoot $gallery } 'reports must be outside'
    function Remove-Item
    {
        [CmdletBinding()]
        param([string]$LiteralPath, [switch]$Recurse, [switch]$Force)
        if ((Split-Path $LiteralPath -Leaf) -like '.ps-runtime-dependencies-*') { throw 'INJECTED_CLEANUP_FAILURE' }
        Microsoft.PowerShell.Management\Remove-Item -LiteralPath $LiteralPath -Recurse:$Recurse -Force:$Force
    }
    try
    {
        Assert-RuntimeFixtureThrows { Invoke-PSRuntimeValidation @parameters -ReportRoot $reports -WarningAction SilentlyContinue } 'unexpectedly succeeded'
    }
    finally
    {
        Microsoft.PowerShell.Management\Remove-Item Function:\Remove-Item
        $generatedScratch = $script:runtimeFixtureCalls[$script:runtimeFixtureCalls.Count - 1].WorkingDirectory
        Remove-Item -LiteralPath $generatedScratch -Recurse -Force
    }
    $persisted = Get-Content (Join-Path $reports 'runtime-dependencies.json') -Raw | ConvertFrom-Json
    Assert-RuntimeFixture ($persisted.status -eq 'Failed' -and ($persisted.cases | Where-Object name -eq 'missing-framework').message -match 'unexpectedly succeeded') 'Cleanup failure preserves the original probe failure and report.'
    Assert-RuntimeFixture (($beforeFiles -join '') -ceq (@(Get-ChildItem $toolRoot -File -Recurse | Get-FileHash | Select-Object Path, Hash | ConvertTo-Json -Compress) -join '')) 'Primary toolchain files are unchanged.'
    Assert-RuntimeFixture ((Get-FileHash $packagePath).Hash -eq $beforePackages) 'Gallery package is unchanged.'
    Assert-RuntimeFixture ("$env:DOTNET_ROOT|$env:PATH|$env:PSModulePath" -ceq $beforeEnvironment) 'Parent environment is unchanged.'
    Write-Host 'PASS: failures persist, setup cannot masquerade as rejection, no-report assertions, cleanup and input isolation'
    $count++
}
finally
{
    ${function:Invoke-PSRuntimeProcess} = $nativeProcess
    Remove-Item -LiteralPath $scratch -Recurse -Force
}
Write-Host "PASS: $count PowerShell runtime dependency fixture groups (offline; no older runtime installed)."
