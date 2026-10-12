. (Join-Path $PSScriptRoot 'dependency_packages.ps1')
. (Join-Path $PSScriptRoot 'dependency_runtime.ps1')

function Get-PSUpgradeGalleryPackages
{
    param([Parameter(Mandatory)][string]$GalleryRoot)

    Assert-AzArtifactNoLinks -Path $GalleryRoot
    $packages = @(foreach ($file in Get-ChildItem -LiteralPath $GalleryRoot -Filter '*.nupkg' -File -ErrorAction Stop)
    {
        Assert-AzArtifactNoLinks -Path $file.FullName
        $metadata = Read-PSDependencyPackage -Path $file.FullName
        if ($metadata.Name -notmatch '^Az(?:\.[A-Za-z0-9_-]+)*$' -or
            $metadata.Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$' -or
            $file.Name -cne "$($metadata.Name).$($metadata.Version).nupkg")
        {
            throw "Invalid upgrade package identity: $($file.Name)"
        }
        [pscustomobject]@{
            Name = $metadata.Name
            Version = $metadata.Version
            Dependencies = @($metadata.Dependencies)
            Commands = @()
            PackagePath = $file.FullName
        }
    })
    if ($packages.Count -eq 0) { throw "No PowerShell packages found in $GalleryRoot." }
    if (@($packages | Group-Object Name | Where-Object Count -ne 1).Count -gt 0)
    {
        throw 'The released gallery must contain one version of each module.'
    }
    $byName = @{}
    foreach ($package in $packages) { $byName[$package.Name] = $package }
    foreach ($package in $packages)
    {
        foreach ($dependency in $package.Dependencies)
        {
            # Cleanup needs the bundle's resolved version, not its declared NuGet minimum.
            if ($byName.ContainsKey($dependency.Name)) { $dependency.Version = $byName[$dependency.Name].Version }
        }
    }
    return $packages
}

function Get-PSUpgradeCleanupPackages
{
    param([Parameter(Mandatory)][object[]]$Packages)

    $byIdentity = @{}
    # A same-version replacement may change dependencies, such as Login to Accounts.
    foreach ($package in $Packages) { $byIdentity["$($package.Name)|$($package.Version)"] = $package }
    $contracts = @(foreach ($key in $byIdentity.Keys | Sort-Object)
    {
        $package = $byIdentity[$key]
        $dependencies = @(foreach ($dependency in $package.Dependencies)
        {
            $dependencyKey = "$($dependency.Name)|$($dependency.Version)"
            if ($byIdentity.ContainsKey($dependencyKey)) { @{ Name = $dependencyKey } }
        })
        [pscustomobject]@{ Name = $key; Dependencies = $dependencies; Package = $package }
    })
    $root = [pscustomobject]@{
        Name = '__upgrade_cleanup__'
        Dependencies = @($contracts | ForEach-Object { @{ Name = $_.Name } })
        Package = $null
    }
    $ordered = @(Get-PSDependencyClosure -Name $root.Name -Contracts (@($contracts) + @($root)) |
        Where-Object Name -ne $root.Name | ForEach-Object Package)
    [array]::Reverse($ordered)
    return $ordered
}

function Test-PSUpgradeTransportEntry
{
    param([Parameter(Mandatory)][string]$Name)

    return $Name -in '[Content_Types].xml', '.signature.p7s', '_rels/.rels' -or
        $Name -match '^[^/]+\.nuspec$' -or
        $Name -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$'
}

