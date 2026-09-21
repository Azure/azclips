. (Join-Path $PSScriptRoot 'phase_artifacts.ps1')
. (Join-Path $PSScriptRoot 'module_artifacts.ps1')

function Get-PSRuntimeScratchRoot
{
    $root = if ($env:AGENT_TEMPDIRECTORY) { $env:AGENT_TEMPDIRECTORY }
        elseif ($env:RUNNER_TEMP) { $env:RUNNER_TEMP }
        else { [IO.Path]::GetTempPath() }
    # Resolve system temp aliases (for example macOS /var) before link-safe cleanup.
    $fullPath = [IO.Path]::GetFullPath($root)
    $resolved = [IO.Path]::GetPathRoot($fullPath)
    foreach ($segment in $fullPath.Substring($resolved.Length).Split([IO.Path]::DirectorySeparatorChar, [StringSplitOptions]::RemoveEmptyEntries))
    {
        $directory = Get-Item -LiteralPath (Join-Path $resolved $segment) -Force -ErrorAction Stop
        $resolved = if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { $directory.ResolveLinkTarget($true).FullName } else { $directory.FullName }
    }
    return $resolved
}

function Invoke-PSRuntimeProcess
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [hashtable]$Environment = @{},
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
    )

    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.WorkingDirectory = $WorkingDirectory
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $start.Environment[$key] = [string]$Environment[$key] }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $timedOut = $false
    $truncated = $false
    $texts = @([Text.StringBuilder]::new(), [Text.StringBuilder]::new())
    $limit = 2MB
    try
    {
        if (-not $process.Start()) { throw "Could not start '$FilePath'." }
        $readers = @($process.StandardOutput, $process.StandardError)
        $buffers = @([char[]]::new(4096), [char[]]::new(4096))
        $pending = @($readers[0].ReadAsync($buffers[0], 0, 4096), $readers[1].ReadAsync($buffers[1], 0, 4096))
        while ($true)
        {
            foreach ($index in 0, 1)
            {
                if ($null -ne $pending[$index] -and $pending[$index].IsCompleted)
                {
                    $length = $pending[$index].GetAwaiter().GetResult()
                    $pending[$index] = $null
                    if ($length -gt 0)
                    {
                        $keep = [Math]::Min($length, $limit - $texts[$index].Length)
                        if ($keep -gt 0) { $null = $texts[$index].Append($buffers[$index], 0, $keep) }
                        if ($keep -lt $length) { $truncated = $true }
                        $pending[$index] = $readers[$index].ReadAsync($buffers[$index], 0, 4096)
                    }
                }
            }
            if ($process.HasExited -and $null -eq $pending[0] -and $null -eq $pending[1]) { break }
            if ($watch.Elapsed.TotalSeconds -ge $TimeoutSeconds)
            {
                $timedOut = $true
                if (-not $process.HasExited) { $process.Kill($true) }
                $null = $process.WaitForExit(2000)
                break
            }
            Start-Sleep -Milliseconds 10
        }
        $exitCode = if ($process.HasExited) { $process.ExitCode } else { -1 }
        [pscustomobject]@{
            ExitCode = $exitCode
            TimedOut = $timedOut
            Truncated = $truncated
            StdOut = $texts[0].ToString()
            StdErr = $texts[1].ToString()
            Output = $texts[0].ToString() + "`n" + $texts[1].ToString()
            DurationSeconds = $watch.Elapsed.TotalSeconds
        }
    }
    finally
    {
        try { if ($process.Id -and -not $process.HasExited) { $process.Kill($true) } }
        catch { Write-Warning "Runtime child cleanup failed: $_" }
        $process.Dispose()
        $watch.Stop()
    }
}

