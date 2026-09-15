. (Join-Path $PSScriptRoot 'module_definitions.ps1')

function Read-PSDependencyPackage
{
    param([Parameter(Mandatory)][string]$Path)

    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try
    {
        $entries = @($archive.Entries | Where-Object { $_.FullName -match '^[^/\\]+\.nuspec$' })
        if ($entries.Count -ne 1) { throw "Expected one root nuspec in $Path." }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { [xml]$document = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $metadata = $document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if (-not $metadata) { throw "Missing package metadata in $Path." }
        $dependencies = @(
            foreach ($dependency in $metadata.SelectNodes("*[local-name()='dependencies']//*[local-name()='dependency']"))
            {
                [pscustomobject]@{ Name = $dependency.GetAttribute('id'); Version = $dependency.GetAttribute('version') }
            }
        )
        [pscustomobject]@{
            Name = [string]$metadata.id
            Version = [string]$metadata.version
            Dependencies = $dependencies
        }
    }
    finally { $archive.Dispose() }
}

function Get-PSDependencyContracts
{
    param([Parameter(Mandatory)][string]$SourcePsRoot, [Parameter(Mandatory)][string]$RepositoryRoot)

    $definitions = @(Get-AzModuleDefinitions -SourcePsRoot $SourcePsRoot)
    $byName = @{}
    foreach ($definition in $definitions)
    {
        if ($byName.ContainsKey($definition.PackageName)) { throw "Duplicate source module: $($definition.PackageName)" }
        $byName[$definition.PackageName] = $definition
    }
    $contracts = @(
        foreach ($definition in $definitions)
        {
            $sourcePath = Join-Path $SourcePsRoot "$($definition.SourceModule)/$($definition.SourceModule).psd1"
            $manifest = Import-PowerShellDataFile -LiteralPath $sourcePath -ErrorAction Stop
            $requirements = @($manifest.RequiredModules)
            $dependencies = @(
                for ($index = 0; $index -lt $definition.RequiredModules.Count; $index++)
                {
                    $name = $definition.RequiredModules[$index]
                    if (-not $byName.ContainsKey($name)) { throw "$($definition.PackageName) has an unknown source dependency: $name" }
                    $version = if ($requirements[$index] -is [string]) { $byName[$name].Version }
                        elseif ($requirements[$index].RequiredVersion) { [string]$requirements[$index].RequiredVersion }
                        else { [string]$requirements[$index].ModuleVersion }
                    if (-not ($version -as [version]) -or [version]$version -ne [version]$byName[$name].Version)
                    {
                        throw "$($definition.PackageName) requires $name $version, but its source version is $($byName[$name].Version)."
                    }
                    [pscustomobject]@{ Name = $name; Version = $version }
                }
            )
            $packagePath = Join-Path $RepositoryRoot "$($definition.PackageName).$($definition.Version).nupkg"
            $package = Read-PSDependencyPackage -Path $packagePath
            if ($package.Name -cne $definition.PackageName -or $package.Version -ne $definition.Version)
            {
                throw "Package identity does not match the source manifest: $packagePath"
            }
            if ($package.Dependencies.Count -ne $dependencies.Count) { throw "$($package.Name) dependency count differs from its source manifest." }
            foreach ($dependency in $dependencies)
            {
                $actual = @($package.Dependencies | Where-Object Name -eq $dependency.Name)
                # Existing packages declare a minimum; generated imports require the exact source version.
                if ($actual.Count -ne 1 -or $actual[0].Version -notin $dependency.Version, "[$($dependency.Version)]")
                {
                    throw "$($package.Name) metadata must declare dependency $($dependency.Name) $($dependency.Version)."
                }
            }
            [pscustomobject]@{
                Name = $definition.PackageName
                Version = $definition.Version
                Dependencies = $dependencies
                Commands = @($definition.CmdletsToExport)
                PackagePath = $packagePath
            }
        }
    )
    return $contracts
}

function Get-PSDependencyClosure
{
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][object[]]$Contracts)

    $byName = @{}
    foreach ($contract in $Contracts) { $byName[$contract.Name] = $contract }
    $visiting = @{}
    $visited = @{}
    $result = [Collections.Generic.List[object]]::new()
    function Visit-Dependency([string]$Current)
    {
        if ($visiting.ContainsKey($Current)) { throw "Circular module dependency: $Current" }
        if ($visited.ContainsKey($Current)) { return }
        if (-not $byName.ContainsKey($Current)) { throw "Unknown dependency: $Current" }
        $visiting[$Current] = $true
        foreach ($dependency in $byName[$Current].Dependencies) { Visit-Dependency $dependency.Name }
        $visiting.Remove($Current)
        $visited[$Current] = $true
        $result.Add($byName[$Current])
    }
    Visit-Dependency $Name
    return $result.ToArray()
}