function Get-PSUpgradePayload
{
    param([Parameter(Mandatory)][string]$PackagePath)

    Assert-AzArtifactNoLinks -Path $PackagePath
    $payload = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $archive = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    $size = 0L
    try
    {
        foreach ($entry in $archive.Entries)
        {
            $name = $entry.FullName.Replace('\', '/')
            if ($name.StartsWith('/') -or $name.Contains(':') -or
                @($name.TrimEnd('/').Split('/') | Where-Object { $_ -in '', '.', '..' }).Count -gt 0 -or
                (($entry.ExternalAttributes -shr 16) -band 0xF000) -eq 0xA000)
            {
                throw "Unsafe package entry in ${PackagePath}: $name"
            }
            if (-not $names.Add($name)) { throw "Duplicate package entry: $name" }
            if ($names.Count -gt 20000) { throw 'Upgrade package exceeds the entry limit.' }
            if ($name.EndsWith('/')) { continue }
            $size += $entry.Length
            if ($size -gt 1GB -or $names.Count -gt 20000) { throw 'Upgrade package exceeds the payload limit.' }
            if (Test-PSUpgradeTransportEntry -Name $name) { continue }
            $stream = $entry.Open()
            try { $payload.Add($name, [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))) }
            finally { $stream.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    if ($payload.Count -eq 0) { throw "Package has no module payload: $PackagePath" }
    return ,$payload
}

function Assert-PSUpgradeInstalledPayload
{
    param([Parameter(Mandatory)]$Package, [Parameter(Mandatory)][string]$InstalledLocation)

    Assert-AzArtifactNoLinks -Path $InstalledLocation
    $directory = Get-Item -LiteralPath $InstalledLocation -ErrorAction Stop
    if (-not $directory.PSIsContainer -or $directory.Name -ne $Package.Version -or
        $directory.Parent.Name -ne $Package.Name)
    {
        throw "Unexpected installed location for $($Package.Name) $($Package.Version): $InstalledLocation"
    }
    $payload = Get-PSUpgradePayload -PackagePath $Package.PackagePath
    foreach ($entry in $payload.GetEnumerator())
    {
        $path = Join-Path $InstalledLocation $entry.Key
        Assert-AzArtifactNoLinks -Path $path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $entry.Value)
        {
            throw "Installed payload differs from the pipeline/package input: $($Package.Name) $($entry.Key)"
        }
    }
    foreach ($item in Get-ChildItem -LiteralPath $InstalledLocation -Recurse -Force -ErrorAction Stop)
    {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Installed module contains a link: $($item.FullName)" }
        if ($item.PSIsContainer) { continue }
        $relative = [IO.Path]::GetRelativePath($InstalledLocation, $item.FullName).Replace('\', '/')
        if ($relative -ne 'PSGetModuleInfo.xml' -and
            -not (Test-PSUpgradeTransportEntry -Name $relative) -and -not $payload.ContainsKey($relative))
        {
            throw "Installed module contains a stale or unexpected file: $($Package.Name) $relative"
        }
    }
    return @{
        name = $Package.Name
        version = $Package.Version
        modulePath = Join-Path $InstalledLocation "$($Package.Name).psd1"
        installedLocation = $InstalledLocation
        packageSha256 = (Get-FileHash -LiteralPath $Package.PackagePath -Algorithm SHA256).Hash
        verifiedFiles = $payload.Count
        commands = @($Package.Commands)
    }
}

function Get-PSUpgradeInstalledModule
{
    param([Parameter(Mandatory)]$Package)
    try { Get-InstalledModule -Name $Package.Name -RequiredVersion $Package.Version -ErrorAction Stop }
    catch { if (($_.FullyQualifiedErrorId -split ',')[0] -ne 'NoMatchFound') { throw } }
}

function Save-PSUpgradeReport
{
    param([Parameter(Mandatory)]$Context)
    if ($Context.ReportRoot)
    {
        Write-AzArtifactJson -Path (Join-Path $Context.ReportRoot 'upgrade-tests.json') -Value $Context.Report
    }
}

function Invoke-PSUpgradeOperation
{
    param([Parameter(Mandatory)]$Context, [Parameter(Mandatory)][hashtable]$Request, [string]$Name)

    $case = [ordered]@{
        name = $Name
        operation = $Request.operation
        status = 'Running'
        startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        finishedAtUtc = $null
        message = $null
        exitCode = $null
        output = $null
        data = $null
    }
    $Context.Report.cases.Add($case)
    $id = [guid]::NewGuid().ToString('N')
    $requestPath = Join-Path $Context.Scratch "$id-request.json"
    $resultPath = Join-Path $Context.Scratch "$id-result.json"
    Write-PSValidationJson -Path $requestPath -Value $Request
    Save-PSUpgradeReport $Context
    $failed = $false
    try
    {
        $process = Invoke-PSRuntimeProcess -FilePath $Context.PwshPath -Arguments @(
            '-NoProfile', '-NonInteractive', '-File', (Join-Path $PSScriptRoot 'upgrade_worker.ps1'),
            '-RequestPath', $requestPath, '-ResultPath', $resultPath
        ) -WorkingDirectory $Context.Scratch -TimeoutSeconds $Context.TimeoutSeconds -Environment @{
            AZCLIPS_TELEMETRY_TARGET = 'local'
            POWERSHELL_TELEMETRY_OPTOUT = '1'
            DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        }
        $case.exitCode = $process.ExitCode
        $case.output = $process.Output
        if ($process.TimedOut) { throw "Upgrade operation '$Name' timed out." }
        if ($process.Truncated) { throw "Upgrade operation '$Name' exceeded the diagnostic output limit." }
        if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf))
        {
            throw "Upgrade operation '$Name' produced no result (exit $($process.ExitCode)): $($process.Output)"
        }
        $result = Get-Content -LiteralPath $resultPath -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        $case.data = $result.data
        if ($result.status -ne 'Succeeded' -or $process.ExitCode -ne 0)
        {
            throw "Upgrade operation '$Name' failed: $($result.message)"
        }
        $case.status = 'Succeeded'
        Write-Host "PASS: upgrade $Name"
        return $result.data
    }
    catch
    {
        $failed = $true
        $case.status = 'Failed'
        $case.message = $_.Exception.Message
        Write-Host "FAIL: upgrade $Name`: $($case.message)"
        throw
    }
    finally
    {
        $case.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        try { Save-PSUpgradeReport $Context }
        catch
        {
            if (-not $failed) { throw }
            Write-Warning "Could not retain upgrade diagnostics: $($_.Exception.Message)" -WarningAction Continue
        }
    }
}

function Invoke-PSUpgradeValidation
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$BaselineRoot,
        [Parameter(Mandatory)][string]$RepositoryRoot,
        [Parameter(Mandatory)][string]$SourcePsRoot,
        [Parameter(Mandatory)][string]$PwshPath,
        [string]$ReportRoot,
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180
    )

    $context = [pscustomobject]@{
        Scratch = $null
        PwshPath = $PwshPath
        TimeoutSeconds = $TimeoutSeconds
        ReportRoot = $null
        Report = [ordered]@{
            schemaVersion = 1
            status = 'Running'
            startedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
            finishedAtUtc = $null
            baseline = $null
            targetVersion = $null
            message = $null
            cases = [Collections.Generic.List[object]]::new()
            cleanupErrors = [Collections.Generic.List[string]]::new()
        }
    }
    $attempted = [Collections.Generic.List[object]]::new()
    $roots = @()
    $failure = $null
    $repositoryName = "AzclipsUpgrade-$([guid]::NewGuid().ToString('N'))"
    $gallery = $null
    try
    {
        $workingDirectory = (Get-Location).ProviderPath
        $BaselineRoot = [IO.Path]::GetFullPath($BaselineRoot, $workingDirectory)
        $RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot, $workingDirectory)
        $SourcePsRoot = [IO.Path]::GetFullPath($SourcePsRoot, $workingDirectory)
        if ($PwshPath.Contains('/') -or $PwshPath.Contains('\'))
        {
            $context.PwshPath = [IO.Path]::GetFullPath($PwshPath, $workingDirectory)
        }
        if ($ReportRoot)
        {
            $resolvedReportRoot = [IO.Path]::GetFullPath($ReportRoot, $workingDirectory)
            $excludedRoots = @($BaselineRoot, $RepositoryRoot, $SourcePsRoot) +
                @($env:PSModulePath -split [regex]::Escape([string][IO.Path]::PathSeparator) | Where-Object { $_ })
            foreach ($inputRoot in $excludedRoots)
            {
                if (Test-AzArtifactPathWithin -Path $resolvedReportRoot -Root $inputRoot)
                {
                    throw 'Upgrade reports must be outside release, source, pipeline package and module directories.'
                }
            }
            New-AzArtifactDirectory -Path $resolvedReportRoot
            $context.ReportRoot = $resolvedReportRoot
            Save-PSUpgradeReport $context
        }
        $release = Get-Content -LiteralPath (Join-Path $BaselineRoot 'release.json') -Raw -ErrorAction Stop | ConvertFrom-Json -AsHashtable
        Assert-AzArtifactNoLinks -Path $RepositoryRoot
        foreach ($packageFile in Get-ChildItem -LiteralPath $RepositoryRoot -File -Filter '*.nupkg' -ErrorAction Stop)
        {
            Assert-AzArtifactNoLinks -Path $packageFile.FullName
        }
        $targets = @(Get-PSDependencyContracts -SourcePsRoot $SourcePsRoot -RepositoryRoot $RepositoryRoot)
        $targetAz = @($targets | Where-Object Name -eq 'Az')
        if ($targetAz.Count -ne 1) { throw 'Upgrade validation requires exactly one target Az rollup.' }
        $context.Report.targetVersion = $targetAz[0].Version
        $context.Report.baseline = $release
        if ($release.schemaVersion -ne 1 -or -not ($release.version -as [version]) -or
            [version]$release.targetVersion -ne [version]$targetAz[0].Version -or
            [version]$release.version -ge [version]$targetAz[0].Version)
        {
            throw 'The prepared baseline must target this pipeline version and have a strictly older Az version.'
        }
        $baselineGallery = Join-Path $BaselineRoot 'gallery'
        $baselinePackages = @(Get-PSUpgradeGalleryPackages -GalleryRoot $baselineGallery)
        $baselineAz = @($baselinePackages | Where-Object Name -eq 'Az')
        if ($baselineAz.Count -ne 1 -or [version]$baselineAz[0].Version -ne [version]$release.version)
        {
            throw 'The released Az package does not match the prepared baseline metadata.'
        }
        $baseline = @(Get-PSDependencyClosure -Name Az -Contracts $baselinePackages)
        foreach ($target in $targets)
        {
            if ($target.Name -notmatch '^Az(?:\.[A-Za-z0-9_-]+)*$' -or
                $target.Version -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$')
            {
                throw "Invalid target module identity: $($target.Name) $($target.Version)"
            }
            $previous = @($baselinePackages | Where-Object Name -eq $target.Name)
            if ($previous.Count -eq 1 -and [version]$target.Version -lt [version]$previous[0].Version)
            {
                throw "Target $($target.Name) $($target.Version) would downgrade $($previous[0].Version)."
            }
        }
        $scratchPath = Join-Path (Get-PSRuntimeScratchRoot) "azclips-upgrade-$([guid]::NewGuid().ToString('N'))"
        if (Test-Path -LiteralPath $scratchPath) { throw "Upgrade scratch directory already exists: $scratchPath" }
        New-AzArtifactDirectory -Path $scratchPath
        $context.Scratch = $scratchPath
        $gallery = Join-Path $context.Scratch 'gallery'
        New-AzArtifactDirectory -Path $gallery
        foreach ($package in $baselinePackages)
        {
            $null = Get-PSUpgradePayload -PackagePath $package.PackagePath
            Copy-Item -LiteralPath $package.PackagePath -Destination $gallery -ErrorAction Stop
        }
        $installer = Join-Path $baselineGallery 'InstallModule.ps1'
        Assert-AzArtifactNoLinks -Path $installer
        Copy-Item -LiteralPath $installer -Destination $gallery -ErrorAction Stop
        foreach ($name in 'LICENSE', 'NOTICE')
        {
            $path = Join-Path $baselineGallery $name
            if (Test-Path -LiteralPath $path -PathType Leaf)
            {
                Assert-AzArtifactNoLinks -Path $path
                Copy-Item -LiteralPath $path -Destination $gallery -ErrorAction Stop
            }
        }

        $preflight = Invoke-PSUpgradeOperation $context @{
            operation = 'Preflight'; packages = @($baseline) + @($targets)
            repositoryName = $repositoryName; galleryRoot = $gallery
        } 'preflight'
        $roots = @($preflight.moduleRoots)
        foreach ($package in $baseline)
        {
            $attempted.Add($package)
        }
        $null = Invoke-PSUpgradeOperation $context @{
            operation = 'Install'; moduleName = 'Az'; version = $release.version
            repositoryName = $repositoryName; galleryRoot = $gallery; allowUnsignedArtifact = $false
        } 'install-baseline'
        $baselineInventory = Invoke-PSUpgradeOperation $context @{
            operation = 'Inspect'; packages = $baseline; repositoryName = $repositoryName; galleryRoot = $gallery
        } 'inspect-baseline'
        $null = Invoke-PSUpgradeOperation $context @{
            operation = 'Import'; modules = @($baselineInventory.modules)
        } 'import-baseline'

        foreach ($target in $targets)
        {
            $null = Get-PSUpgradePayload -PackagePath $target.PackagePath
            $destination = Join-Path $gallery ([IO.Path]::GetFileName($target.PackagePath))
            Copy-Item -LiteralPath $target.PackagePath -Destination $destination -Force -ErrorAction Stop
            if ((Get-FileHash -LiteralPath $destination).Hash -ne (Get-FileHash -LiteralPath $target.PackagePath).Hash)
            {
                throw "Pipeline package copy does not match its input: $($target.Name)"
            }
        }
        $targetOrder = [Collections.Generic.List[object]]::new()
        $orderedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($name in @($targets | Where-Object Name -ne Az | ForEach-Object Name) + @('Az'))
        {
            foreach ($target in @(Get-PSDependencyClosure -Name $name -Contracts $targets))
            {
                if ($orderedNames.Add($target.Name)) { $targetOrder.Add($target) }
            }
        }
        foreach ($target in $targetOrder)
        {
            foreach ($dependency in @(Get-PSDependencyClosure -Name $target.Name -Contracts $targets))
            {
                $attempted.Add($dependency)
            }
            $null = Invoke-PSUpgradeOperation $context @{
                operation = 'Install'; moduleName = $target.Name; version = $target.Version
                repositoryName = $repositoryName; galleryRoot = $gallery; allowUnsignedArtifact = $true
            } "install-target-$($target.Name)"
        }
        $targetInventory = Invoke-PSUpgradeOperation $context @{
            operation = 'Inspect'; packages = $targets; repositoryName = $repositoryName; galleryRoot = $gallery
        } 'inspect-target'
        $null = Invoke-PSUpgradeOperation $context @{
            operation = 'Import'; modules = @($targetInventory.modules)
        } 'import-target'
    }
    catch
    {
        $failure = $_
        $context.Report.message = $_.Exception.Message
    }
    finally
    {
        if ($attempted.Count -gt 0)
        {
            try
            {
                $reverse = @(Get-PSUpgradeCleanupPackages -Packages $attempted.ToArray())
                $null = Invoke-PSUpgradeOperation $context @{
                    operation = 'Cleanup'; packages = $reverse; moduleRoots = $roots
                    repositoryName = $repositoryName; galleryRoot = $gallery
                } 'cleanup'
            }
            catch
            {
                $context.Report.cleanupErrors.Add($_.Exception.Message)
                if (-not $failure) { $failure = $_ }
            }
        }
        if ($context.Scratch)
        {
            try { Remove-Item -LiteralPath $context.Scratch -Recurse -Force -ErrorAction Stop }
            catch
            {
                $context.Report.cleanupErrors.Add($_.Exception.Message)
                if (-not $failure) { $failure = $_ }
            }
        }
        $context.Report.status = if ($failure) { 'Failed' } else { 'Succeeded' }
        $context.Report.finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        if ($failure -and -not $context.Report.message) { $context.Report.message = $failure.Exception.Message }
        try { Save-PSUpgradeReport $context }
        catch
        {
            if (-not $failure) { $failure = $_ }
            else { Write-Warning "Could not finalize upgrade report: $($_.Exception.Message)" -WarningAction Continue }
        }
    }
    if ($failure) { throw $failure }
    return $context.Report
}
