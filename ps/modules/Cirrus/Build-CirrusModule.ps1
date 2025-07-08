<#
.SYNOPSIS
Builds the Cirrus PowerShell core module.

.DESCRIPTION
This script compiles the Rust library and C# assembly, then creates a complete
PowerShell module with help documentation in the specified output directory.

.PARAMETER Release
Build in Release configuration. If not specified, builds in Debug configuration.

.PARAMETER Clean
Clean previous build artifacts before building.

.PARAMETER Test
Run module tests after building.

.PARAMETER OutputPSModules
Directory where the Cirrus module will be created. A 'Cirrus' subdirectory will be created here.

.PARAMETER KeepHelpFolder
Keep the help directory after generating help files. Defaults to $false to keep the module directory clean.

.EXAMPLE
Build-CirrusModule.ps1 -OutputPSModules "C:\PSModules"

Builds the Cirrus module in Debug configuration to C:\PSModules\Cirrus

.EXAMPLE
Build-CirrusModule.ps1 -Release -OutputPSModules "C:\PSModules"

Builds the Cirrus module in Release configuration.

.EXAMPLE
Build-CirrusModule.ps1 -Clean -Release -Test -OutputPSModules "C:\PSModules"

Cleans previous builds, builds in Release configuration, and runs tests.

.EXAMPLE
Build-CirrusModule.ps1 -OutputPSModules "C:\PSModules" -KeepHelpFolder $true

Builds the module and keeps the help directory for inspection.

.NOTES
- Requires Rust toolchain and .NET SDK to be installed
- Automatically generates help documentation using PlatyPS
- The built module includes all necessary files for distribution
#>

param(
    [switch]$Release,
    [switch]$Clean,
    [switch]$Test,
    [Parameter(Mandatory = $true)]
    [string]$OutputPSModules,
    [Parameter(Mandatory = $false)]
    [bool]$KeepHelpFolder = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building Cirrus PowerShell Module ===" -ForegroundColor Cyan

$CirrusCargoName = "cirrus_ps"

# Get paths
$scriptDir = $PSScriptRoot
$psDir = Split-Path $scriptDir -Parent | Split-Path -Parent
$rootDir = Split-Path $psDir -Parent
$targetDir = Join-Path $rootDir "target"

Write-Host "Script Directory: $scriptDir" -ForegroundColor Gray
Write-Host "PS Directory: $psDir" -ForegroundColor Gray
Write-Host "Root Directory: $rootDir" -ForegroundColor Gray

# Set working directory to root directory to avoid location changes
Set-Location $rootDir
Write-Host "Set working directory to: $rootDir" -ForegroundColor Gray

# Prepare export directory early
$cirrusModuleDir = Join-Path $OutputPSModules "Cirrus"
Write-Host "Export Directory: $cirrusModuleDir" -ForegroundColor Gray

# Create the PSModules directory if it doesn't exist
if (-not (Test-Path $OutputPSModules)) {
    New-Item -Path $OutputPSModules -ItemType Directory -Force | Out-Null
    Write-Host "✓ Created PSModules directory: $OutputPSModules" -ForegroundColor Green
}

# Create the Cirrus module directory
if (Test-Path $cirrusModuleDir) {
    Remove-Item $cirrusModuleDir -Recurse -Force
    Write-Host "Removed existing Cirrus module directory" -ForegroundColor Gray
}
New-Item -Path $cirrusModuleDir -ItemType Directory -Force | Out-Null
Write-Host "✓ Created Cirrus module directory: $cirrusModuleDir" -ForegroundColor Green

# Always clean old XML help files before build
Write-Host "`nCleaning old XML help files..." -ForegroundColor Yellow
$oldXmlFiles = Get-ChildItem -Path $scriptDir -Filter "*-help.xml" -ErrorAction SilentlyContinue
if ($oldXmlFiles.Count -gt 0) {
    $oldXmlFiles | Remove-Item -Force
    Write-Host "✓ Removed $($oldXmlFiles.Count) old XML help file(s)" -ForegroundColor Green
}
else {
    Write-Host "○ No old XML help files found" -ForegroundColor Gray
}

# Clean if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    
    if (Test-Path $targetDir) {
        Remove-Item $targetDir -Recurse -Force
        Write-Host "✓ Cleaned Rust target directory" -ForegroundColor Green
    }
    
    $binDir = Join-Path $scriptDir "bin"
    if (Test-Path $binDir) {
        Remove-Item $binDir -Recurse -Force
        Write-Host "✓ Cleaned C# bin directory" -ForegroundColor Green
    }
    
    $objDir = Join-Path $scriptDir "obj"
    if (Test-Path $objDir) {
        Remove-Item $objDir -Recurse -Force
        Write-Host "✓ Cleaned C# obj directory" -ForegroundColor Green
    }
}

