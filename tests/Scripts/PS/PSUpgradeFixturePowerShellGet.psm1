# This manager is exclusively for PSUpgradeFixtures: every operation fails closed
# unless the child driver supplied an existing, explicitly marked private root.
$ErrorActionPreference = 'Stop'

function Get-FixtureRoot
{
    if (-not $env:AZCLIPS_UPGRADE_FIXTURE_ROOT -or
        -not [IO.Path]::IsPathRooted($env:AZCLIPS_UPGRADE_FIXTURE_ROOT) -or
        -not (Test-Path -LiteralPath (Join-Path $env:AZCLIPS_UPGRADE_FIXTURE_ROOT '.upgrade-fixture')))
    {
        throw 'Private upgrade fixture root is required; refusing package-manager operations.'
    }
    return [IO.Path]::GetFullPath($env:AZCLIPS_UPGRADE_FIXTURE_ROOT)
}

function Read-FixtureState
{
    Get-Content -LiteralPath (Join-Path (Get-FixtureRoot) 'state.json') -Raw | ConvertFrom-Json -AsHashtable
}

function Save-FixtureState
{
    param($State)
    $State | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path (Get-FixtureRoot) 'state.json')
}

function Add-FixtureEvent
{
    param([hashtable]$Event)
    $Event.pid = $PID
    $Event | ConvertTo-Json -Depth 10 -Compress | Add-Content -LiteralPath (Join-Path (Get-FixtureRoot) 'events.jsonl')
}

function Get-PSRepository
{
    [CmdletBinding()]
    param([string]$Name)
    $state = Read-FixtureState
    $state.repositories.Values | Where-Object { -not $Name -or $_.Name -eq $Name } | ForEach-Object { [pscustomobject]$_ }
}

function Register-PSRepository
{
    [CmdletBinding()]
    param([string]$Name, [string]$SourceLocation, [string]$PublishLocation, [string]$InstallationPolicy)
    $root = Get-FixtureRoot
    $source = [IO.Path]::GetFullPath($SourceLocation)
    if (-not $source.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $source -ne [IO.Path]::GetFullPath($PublishLocation))
    {
        throw 'Fixture repository must remain inside the private root.'
    }
    $state = Read-FixtureState
    if ($state.repositories.ContainsKey($Name)) { throw "Fixture repository already registered: $Name" }
    $state.repositories[$Name] = @{ Name = $Name; SourceLocation = $source; InstallationPolicy = $InstallationPolicy }
    Save-FixtureState $state
    Add-FixtureEvent @{ operation = 'Register'; name = $Name; gallery = $source }
}

function Unregister-PSRepository
{
    [CmdletBinding()]
    param([string]$Name)
    $state = Read-FixtureState
    Add-FixtureEvent @{ operation = 'Unregister'; name = $Name }
    if ($env:AZCLIPS_UPGRADE_FIXTURE_OPERATION -eq 'Cleanup' -and
        $env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -in 'cleanup-error', 'cleanup-primary')
    {
        throw 'Injected fixture repository cleanup failure.'
    }
    $null = $state.repositories.Remove($Name)
    Save-FixtureState $state
}

function Get-InstalledModule
{
    [CmdletBinding()]
    param([string]$Name, [string]$RequiredVersion)
    $state = Read-FixtureState
    if (-not $RequiredVersion) { throw 'Fixture queries must pin the exact installed version.' }
    if ($state.installed.ContainsKey("$Name|$RequiredVersion"))
    {
        return [pscustomobject]$state.installed["$Name|$RequiredVersion"]
    }
    Write-Error "No installed fixture module: $Name $RequiredVersion" -ErrorId NoMatchFound -Category ObjectNotFound
}