function Assert-PSRuntimeResult
{
    param(
        [Parameter(Mandatory)]$Result,
        [Parameter(Mandatory)][ValidateSet('Success', 'MissingFramework', 'InstallerVersion', 'ManifestVersion')][string]$Kind,
        [string]$ModuleName,
        [string]$MinimumVersion = '7.6'
    )

    if ($Result.TimedOut) { throw "$Kind probe timed out; a timeout is not an expected rejection." }
    if ($Result.Truncated) { throw "$Kind diagnostic exceeded the capture limit." }
    if ($Kind -eq 'Success')
    {
        if ($Result.ExitCode -ne 0) { throw "Runtime command failed with exit code $($Result.ExitCode): $($Result.Output)" }
        return
    }
    if ($Result.ExitCode -eq 0) { throw "$Kind probe unexpectedly succeeded." }
    $text = $Result.Output
    $matchesDiagnostic = switch ($Kind)
    {
        'MissingFramework' {
            $text -match "(?im)^\s*Framework:\s*['`"]Microsoft\.NETCore\.App['`"]" -and
                $text -match '(?i)(No frameworks were found|You must install or update \.NET)'
        }
        'InstallerVersion' {
            $text -match 'Azclips PowerShell modules require PowerShell 7\.6 or later' -and
                $text -match 'Current PowerShell version is 7\.5'
        }
        'ManifestVersion' {
            $text -match 'Modules_InsufficientPowerShellVersion' -and
                $text -match [regex]::Escape($ModuleName) -and
                $text -match [regex]::Escape($MinimumVersion) -and
                $text -match '(?i)PowerShell'
        }
    }
    if (-not $matchesDiagnostic)
    {
        throw "$Kind probe failed for an unrelated reason (exit $($Result.ExitCode)): $text"
    }
}

function Get-PSRuntimeEnvironment
{
    param([Parameter(Mandatory)][string]$DotnetRoot, [Parameter(Mandatory)][string]$ScratchRoot)

    $environment = @{
        DOTNET_ROOT = $DotnetRoot
        'DOTNET_ROOT(x86)' = $DotnetRoot
        DOTNET_ROOT_X64 = $DotnetRoot
        DOTNET_ROOT_X86 = $DotnetRoot
        DOTNET_ROOT_ARM64 = $DotnetRoot
        DOTNET_ROOT_ARM = $DotnetRoot
        DOTNET_HOST_PATH = Join-Path $DotnetRoot $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
        DOTNET_MULTILEVEL_LOOKUP = '0'
        DOTNET_ROLL_FORWARD = 'LatestPatch'
        DOTNET_ROLL_FORWARD_TO_PRERELEASE = '0'
        DOTNET_ADDITIONAL_DEPS = ''
        DOTNET_SHARED_STORE = ''
        DOTNET_STARTUP_HOOKS = ''
        DOTNET_CLI_UI_LANGUAGE = 'en-US'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
        DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
        DOTNET_NOLOGO = '1'
        DOTNET_CLI_HOME = Join-Path $ScratchRoot 'cli-home'
        NUGET_PACKAGES = Join-Path $ScratchRoot 'nuget-packages'
        NUGET_HTTP_CACHE_PATH = Join-Path $ScratchRoot 'nuget-http'
        DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $ScratchRoot 'bundles'
        PATH = $DotnetRoot + [IO.Path]::PathSeparator + $env:PATH
        NO_COLOR = '1'
        TERM = 'dumb'
    }
    return $environment
}

function Assert-PSRuntimeRegularPath
{
    param([Parameter(Mandatory)][string]$Path)

    $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
    while ($null -ne $item)
    {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
        {
            throw "Runtime validation does not follow symbolic links or reparse points: $($item.FullName)"
        }
        $item = if ($item -is [IO.DirectoryInfo]) { $item.Parent } else { $item.Directory }
    }
}