function New-PSIncompatibleDependencyPackage
{
    param([Parameter(Mandatory)][string]$SourcePath, [Parameter(Mandatory)][string]$DestinationPath)

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -ErrorAction Stop
    $archive = [IO.Compression.ZipFile]::Open($DestinationPath, [IO.Compression.ZipArchiveMode]::Update)
    try
    {
        foreach ($entry in @($archive.Entries | Where-Object { $_.FullName -match '^[^/\\]+\.(nuspec|psd1)$' }))
        {
            $name = $entry.FullName
            $reader = [IO.StreamReader]::new($entry.Open())
            try { $text = $reader.ReadToEnd() }
            finally { $reader.Dispose() }
            if ($name.EndsWith('.nuspec'))
            {
                [xml]$document = $text
                $document.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='version']").InnerText = '0.0.0'
                $text = $document.OuterXml
            }
            else
            {
                $text = [regex]::Replace($text, "(?m)^(\s*ModuleVersion\s*=\s*)'[^']+'", "`${1}'0.0.0'")
            }
            $entry.Delete()
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($name).Open(), [Text.UTF8Encoding]::new($false))
            try { $writer.Write($text) }
            finally { $writer.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}

function Assert-PSDependencyFailure
{
    param([Parameter(Mandatory)]$Result, [Parameter(Mandatory)]$Dependency)

    if ($Result.status -ne 'Failed') { throw "Expected rejection of dependency $($Dependency.Name) $($Dependency.Version), but the operation succeeded." }
    $diagnostics = @($Result.message) + @($Result.diagnostics) -join "`n"
    if ($Result.errorId -notmatch '^(ProviderFailToDownloadFile|NoMatchFound|UnableToFindDependency),' -or
        $diagnostics -notmatch [regex]::Escape($Dependency.Name) -or
        $diagnostics -notmatch '(?i)(unable to find|not found|cannot find|could not find).*depend|depend.*(not found|unable to find|cannot find|could not find)')
    {
        throw "Unexpected dependency error for $($Dependency.Name): [$($Result.errorId)] $diagnostics"
    }
}

function Invoke-PSDependencyProcess
{
    param(
        [Parameter(Mandatory)][string]$PwshPath,
        [Parameter(Mandatory)][string]$WorkerPath,
        [Parameter(Mandatory)][string]$RequestPath,
        [Parameter(Mandatory)][string]$ResultPath,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [ValidateRange(1, 1800)][int]$TimeoutSeconds = 180
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $PwshPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.Environment['POWERSHELL_TELEMETRY_OPTOUT'] = '1'
    $startInfo.Environment['AZCLIPS_TELEMETRY_TARGET'] = 'local'
    $startInfo.Environment['NUGET_PACKAGES'] = Join-Path $WorkingDirectory 'nuget'
    $startInfo.Environment['NUGET_HTTP_CACHE_PATH'] = Join-Path $WorkingDirectory 'http-cache'
    foreach ($argument in '-NoLogo', '-NoProfile', '-NonInteractive', '-File', $WorkerPath, '-RequestPath', $RequestPath, '-ResultPath', $ResultPath)
    {
        $startInfo.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try
    {
        if (-not $process.Start()) { throw 'Unable to start dependency validation worker.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
        if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
        [pscustomobject]@{
            ExitCode = $process.ExitCode
            TimedOut = $timedOut
            Output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        }
    }
    finally { $process.Dispose() }
}
