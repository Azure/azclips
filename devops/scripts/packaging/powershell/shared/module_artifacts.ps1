<#
.SYNOPSIS
Captures allowlisted diagnostics and exact attempted module versions for one matrix run.
#>
function Test-AzArtifactPathWithin
{
    param([string]$Path, [string]$Root)
    $comparison = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    $separator = [IO.Path]::DirectorySeparatorChar
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd([char[]]@('\', '/'))
    $fullPath = [IO.Path]::GetFullPath($Path)
    $fullPath.Equals($rootPath, $comparison) -or $fullPath.StartsWith($rootPath + $separator, $comparison)
}

function Assert-AzArtifactNoLinks
{
    param([string]$Path)
    $current = [IO.Path]::GetFullPath($Path)
    while ($current)
    {
        if (Test-Path -LiteralPath $current)
        {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)
            {
                throw "Artifact capture refuses a symbolic link or reparse point: $current"
            }
        }
        $parent = [IO.Path]::GetDirectoryName($current)
        if ($parent -eq $current) { break }
        $current = $parent
    }
}

function Set-AzArtifactReadable
{
    param([string]$Path, [switch]$Directory)
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Unix)
    {
        # A root container must leave artifacts readable by the nonroot host publisher.
        $mode = if ($Directory) { 493 } else { 420 } # 0755 / 0644
        [IO.File]::SetUnixFileMode($Path, [IO.UnixFileMode]$mode)
    }
}

function New-AzArtifactDirectory
{
    param([string]$Path)
    Assert-AzArtifactNoLinks -Path $Path
    [IO.Directory]::CreateDirectory($Path) | Out-Null
    Set-AzArtifactReadable -Path $Path -Directory
}

function Write-AzArtifactJson
{
    param([string]$Path, [object]$Value)
    Assert-AzArtifactNoLinks -Path $Path
    New-AzArtifactDirectory -Path ([IO.Path]::GetDirectoryName($Path))
    $json = ConvertTo-Json -InputObject $Value -Depth 12
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
    Set-AzArtifactReadable -Path $Path
}

function Add-AzModuleCaptureError
{
    param($Context, [string]$Stage, [string]$Message, [string]$PackageName, [string]$Version)
    $Context.Errors.Add([pscustomobject][ordered]@{
        capturedAtUtc = [DateTime]::UtcNow.ToString('o')
        stage = $Stage
        packageName = $PackageName
        version = $Version
        message = $Message
    })
    Write-Warning "Module artifact capture [$Stage]: $Message" -WarningAction Continue
    if ($Context.Ready)
    {
        try
        {
            Write-AzArtifactJson -Path (Join-Path $Context.ReportRoot 'capture-errors.json') -Value ([ordered]@{
                schemaVersion = 1
                errors = @($Context.Errors.ToArray())
            })
        }
        catch
        {
            Write-Warning "Could not write capture-errors.json: $($_.Exception.Message)" -WarningAction Continue
        }
    }
}