function New-PSRuntimeFrameworklessHost
{
    param([Parameter(Mandatory)][string]$DotnetRoot, [Parameter(Mandatory)][string]$Destination)

    $executableName = if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' }
    $libraryName = if ($IsWindows) { 'hostfxr.dll' } elseif ($IsMacOS) { 'libhostfxr.dylib' } else { 'libhostfxr.so' }
    $sourceExecutable = Join-Path $DotnetRoot $executableName
    $fxrRoot = Join-Path $DotnetRoot 'host/fxr'
    Assert-PSRuntimeRegularPath $sourceExecutable
    Assert-PSRuntimeRegularPath $fxrRoot
    $fxr = Get-ChildItem -LiteralPath $fxrRoot -Directory |
        Where-Object { ($_.Name.Split('-')[0] -as [version]) } |
        Sort-Object { [version]$_.Name.Split('-')[0] } -Descending |
        Select-Object -First 1
    if (-not $fxr) { throw "No host/fxr version found in primary .NET root '$DotnetRoot'." }
    $sourceLibrary = Join-Path $fxr.FullName $libraryName
    Assert-PSRuntimeRegularPath $sourceLibrary
    if (Test-Path -LiteralPath $Destination) { throw "Frameworkless destination already exists: $Destination" }
    $targetFxr = Join-Path $Destination "host/fxr/$($fxr.Name)"
    $null = New-Item -ItemType Directory -Path $targetFxr -Force
    Copy-Item -LiteralPath $sourceExecutable -Destination (Join-Path $Destination $executableName)
    Copy-Item -LiteralPath $sourceLibrary -Destination (Join-Path $targetFxr $libraryName)
    if (-not $IsWindows)
    {
        [IO.File]::SetUnixFileMode((Join-Path $Destination $executableName), [IO.File]::GetUnixFileMode($sourceExecutable))
    }
    return (Join-Path $Destination $executableName)
}