function Read-FixturePackage
{
    param([string]$Path)
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try
    {
        $entry = @($archive.Entries | Where-Object FullName -Match '^[^/\\]+\.nuspec$')
        if ($entry.Count -ne 1) { throw 'Fixture package needs exactly one nuspec.' }
        $reader = [IO.StreamReader]::new($entry[0].Open())
        try { [xml]$xml = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $metadata = $xml.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        @{
            name = [string]$metadata.id
            version = [string]$metadata.version
            dependencies = @($metadata.SelectNodes("*[local-name()='dependencies']/*[local-name()='dependency']") |
                ForEach-Object { @{ name = $_.GetAttribute('id'); version = $_.GetAttribute('version') } })
            path = $Path
        }
    }
    finally { $archive.Dispose() }
}

function Install-FixturePackage
{
    param([string]$Name, [string]$Version, [string]$Repository, [bool]$Force, [bool]$Explicit)
    $root = Get-FixtureRoot
    $state = Read-FixtureState
    $repositoryInfo = $state.repositories[$Repository]
    if (-not $repositoryInfo) { throw "Unregistered fixture gallery: $Repository" }
    $packagePath = Join-Path $repositoryInfo.SourceLocation "$Name.$Version.nupkg"
    $package = Read-FixturePackage $packagePath
    if ($package.name -cne $Name -or $package.version -ne $Version -or
        $Name -notmatch '^Az(?:\.[A-Za-z0-9_-]+)*$' -or $Version -notmatch '^\d+\.\d+\.\d+$')
    {
        throw 'Unsafe fixture package identity.'
    }
    $key = "$Name|$Version"
    if ($state.installed.ContainsKey($key) -and -not $Force)
    {
        Add-FixtureEvent @{ operation = 'Skip'; name = $Name; version = $Version; repository = $Repository }
        return
    }
    $resolvedDependencies = @(foreach ($dependency in $package.dependencies)
    {
        $minimum = $dependency.version.Trim('[', ']')
        $candidates = @(Get-ChildItem -LiteralPath $repositoryInfo.SourceLocation -Filter "$($dependency.name).*.nupkg" -File |
            ForEach-Object { Read-FixturePackage $_.FullName } |
            Where-Object { $_.name -eq $dependency.name -and [version]$_.version -ge [version]$minimum } |
            Sort-Object { [version]$_.version })
        if ($candidates.Count -eq 0) { throw "Missing fixture dependency: $($dependency.name)" }
        # Deliberately model the conservative cached-dependency case: this fixture
        # demands explicit equal-version refreshes, not a complete PowerShellGet emulation.
        Install-FixturePackage $dependency.name $candidates[0].version $Repository $false $false
        @{ Name = $dependency.name; Version = $candidates[0].version }
    })
    $location = Join-Path $root "installed\$Name\$Version"
    $before = if (Test-Path -LiteralPath (Join-Path $location 'payload.txt'))
        { (Get-FileHash -LiteralPath (Join-Path $location 'payload.txt')).Hash } else { $null }
    if (Test-Path -LiteralPath $location) { Remove-Item -LiteralPath $location -Recurse -Force }
    $null = New-Item -ItemType Directory -Path $location -Force
    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    try
    {
        foreach ($entry in $archive.Entries)
        {
            $nameInPackage = $entry.FullName.Replace('\', '/')
            if ($nameInPackage -match '(^/|:|(^|/)\.\.?(/|$))') { throw 'Unsafe fixture archive path.' }
            # Retain client transport files; production validation decides which are payload.
            if ($nameInPackage.EndsWith('/')) { continue }
            $destination = Join-Path $location $nameInPackage
            $null = New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force
            $inputStream = $entry.Open()
            $outputStream = [IO.File]::Create($destination)
            try { $inputStream.CopyTo($outputStream) }
            finally { $inputStream.Dispose(); $outputStream.Dispose() }
        }
    }
    finally { $archive.Dispose() }
    Set-Content -LiteralPath (Join-Path $location 'PSGetModuleInfo.xml') -Value '<fixture />'
    $state = Read-FixtureState
    $state.installed[$key] = @{
        Name = $Name; Version = $Version; Repository = $Repository; InstalledLocation = $location
        Dependencies = $resolvedDependencies
    }
    Save-FixtureState $state
    Add-FixtureEvent @{
        operation = 'Install'; name = $Name; version = $Version; repository = $Repository
        gallery = $repositoryInfo.SourceLocation; explicit = $Explicit; force = $Force
        packageSha256 = (Get-FileHash -LiteralPath $packagePath).Hash
        previousPayloadSha256 = $before
        payloadSha256 = (Get-FileHash -LiteralPath (Join-Path $location 'payload.txt')).Hash
        galleryPackages = @(Get-ChildItem -LiteralPath $repositoryInfo.SourceLocation -Filter '*.nupkg' -File | ForEach-Object Name)
        dependencies = $resolvedDependencies
    }
}

function Install-Module
{
    [CmdletBinding()]
    param([string]$Name, [string]$RequiredVersion, [string]$Repository, [string]$Scope,
        [switch]$Force, [switch]$AllowClobber, [switch]$SkipPublisherCheck)
    $null = Get-FixtureRoot
    if (-not $RequiredVersion -or $Scope -ne 'CurrentUser' -or -not $Force -or -not $AllowClobber)
    {
        throw 'Fixture helper must pin RequiredVersion and use CurrentUser, Force and AllowClobber.'
    }
    $isBaseline = $Name -eq 'Az' -and $RequiredVersion -eq '0.1.2'
    Add-FixtureEvent @{
        operation = 'ManagerInstall'; name = $Name; version = $RequiredVersion
        skipPublisherCheck = $SkipPublisherCheck.IsPresent
    }
    if ($SkipPublisherCheck.IsPresent -ne (-not $isBaseline))
    {
        throw 'Publisher checks may be skipped only for pipeline artifact installs, never the released baseline.'
    }
    Install-FixturePackage $Name $RequiredVersion $Repository $Force.IsPresent $true
    if ($env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -eq 'installer-failure' -and $Name -eq 'Az' -and $RequiredVersion -eq '0.1.2')
    {
        throw 'Injected released installer failure after partial installation.'
    }
    if ($env:AZCLIPS_UPGRADE_FIXTURE_SCENARIO -eq 'target-installer-failure' -and $Name -eq 'Az.Compute')
    {
        throw 'Injected target installer failure after installation.'
    }
}

function Uninstall-Module
{
    [CmdletBinding()]
    param([string]$Name, [string]$RequiredVersion, [switch]$Force)
    $root = Get-FixtureRoot
    $state = Read-FixtureState
    if (-not $RequiredVersion -or -not $Force) { throw 'Fixture removal must pin the exact version and force removal.' }
    $installed = $state.installed["$Name|$RequiredVersion"]
    $location = Join-Path $root "installed\$Name\$RequiredVersion"
    if (-not $installed -or $installed.InstalledLocation -ne $location -or $RequiredVersion -eq '9.0.0')
    {
        throw 'Fixture removal refused an unowned or unrelated module.'
    }
    $dependents = @(foreach ($consumer in $state.installed.Values)
    {
        foreach ($dependency in $consumer.Dependencies)
        {
            if ($dependency.Name -eq $Name -and $dependency.Version -eq $RequiredVersion)
            {
                "$($consumer.Name)|$($consumer.Version)"
            }
        }
    })
    if ($dependents.Count -gt 0)
    {
        Add-FixtureEvent @{ operation = 'UninstallBlocked'; name = $Name; version = $RequiredVersion; dependents = $dependents }
        throw "Fixture refuses to uninstall $Name $RequiredVersion while required by $($dependents -join ', ')."
    }
    Add-FixtureEvent @{ operation = 'Uninstall'; name = $Name; version = $RequiredVersion }
    if (Test-Path -LiteralPath $location) { Remove-Item -LiteralPath $location -Recurse -Force }
    $null = $state.installed.Remove("$Name|$RequiredVersion")
    Save-FixtureState $state
}

$null = Get-FixtureRoot
Export-ModuleMember -Function Get-PSRepository, Register-PSRepository, Unregister-PSRepository, Get-InstalledModule, Install-Module, Uninstall-Module