# Step 1: Build Rust library
Write-Host "`n--- Step 1: Building Rust Library ---" -ForegroundColor Yellow

$buildType = if ($Release) { "release" } else { "debug" }
$buildFlag = if ($Release) { "--release" } else { "" }

Write-Host "Building Rust library ($buildType)..." -ForegroundColor White

try {
    $cargoArgs = @("build", "-p", $CirrusCargoName)
    if ($Release) {
        $cargoArgs += "--release"
    }
    
    # Use explicit path for cargo command
    Push-Location $psDir
    & cargo $cargoArgs
    Pop-Location
    
    if ($LASTEXITCODE -ne 0) {
        throw "Cargo build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "✓ Rust library built successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to build Rust library: $_"
    exit 1
}

# Step 2: Copy Rust library to export directory
Write-Host "`n--- Step 2: Copying Rust Library ---" -ForegroundColor Yellow

# Ensure export directory exists before copying
if (-not (Test-Path $cirrusModuleDir)) {
    Write-Host "Creating export directory: $cirrusModuleDir" -ForegroundColor Yellow
    New-Item -Path $cirrusModuleDir -ItemType Directory -Force | Out-Null
}

$rustLibName = if ($IsWindows) { "$CirrusCargoName.dll" } elseif ($IsLinux) { "lib$CirrusCargoName.so" } else { "lib$CirrusCargoName.dylib" }
$rustLibPath = Join-Path $targetDir $buildType $rustLibName
$moduleLibPath = Join-Path $cirrusModuleDir $rustLibName

Write-Host "Copying from: $rustLibPath" -ForegroundColor Gray
Write-Host "Copying to: $moduleLibPath" -ForegroundColor Gray

if (Test-Path $rustLibPath) {
    Copy-Item $rustLibPath $moduleLibPath -Force
    Write-Host "✓ Copied $rustLibName to export directory" -ForegroundColor Green
}
else {
    Write-Error "Rust library not found at: $rustLibPath"
    exit 1
}

# Step 3: Build C# assembly
Write-Host "`n--- Step 3: Building C# Assembly ---" -ForegroundColor Yellow

Write-Host "Building C# assembly..." -ForegroundColor White

try {
    $dotnetArgs = @("build")
    if ($Release) {
        $dotnetArgs += "--configuration", "Release"
    }
    
    # Use explicit path for dotnet command
    Push-Location $scriptDir
    & dotnet $dotnetArgs
    Pop-Location
    
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "✓ C# assembly built successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to build C# assembly: $_"
    exit 1
}

# Step 4: Copy C# assembly to export directory
Write-Host "`n--- Step 4: Copying C# Assembly ---" -ForegroundColor Yellow

# Ensure export directory still exists
if (-not (Test-Path $cirrusModuleDir)) {
    Write-Host "Creating export directory: $cirrusModuleDir" -ForegroundColor Yellow
    New-Item -Path $cirrusModuleDir -ItemType Directory -Force | Out-Null
}

$configDir = if ($Release) { "Release" } else { "Debug" }
$assemblyPath = Join-Path $scriptDir "bin" $configDir "net9.0" "Cirrus.dll"
$moduleAssemblyPath = Join-Path $cirrusModuleDir "Cirrus.dll"

Write-Host "Copying from: $assemblyPath" -ForegroundColor Gray
Write-Host "Copying to: $moduleAssemblyPath" -ForegroundColor Gray

if (Test-Path $assemblyPath) {
    Copy-Item $assemblyPath $moduleAssemblyPath -Force
    Write-Host "✓ Copied Cirrus.dll to export directory" -ForegroundColor Green
}
else {
    Write-Error "C# assembly not found at: $assemblyPath"
    exit 1
}

# Step 5: Get Cirrus Version and Prepare Module Files
Write-Host "`n--- Step 5: Getting Version and Preparing Module Files ---" -ForegroundColor Yellow

try {
    # Import Cirrus.dll to get version information
    Write-Host "  Importing Cirrus.dll to get version..." -ForegroundColor White
    Import-Module $moduleAssemblyPath -Force
    
    # Get Cirrus version using PSWrapper
    [Cirrus.Core.PSWrapper]::Initialize()
    $cirrusVersion = [Cirrus.Core.PSWrapper]::GetCirrusVersion()
    Write-Host "  ✓ Retrieved Cirrus version: $cirrusVersion" -ForegroundColor Green
    
    # Remove the imported module to avoid conflicts
    Remove-Module Cirrus -Force -ErrorAction SilentlyContinue
    
    # Copy and update module manifest with version
    $sourceManifestPath = Join-Path $scriptDir "Cirrus.psd1"
    $manifestPath = Join-Path $cirrusModuleDir "Cirrus.psd1"
    
    # Read source manifest content
    $manifestContent = Get-Content $sourceManifestPath -Raw
    
    # Update version in manifest content
    $manifestContent = $manifestContent -replace "ModuleVersion\s*=\s*'[^']*'", "ModuleVersion        = '$cirrusVersion'"
    
    # Save updated manifest to export directory
    Set-Content -Path $manifestPath -Value $manifestContent -Encoding UTF8
    Write-Host "  ✓ Created module manifest with version $cirrusVersion" -ForegroundColor Green
    
    # Copy module script
    $sourceModulePath = Join-Path $scriptDir "Cirrus.psm1"
    $modulePath = Join-Path $cirrusModuleDir "Cirrus.psm1"
    Copy-Item $sourceModulePath $modulePath -Force
    Write-Host "  ✓ Copied module script to export directory" -ForegroundColor Green
    
    # Read current manifest content for help generation preparation
    $manifestContent = Get-Content $manifestPath -Raw
    
    # Backup original FunctionsToExport value
    if ($manifestContent -match "FunctionsToExport\s*=\s*([^`n]+)") {
        $originalFunctionsToExport = $matches[1].Trim()
        Write-Host "  Original FunctionsToExport: $originalFunctionsToExport" -ForegroundColor Gray
    }
    
    # Set FunctionsToExport to '*' for help generation
    $manifestContent = $manifestContent -replace "FunctionsToExport\s*=\s*[^`n]+", "FunctionsToExport    = '*'"
    Set-Content -Path $manifestPath -Value $manifestContent -Encoding UTF8
    Write-Host "  ✓ Set FunctionsToExport to '*' for help generation" -ForegroundColor Green
}
catch {
    Write-Error "Failed to get version and prepare module files: $_"
    exit 1
}

# Step 6: Generate Help Documentation
Write-Host "`n--- Step 6: Generating Help Documentation ---" -ForegroundColor Yellow

try {
    # Import the module from export directory
    Write-Host "Importing Cirrus module from export directory..." -ForegroundColor White
    Import-Module $manifestPath -Force
    
    # Import PlatyPS module
    Write-Host "Importing PlatyPS module..." -ForegroundColor White
    Import-Module platyps -Force
    
    # Create help directory in export directory
    $helpDir = Join-Path $cirrusModuleDir "help"
    if (-not (Test-Path $helpDir)) {
        New-Item -ItemType Directory -Path $helpDir -Force | Out-Null
        Write-Host "✓ Created help directory: $helpDir" -ForegroundColor Green
    }
    
    # Generate markdown help files
    Write-Host "Generating markdown help files..." -ForegroundColor White
    New-MarkdownHelp -Module Cirrus -OutputFolder $helpDir -Force
    Write-Host "✓ Generated markdown help files" -ForegroundColor Green
    
    # Generate external help XML file directly to export directory
    Write-Host "Generating external help XML file..." -ForegroundColor White
    New-ExternalHelp -Path $helpDir -OutputPath $cirrusModuleDir -Force
    
    # Ensure the XML file has the correct PowerShell naming convention
    $xmlFiles = Get-ChildItem -Path $cirrusModuleDir -Filter "*.xml"
    if ($xmlFiles.Count -gt 0) {
        Write-Host "✓ Generated external help XML file in export directory" -ForegroundColor Green
    }
    else {
        Write-Warning "No XML help file was generated"
    }
    
    # Clean up help directory if not keeping it
    if (-not $KeepHelpFolder -and (Test-Path $helpDir)) {
        Remove-Item $helpDir -Recurse -Force
        Write-Host "✓ Removed help directory: $helpDir" -ForegroundColor Green
    }
    
    # Remove the imported module
    Remove-Module Cirrus -Force
    Remove-Module platyps -Force
}
catch {
    Write-Warning "Failed to generate help documentation: $_"
    Write-Host "⚠ Help documentation generation failed, but build will continue" -ForegroundColor Yellow
    
    # Clean up any imported modules
    try {
        Remove-Module Cirrus -Force -ErrorAction SilentlyContinue
        Remove-Module platyps -Force -ErrorAction SilentlyContinue
    }
    catch {
        # Ignore cleanup errors
    }
}

# Step 7: Update Module Manifest with Actual Function List and File List
Write-Host "`n--- Step 7: Updating Module Manifest ---" -ForegroundColor Yellow

try {
    # Import the module from export directory to get actual functions
    Write-Host "Re-importing Cirrus module to get function list..." -ForegroundColor White
    Import-Module $manifestPath -Force
    
    # Get actual exported functions
    $actualFunctions = Get-Command -Module Cirrus -CommandType Function | Select-Object -ExpandProperty Name
    
    # Get actual files in the module directory
    Write-Host "Collecting actual files in module directory..." -ForegroundColor White
    $actualFiles = Get-ChildItem -Path $cirrusModuleDir -File | Select-Object -ExpandProperty Name
    Write-Host "Found $($actualFiles.Count) files: $($actualFiles -join ', ')" -ForegroundColor Gray
    
    # Read current manifest content
    $manifestContent = Get-Content $manifestPath -Raw
    
    # Update FunctionsToExport
    if ($actualFunctions.Count -gt 0) {
        # Format function names as PowerShell array
        $functionsArray = ($actualFunctions | ForEach-Object { "'$_'" }) -join ', '
        $functionsToExportValue = "@($functionsArray)"
        
        Write-Host "Found $($actualFunctions.Count) functions: $($actualFunctions -join ', ')" -ForegroundColor Gray
        
        # Update manifest with actual function list
        $manifestContent = $manifestContent -replace "FunctionsToExport\s*=\s*[^`n]+", "FunctionsToExport    = $functionsToExportValue"
        Write-Host "✓ Updated FunctionsToExport with actual function list" -ForegroundColor Green
    }
    else {
        Write-Warning "No functions found in module, keeping FunctionsToExport as '*'"
    }
    
    # Update FileList
    if ($actualFiles.Count -gt 0) {
        # Format file names as PowerShell array with proper indentation
        $filesArray = ($actualFiles | ForEach-Object { "'$_'" }) -join ",`r`n        "
        $fileListValue = "@(`r`n        $filesArray`r`n    )"
        
        # Update manifest with actual file list
        $manifestContent = $manifestContent -replace "FileList\s*=\s*@\(\s*\)", "FileList = $fileListValue"
        Write-Host "✓ Updated FileList with actual files" -ForegroundColor Green
    }
    else {
        Write-Warning "No files found in module directory, keeping FileList empty"
    }
    
    # Save updated manifest
    Set-Content -Path $manifestPath -Value $manifestContent -Encoding UTF8
    Write-Host "✓ Module manifest updated successfully" -ForegroundColor Green
    
    # Remove the imported module
    Remove-Module Cirrus -Force
}
catch {
    Write-Warning "Failed to update module manifest: $_"
    Write-Host "⚠ Module manifest update failed, but build will continue" -ForegroundColor Yellow
    
    # Clean up any imported modules
    try {
        Remove-Module Cirrus -Force -ErrorAction SilentlyContinue
    }
    catch {
        # Ignore cleanup errors
    }
}

# Step 8: Test the module (if requested)
if ($Test) {
    Write-Host "`n--- Step 8: Testing Module ---" -ForegroundColor Yellow
    
    try {
        # Import the module from the exported location
        Import-Module $manifestPath -Force
        
        # Run tests
        $testResult = Test-CirrusLibrary
        
        if ($testResult) {
            Write-Host "✓ Module tests passed" -ForegroundColor Green
        }
        else {
            Write-Host "⚠ Some module tests failed" -ForegroundColor Yellow
        }
        
        # Show version
        $version = Get-CirrusVersion
        Write-Host "Module Version Information:" -ForegroundColor Cyan
        $version | Format-List
        
        # Remove module
        Remove-Module Cirrus -Force
        
    }
    catch {
        Write-Error "Module testing failed: $_"
        exit 1
    }
}

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "The Cirrus PowerShell module has been built and exported successfully!" -ForegroundColor White
Write-Host ""
Write-Host "Module Location:" -ForegroundColor Cyan
Write-Host "  $cirrusModuleDir" -ForegroundColor White
Write-Host ""
Write-Host "To use the module:" -ForegroundColor Cyan
Write-Host "  Import-Module '$cirrusModuleDir'" -ForegroundColor White
Write-Host ""