function New-AzModuleArtifactContext
{
    param([string]$ReportRoot, [string[]]$ExcludedRoots)
    if ([string]::IsNullOrWhiteSpace($ReportRoot)) { return $null }
    $context = [pscustomobject]@{
        ReportRoot = $null
        Ready = $false
        Errors = [Collections.Generic.List[object]]::new()
        Snapshots = @{}
        ExcludedRoots = $ExcludedRoots
    }
    try
    {
        if (-not [IO.Path]::IsPathRooted($ReportRoot)) { throw 'ReportRoot must be an absolute filesystem path.' }
        $resolvedRoot = [IO.Path]::GetFullPath($ReportRoot)
        $moduleSearchPaths = @($env:PSModulePath -split [regex]::Escape([string][IO.Path]::PathSeparator) | Where-Object { $_ })
        foreach ($excludedRoot in $ExcludedRoots)
        {
            if ($excludedRoot -and (Test-AzArtifactPathWithin -Path $resolvedRoot -Root $excludedRoot))
            {
                throw "ReportRoot must not be beneath the source or package repository: $excludedRoot"
            }
        }
        foreach ($moduleSearchPath in $moduleSearchPaths)
        {
            if ([IO.Path]::IsPathRooted($moduleSearchPath) -and (Test-AzArtifactPathWithin -Path $resolvedRoot -Root $moduleSearchPath))
            {
                throw "ReportRoot must not be beneath a module-search directory: $moduleSearchPath"
            }
        }
        New-AzArtifactDirectory -Path $resolvedRoot
        $context.ReportRoot = $resolvedRoot
        $context.Ready = $true
        New-AzArtifactDirectory -Path (Join-Path $resolvedRoot 'inventories')
        Write-AzArtifactJson -Path (Join-Path $resolvedRoot 'environment.json') -Value ([ordered]@{
            schemaVersion = 1
            capturedAtUtc = [DateTime]::UtcNow.ToString('o')
            powerShellVersion = $PSVersionTable.PSVersion.ToString()
            powerShellEdition = $PSVersionTable.PSEdition
            dotNetRuntimeDescription = [Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
            osDescription = [Runtime.InteropServices.RuntimeInformation]::OSDescription
            processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
            osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
            psHome = $PSHOME
            moduleSearchPaths = $moduleSearchPaths
        })
    }
    catch { Add-AzModuleCaptureError -Context $context -Stage initialization -Message $_.Exception.Message }
    $context
}

function Copy-AzAttemptedModule
{
    param($Context, $Definition, [array]$Installed, [array]$Available)
    $name = [string]$Definition.PackageName
    $version = [string]$Definition.Version
    if ($name -notmatch '^[A-Za-z0-9_.-]+$' -or $version -notmatch '^\d+\.\d+(\.\d+){0,2}$')
    {
        throw "Unsafe module identity for capture: $name $version"
    }
    $key = "$name|$version"
    if ($Context.Snapshots.ContainsKey($key)) { return $Context.Snapshots[$key] }

    $paths = @(
        foreach ($module in $Installed)
        {
            if ($module.Name -ne $name -or [version]$module.Version -ne [version]$version) { throw "Installed metadata does not match $name $version." }
            if ($module.InstalledLocation) { [IO.Path]::GetFullPath([string]$module.InstalledLocation) }
        }
        foreach ($module in $Available)
        {
            if ($module.Name -ne $name -or [version]$module.Version -ne [version]$version) { throw "Available metadata does not match $name $version." }
            if ($module.ModuleBase) { [IO.Path]::GetFullPath([string]$module.ModuleBase) }
            elseif ($module.Path) { [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath([string]$module.Path)) }
        }
    ) | Sort-Object -Unique
    if (@($paths).Count -eq 0)
    {
        if ($Installed.Count -gt 0 -or $Available.Count -gt 0)
        {
            throw "Module metadata has no usable directory for $name $version."
        }
        return [ordered]@{ status = 'missing'; sourcePath = $null; destinationPath = $null; error = 'The attempted version has no discoverable module directory.' }
    }
    if (@($paths).Count -ne 1) { throw "Ambiguous installed directories for $name ${version}: $($paths -join ', ')" }
    $sourcePath = [string]@($paths)[0]
    Assert-AzArtifactNoLinks -Path $sourcePath
    $source = Get-Item -LiteralPath $sourcePath -Force -ErrorAction Stop
    if (-not $source.PSIsContainer -or $source.Name -ne $version -or $source.Parent.Name -ne $name)
    {
        throw "Module capture requires the exact package/version directory for $name ${version}: $sourcePath"
    }
    foreach ($excludedRoot in $Context.ExcludedRoots)
    {
        if ($excludedRoot -and (Test-AzArtifactPathWithin -Path $sourcePath -Root $excludedRoot))
        {
            throw "Installed module capture cannot copy from a source or package repository: $sourcePath"
        }
    }
    if (Test-AzArtifactPathWithin -Path $Context.ReportRoot -Root $sourcePath)
    {
        throw "ReportRoot must not be beneath the installed module being captured: $sourcePath"
    }
    $manifestPath = Join-Path $sourcePath "$name.psd1"
    Assert-AzArtifactNoLinks -Path $manifestPath
    $manifest = Import-PowerShellDataFile -LiteralPath $manifestPath -ErrorAction Stop
    if ([version]$manifest.ModuleVersion -ne [version]$version)
    {
        throw "Installed manifest version does not match $name ${version}: $manifestPath"
    }

    # Enumerate one directory at a time: never recurse through a link, even before copying.
    $pending = [Collections.Generic.Queue[string]]::new()
    $entries = [Collections.Generic.List[object]]::new()
    $pending.Enqueue($sourcePath)
    while ($pending.Count -gt 0)
    {
        foreach ($entry in Get-ChildItem -LiteralPath $pending.Dequeue() -Force -ErrorAction Stop)
        {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)
            {
                throw "Module capture refuses a symbolic link or reparse point: $($entry.FullName)"
            }
            if ($entry.Name -ieq 'NuGet.config') { throw "Module capture refuses NuGet.config: $($entry.FullName)" }
            $entries.Add($entry)
            if ($entry.PSIsContainer) { $pending.Enqueue($entry.FullName) }
        }
    }

    $snapshotRoot = Join-Path $Context.ReportRoot 'installed-modules'
    $packageRoot = Join-Path $snapshotRoot $name
    $destination = Join-Path $packageRoot $version
    if (Test-Path -LiteralPath $destination) { throw "Snapshot destination already exists and will not be overwritten: $destination" }
    New-AzArtifactDirectory -Path $snapshotRoot
    New-AzArtifactDirectory -Path $packageRoot
    New-AzArtifactDirectory -Path $destination
    foreach ($entry in $entries)
    {
        Assert-AzArtifactNoLinks -Path $entry.FullName
        $relative = $entry.FullName.Substring($sourcePath.TrimEnd([char[]]@('\', '/')).Length + 1)
        $target = Join-Path $destination $relative
        if ($entry.PSIsContainer) { New-AzArtifactDirectory -Path $target }
        else
        {
            Assert-AzArtifactNoLinks -Path $target
            [IO.File]::Copy($entry.FullName, $target, $false)
            Set-AzArtifactReadable -Path $target
        }
    }
    $result = [ordered]@{ status = 'copied'; sourcePath = $sourcePath; destinationPath = $destination; error = $null }
    $Context.Snapshots[$key] = $result
    $result
}

function Write-AzModuleInventory
{
    param(
        $Context,
        [string]$Stage,
        [AllowEmptyCollection()][array]$Definitions,
        [AllowEmptyCollection()][array]$SnapshotDefinitions
    )
    if (-not $Context -or -not $Context.Ready) { return }
    try
    {
        $modules = @(
            foreach ($definition in $Definitions)
            {
                $installed = @()
                $available = @()
                $installedError = $null
                $availableError = $null
                try { $installed = @(Get-InstalledModule -Name $definition.PackageName -RequiredVersion $definition.Version -ErrorAction Stop) }
                catch
                {
                    if (($_.FullyQualifiedErrorId -split ',')[0] -ne 'NoMatchFound')
                    {
                        $installedError = $_.Exception.Message
                        Add-AzModuleCaptureError -Context $Context -Stage $Stage -PackageName $definition.PackageName -Version $definition.Version -Message "Installed lookup failed: $installedError"
                    }
                }
                try
                {
                    $available = @(Get-Module -ListAvailable -Name $definition.PackageName -ErrorAction Stop |
                        Where-Object { $_.Version -eq [version]$definition.Version })
                }
                catch
                {
                    $availableError = $_.Exception.Message
                    Add-AzModuleCaptureError -Context $Context -Stage $Stage -PackageName $definition.PackageName -Version $definition.Version -Message "Available lookup failed: $availableError"
                }
                $snapshot = [ordered]@{ status = 'not-requested'; sourcePath = $null; destinationPath = $null; error = $null }
                if ($PSBoundParameters.ContainsKey('SnapshotDefinitions'))
                {
                    $snapshot.status = 'not-attempted'
                    if (@($SnapshotDefinitions | Where-Object { $_.PackageName -eq $definition.PackageName -and $_.Version -eq $definition.Version }).Count -gt 0)
                    {
                        try { $snapshot = Copy-AzAttemptedModule -Context $Context -Definition $definition -Installed $installed -Available $available }
                        catch
                        {
                            $snapshot = [ordered]@{ status = 'error'; sourcePath = $null; destinationPath = $null; error = $_.Exception.Message }
                            Add-AzModuleCaptureError -Context $Context -Stage $Stage -PackageName $definition.PackageName -Version $definition.Version -Message $_.Exception.Message
                        }
                    }
                }
                [ordered]@{
                    packageName = $definition.PackageName
                    version = $definition.Version
                    installed = [ordered]@{
                        status = if ($installedError) { 'error' } elseif ($installed.Count) { 'found' } else { 'missing' }
                        locations = @($installed | ForEach-Object { [string]$_.InstalledLocation })
                        error = $installedError
                    }
                    available = [ordered]@{
                        status = if ($availableError) { 'error' } elseif ($available.Count) { 'found' } else { 'missing' }
                        paths = @($available | ForEach-Object { [string]$_.Path })
                        error = $availableError
                    }
                    snapshot = $snapshot
                }
            }
        )
        Write-AzArtifactJson -Path (Join-Path $Context.ReportRoot "inventories\$Stage.json") -Value ([ordered]@{
            schemaVersion = 1
            capturedAtUtc = [DateTime]::UtcNow.ToString('o')
            stage = $Stage
            modules = @($modules)
        })
    }
    catch { Add-AzModuleCaptureError -Context $Context -Stage $Stage -Message $_.Exception.Message }
}