function Get-PSRuntimePackageManifests
{
    param(
        [Parameter(Mandatory)][string]$SourcePsRoot,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$Destination
    )

    $sources = @(Get-ChildItem -LiteralPath $SourcePsRoot -File -Filter '*.psd1' -Recurse)
    if ($sources.Count -eq 0) { throw "No source module manifests found under '$SourcePsRoot'." }
    foreach ($source in $sources)
    {
        Assert-PSRuntimeRegularPath $source.FullName
        if ($source.Length -gt 1MB) { throw "Source manifest is too large: $($source.FullName)" }
        $definition = Import-PowerShellDataFile -LiteralPath $source.FullName
        $name = if ($source.BaseName -eq 'Az' -or $source.BaseName.StartsWith('Az.')) { $source.BaseName } else { "Az.$($source.BaseName)" }
        $version = [string]$definition.ModuleVersion
        if (-not ($version -as [version])) { throw "Invalid source ModuleVersion: $($source.FullName)" }
        $package = Join-Path $RepositoryRoot "$name.$version.nupkg"
        Assert-PSRuntimeRegularPath $package
        $archive = [IO.Compression.ZipFile]::OpenRead($package)
        try
        {
            $entries = @($archive.Entries | Where-Object FullName -eq "$name.psd1")
            if ($entries.Count -ne 1) { throw "Expected exactly one root manifest '$name.psd1' in '$package'." }
            $entry = $entries[0]
            if ($entry.Length -gt 1MB -or (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000)
            {
                throw "Packaged manifest is oversized or a symbolic link: $package"
            }
            $moduleDirectory = Join-Path $Destination $name
            $null = New-Item -ItemType Directory -Path $moduleDirectory -Force
            $manifest = Join-Path $moduleDirectory "$name.psd1"
            # Only the actual manifest is needed: the engine version guard must run before loading dependencies.
            $inputStream = $entry.Open()
            $outputStream = [IO.File]::Create($manifest)
            try
            {
                $buffer = [byte[]]::new(8192)
                $total = 0
                while (($read = $inputStream.Read($buffer, 0, $buffer.Length)) -gt 0)
                {
                    $total += $read
                    if ($total -gt 1MB) { throw "Packaged manifest exceeded the extraction limit: $package" }
                    $outputStream.Write($buffer, 0, $read)
                }
            }
            finally { $inputStream.Dispose(); $outputStream.Dispose() }
            $data = Import-PowerShellDataFile -LiteralPath $manifest
            if ([string]$data.ModuleVersion -ne $version) { throw "Packaged ModuleVersion differs from source for $name." }
            if (-not ($data.PowerShellVersion -as [version])) { throw "Packaged PowerShellVersion is missing or invalid for $name." }
            [pscustomobject]@{
                Name = $name
                Version = $version
                MinimumPowerShellVersion = [string]$data.PowerShellVersion
                ManifestPath = $manifest
                PackagePath = $package
            }
        }
        finally { $archive.Dispose() }
    }
}

function ConvertTo-PSRuntimeEncodedCommand
{
    param([Parameter(Mandatory)][string]$Command)
    [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($Command))
}

function Invoke-PSRuntimeValidation
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourcePsRoot,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$PwshPath,
        [Parameter(Mandatory)][string]$DotnetRootPath,
        [Parameter(Mandatory)][string]$InstallScriptPath,
        [string]$ReportRoot,
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 180
    )

    $ErrorActionPreference = 'Stop'
    $scratch = Join-Path (Get-PSRuntimeScratchRoot) ".ps-runtime-dependencies-$([guid]::NewGuid().ToString('N'))"
    $report = [ordered]@{
        schemaVersion = 1
        status = 'Running'
        startedAt = [datetime]::UtcNow.ToString('o')
        completedAt = $null
        cases = [Collections.Generic.List[object]]::new()
    }
    $state = @{ Runtime = $null; Packages = @(); OlderPwsh = $null; OlderEnvironment = $null }
    $failure = $null
    $reportReady = $false

    function Save-RuntimeReport
    {
        if ($reportReady)
        {
            $path = Join-Path $ReportRoot 'runtime-dependencies.json'
            Assert-AzArtifactNoLinks -Path $path
            Write-PSValidationJson -Path $path -Value $report
            Set-AzArtifactReadable -Path $path
        }
    }

    function Invoke-RuntimeCase
    {
        param([string]$Name, [scriptblock]$Action)
        $case = [ordered]@{
            name = $Name; status = 'Running'; message = $null
            observations = [Collections.Generic.List[object]]::new()
        }
        $report.cases.Add($case)
        try { & $Action $case; $case.status = 'Succeeded' }
        catch { $case.status = 'Failed'; $case.message = $_.Exception.Message }
        Save-RuntimeReport
    }

    function Invoke-RuntimeCommand
    {
        param($Case, [string]$FilePath, [string[]]$Arguments, [hashtable]$Environment = @{})
        $result = Invoke-PSRuntimeProcess -FilePath $FilePath -Arguments $Arguments -Environment $Environment `
            -WorkingDirectory $scratch -TimeoutSeconds $TimeoutSeconds
        $observation = [ordered]@{
            executable = $FilePath; arguments = $Arguments
            exitCode = $result.ExitCode; timedOut = $result.TimedOut; truncated = $result.Truncated
            durationSeconds = $result.DurationSeconds
            stdout = $null; stderr = $null
        }
        if ($reportReady)
        {
            $prefix = "$($Case.name)-$($Case.observations.Count)"
            $observation.stdout = "$prefix.stdout.log"
            $observation.stderr = "$prefix.stderr.log"
            foreach ($stream in 'stdout', 'stderr')
            {
                $path = Join-Path $ReportRoot $observation[$stream]
                Assert-AzArtifactNoLinks -Path $path
                $text = if ($stream -eq 'stdout') { $result.StdOut } else { $result.StdErr }
                [IO.File]::WriteAllText($path, $text)
                Set-AzArtifactReadable -Path $path
            }
        }
        $Case.observations.Add($observation)
        return $result
    }

    try
    {
        $SourcePsRoot = [IO.Path]::GetFullPath($SourcePsRoot)
        $RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
        $DotnetRootPath = [IO.Path]::GetFullPath($DotnetRootPath)
        if ($ReportRoot)
        {
            $ReportRoot = [IO.Path]::GetFullPath($ReportRoot)
            foreach ($inputRoot in $SourcePsRoot, $RepositoryRoot, $DotnetRootPath)
            {
                $relative = [IO.Path]::GetRelativePath($inputRoot, $ReportRoot)
                if ($relative -eq '.' -or (-not [IO.Path]::IsPathRooted($relative) -and
                    $relative -ne '..' -and -not $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)")))
                {
                    throw 'Runtime reports must be outside source, gallery, and primary toolchain directories.'
                }
            }
            New-AzArtifactDirectory -Path $ReportRoot
            $reportReady = $true
        }
        $null = New-Item -ItemType Directory -Path $scratch
        $primaryEnvironment = Get-PSRuntimeEnvironment -DotnetRoot $DotnetRootPath -ScratchRoot $scratch
        $discovery = @'
$ErrorActionPreference = 'Stop'
[ordered]@{
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    DotnetVersion = [Environment]::Version.ToString()
    Framework = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
    Architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    RuntimeDirectory = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
    PSHome = $PSHOME
} | ConvertTo-Json -Compress
'@
        $discoveryArguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', (ConvertTo-PSRuntimeEncodedCommand $discovery))

        Invoke-RuntimeCase 'supported-runtime' {
            param($case)
            $result = Invoke-RuntimeCommand $case $PwshPath $discoveryArguments $primaryEnvironment
            Assert-PSRuntimeResult $result Success
            $runtime = $result.StdOut | ConvertFrom-Json
            $case.runtime = $runtime
            if (-not ($runtime.PowerShellVersion -as [version]) -or [version]$runtime.PowerShellVersion -lt [version]'7.6')
            {
                throw "Target PowerShell must be 7.6 or later; actual: $($runtime.PowerShellVersion)."
            }
            if (-not ($runtime.DotnetVersion -as [version]) -or [version]$runtime.DotnetVersion -lt [version]'10.0' -or
                $runtime.Framework -notmatch '^\.NET ' -or $runtime.Architecture -notin 'X64', 'X86', 'Arm64', 'Arm')
            {
                throw "Target .NET/architecture is not a supported PowerShell 7.6 runtime: $($result.StdOut)"
            }
            if (-not [IO.Path]::IsPathRooted($runtime.PSHome)) { throw 'Target PSHOME is not an absolute path.' }
            $state.Runtime = $runtime
            $state.Packages = @(Get-PSRuntimePackageManifests -SourcePsRoot $SourcePsRoot -RepositoryRoot $RepositoryRoot `
                -Destination (Join-Path $scratch 'manifests'))
            $case.packages = $state.Packages
            foreach ($package in $state.Packages)
            {
                $minimum = [version]$package.MinimumPowerShellVersion
                if ($minimum -lt [version]'7.6' -or [version]$runtime.PowerShellVersion -lt $minimum)
                {
                    throw "$($package.Name) packaged PowerShellVersion '$minimum' must require at least 7.6 and be supported by target $($runtime.PowerShellVersion)."
                }
            }
        }

        Invoke-RuntimeCase 'missing-framework' {
            param($case)
            if (-not $state.Runtime) { throw 'Cannot probe missing framework: target runtime discovery failed.' }
            $dll = Join-Path $state.Runtime.PSHome 'pwsh.dll'
            Assert-PSRuntimeRegularPath $dll
            $privateRoot = Join-Path $scratch 'frameworkless'
            $hostPath = New-PSRuntimeFrameworklessHost -DotnetRoot $DotnetRootPath -Destination $privateRoot
            $environment = Get-PSRuntimeEnvironment -DotnetRoot $privateRoot -ScratchRoot $scratch
            $environment.DOTNET_ROLL_FORWARD = 'Disable'
            $result = Invoke-RuntimeCommand $case $hostPath (@($dll) + $discoveryArguments) $environment
            Assert-PSRuntimeResult $result MissingFramework
        }

        Invoke-RuntimeCase 'provision-older-runtime' {
            param($case)
            if (-not $state.Runtime) { throw 'Cannot provision matching older runtime: target runtime discovery failed.' }
            $oldRoot = Join-Path $scratch 'dotnet9'
            $oldTool = Join-Path $scratch 'powershell75'
            $installerName = if ($IsWindows) { 'dotnet-install.ps1' } else { 'dotnet-install.sh' }
            $installer = Join-Path (Split-Path $DotnetRootPath -Parent) $installerName
            if (Test-Path -LiteralPath $installer)
            {
                Assert-PSRuntimeRegularPath $installer
            }
            else
            {
                $installer = Join-Path $scratch $installerName
                $uri = "https://dot.net/v1/$installerName"
                $download = "`$ErrorActionPreference='Stop'; Invoke-WebRequest -Uri '$uri' -OutFile '$($installer.Replace("'", "''"))' -TimeoutSec $TimeoutSeconds"
                $result = Invoke-RuntimeCommand $case $PwshPath @('-NoLogo', '-NoProfile', '-NonInteractive', '-EncodedCommand', (ConvertTo-PSRuntimeEncodedCommand $download)) $primaryEnvironment
                Assert-PSRuntimeResult $result Success
                Assert-PSRuntimeRegularPath $installer
            }
            if ($IsWindows)
            {
                # The Windows PowerShell tool needs both shared frameworks in its isolated runtime root.
                foreach ($runtimeKind in 'dotnet', 'windowsdesktop')
                {
                    $result = Invoke-RuntimeCommand $case $PwshPath @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $installer,
                        '-Runtime', $runtimeKind, '-Channel', '9.0', '-InstallDir', $oldRoot, '-Architecture', $state.Runtime.Architecture.ToLowerInvariant(), '-NoPath') $primaryEnvironment
                    Assert-PSRuntimeResult $result Success
                }
            }
            else
            {
                $bash = (Get-Command bash -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
                $result = Invoke-RuntimeCommand $case $bash @($installer, '--runtime', 'dotnet', '--channel', '9.0',
                    '--install-dir', $oldRoot, '--architecture', $state.Runtime.Architecture.ToLowerInvariant(), '--no-path') $primaryEnvironment
                Assert-PSRuntimeResult $result Success
            }
            $dotnet = Join-Path $DotnetRootPath $(if ($IsWindows) { 'dotnet.exe' } else { 'dotnet' })
            # Use a private NuGet configuration and caches, not caller feeds or user tool installations.
            $nugetConfig = Join-Path $scratch 'NuGet.Config'
            [IO.File]::WriteAllText($nugetConfig, '<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>')
            $result = Invoke-RuntimeCommand $case $dotnet @('tool', 'install', '--tool-path', $oldTool, 'PowerShell',
                '--version', '7.5.*', '--configfile', $nugetConfig, '--no-cache') $primaryEnvironment
            Assert-PSRuntimeResult $result Success
            $state.OlderPwsh = Join-Path $oldTool $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
            $state.OlderEnvironment = Get-PSRuntimeEnvironment -DotnetRoot $oldRoot -ScratchRoot $scratch
            $result = Invoke-RuntimeCommand $case $state.OlderPwsh $discoveryArguments $state.OlderEnvironment
            Assert-PSRuntimeResult $result Success
            $older = $result.StdOut | ConvertFrom-Json
            $case.runtime = $older
            if ($older.PowerShellVersion -notmatch '^7\.5\.\d+$' -or $older.DotnetVersion -notmatch '^9\.' -or
                $older.Architecture -ne $state.Runtime.Architecture)
            {
                throw "Provisioned tool must actually run PowerShell 7.5 on .NET 9 with target architecture: $($result.StdOut)"
            }
            $runtimeRelative = [IO.Path]::GetRelativePath($oldRoot, $older.RuntimeDirectory)
            $toolRelative = [IO.Path]::GetRelativePath($oldTool, $older.PSHome)
            foreach ($relative in $runtimeRelative, $toolRelative)
            {
                if ([IO.Path]::IsPathRooted($relative) -or $relative -eq '..' -or
                    $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)"))
                {
                    throw 'Provisioned PowerShell 7.5 and .NET 9 must run from the private toolchain, not an ambient installation.'
                }
            }
            $state.OlderReady = $true
        }

        Invoke-RuntimeCase 'older-installer-rejection' {
            param($case)
            if (-not $state.OlderReady) { throw 'Cannot assert installer rejection: older runtime provisioning failed.' }
            # Defense in depth: a regressed helper must never reach real repository or installation commands.
            $escapedInstaller = $InstallScriptPath.Replace("'", "''")
            $command = @"
`$ErrorActionPreference = 'Stop'
function global:Get-PSRepository { throw 'RUNTIME_PROBE_SIDE_EFFECT_BLOCKED' }
function global:Register-PSRepository { throw 'RUNTIME_PROBE_SIDE_EFFECT_BLOCKED' }
function global:Unregister-PSRepository { throw 'RUNTIME_PROBE_SIDE_EFFECT_BLOCKED' }
function global:Install-Module { throw 'RUNTIME_PROBE_SIDE_EFFECT_BLOCKED' }
try { & '$escapedInstaller' -RequiredVersion '0.0.0'; exit 0 }
catch { [Console]::Error.WriteLine(`$_.Exception.Message); exit 1 }
"@
            $result = Invoke-RuntimeCommand $case $state.OlderPwsh @('-NoLogo', '-NoProfile', '-NonInteractive',
                '-EncodedCommand', (ConvertTo-PSRuntimeEncodedCommand $command)) $state.OlderEnvironment
            Assert-PSRuntimeResult $result InstallerVersion
        }
        foreach ($package in $state.Packages)
        {
            Invoke-RuntimeCase "older-manifest-$($package.Name)" {
                param($case)
                if (-not $state.OlderReady) { throw 'Cannot assert manifest rejection: older runtime provisioning failed.' }
                $escapedManifest = $package.ManifestPath.Replace("'", "''")
                $command = @"
`$ErrorActionPreference = 'Stop'
`$env:PSModulePath = Join-Path `$PSHOME 'Modules'
try { Import-Module -Name '$escapedManifest' -Force -ErrorAction Stop; exit 0 }
catch { [Console]::Error.WriteLine("`$(`$_.FullyQualifiedErrorId): `$(`$_.Exception.Message)"); exit 1 }
"@
                $result = Invoke-RuntimeCommand $case $state.OlderPwsh @('-NoLogo', '-NoProfile', '-NonInteractive',
                    '-EncodedCommand', (ConvertTo-PSRuntimeEncodedCommand $command)) $state.OlderEnvironment
                Assert-PSRuntimeResult $result ManifestVersion -ModuleName $package.Name -MinimumVersion $package.MinimumPowerShellVersion
            }
        }
    }
    catch { $failure = $_ }
    finally
    {
        try
        {
            if (Test-Path -LiteralPath $scratch)
            {
                Assert-PSRuntimeRegularPath $scratch
                Remove-Item -LiteralPath $scratch -Recurse -Force -ErrorAction Stop
            }
        }
        catch
        {
            if (-not $failure -and @($report.cases | Where-Object status -eq 'Failed').Count -eq 0) { $failure = $_ }
            else { Write-Warning "Runtime scratch cleanup also failed: $_" }
        }
        $report.status = if ($failure -or @($report.cases | Where-Object status -eq 'Failed').Count -gt 0) { 'Failed' } else { 'Succeeded' }
        if ($failure) { $report.message = $failure.Exception.Message }
        $report.completedAt = [datetime]::UtcNow.ToString('o')
        try { Save-RuntimeReport }
        catch
        {
            if (-not $failure -and @($report.cases | Where-Object status -eq 'Failed').Count -eq 0) { $failure = $_ }
            else { Write-Warning "Runtime report could not be saved: $_" }
        }
    }
    if ($failure) { throw $failure }
    if ($report.status -ne 'Succeeded')
    {
        $failed = @($report.cases | Where-Object status -eq 'Failed' | ForEach-Object { "$($_.name): $($_.message)" })
        throw "PowerShell runtime dependency validation failed:`n$($failed -join "`n")"
    }
    return [pscustomobject]$report
}
