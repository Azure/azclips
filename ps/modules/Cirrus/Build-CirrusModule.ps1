# Build script for Cirrus PowerShell module
# This script compiles the Rust library and C# assembly

param(
    [switch]$Release,
    [switch]$Clean,
    [switch]$Test
)

$ErrorActionPreference = "Stop"

Write-Host "=== Building Cirrus PowerShell Module ===" -ForegroundColor Cyan

# Get paths
$scriptDir = $PSScriptRoot
$psDir = Split-Path $scriptDir -Parent | Split-Path -Parent
$rootDir = Split-Path $psDir -Parent
$targetDir = Join-Path $rootDir "target"

Write-Host "Script Directory: $scriptDir" -ForegroundColor Gray
Write-Host "PS Directory: $psDir" -ForegroundColor Gray
Write-Host "Root Directory: $rootDir" -ForegroundColor Gray

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

Set-Location $psDir

$buildType = if ($Release) { "release" } else { "debug" }
$buildFlag = if ($Release) { "--release" } else { "" }

Write-Host "Building Rust library ($buildType)..." -ForegroundColor White

try {
    $cargoArgs = @("build")
    if ($Release) {
        $cargoArgs += "--release"
    }
    
    & cargo $cargoArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Cargo build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "✓ Rust library built successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to build Rust library: $_"
    exit 1
}

# Step 2: Copy Rust library to module directory
Write-Host "`n--- Step 2: Copying Rust Library ---" -ForegroundColor Yellow

$rustLibName = if ($IsWindows) { "az_ps.dll" } elseif ($IsLinux) { "libaz_ps.so" } else { "libaz_ps.dylib" }
$rustLibPath = Join-Path $targetDir $buildType $rustLibName
$moduleLibPath = Join-Path $scriptDir $rustLibName

if (Test-Path $rustLibPath) {
    Copy-Item $rustLibPath $moduleLibPath -Force
    Write-Host "✓ Copied $rustLibName to module directory" -ForegroundColor Green
}
else {
    Write-Error "Rust library not found at: $rustLibPath"
    exit 1
}

# Step 3: Build C# assembly
Write-Host "`n--- Step 3: Building C# Assembly ---" -ForegroundColor Yellow

Set-Location $scriptDir

Write-Host "Building C# assembly..." -ForegroundColor White

try {
    $dotnetArgs = @("build")
    if ($Release) {
        $dotnetArgs += "--configuration", "Release"
    }
    
    & dotnet $dotnetArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "✓ C# assembly built successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to build C# assembly: $_"
    exit 1
}

# Step 4: Copy C# assembly to module root
Write-Host "`n--- Step 4: Copying C# Assembly ---" -ForegroundColor Yellow

$configDir = if ($Release) { "Release" } else { "Debug" }
$assemblyPath = Join-Path $scriptDir "bin" $configDir "net9.0" "Cirrus.dll"
$moduleAssemblyPath = Join-Path $scriptDir "Cirrus.dll"

if (Test-Path $assemblyPath) {
    Copy-Item $assemblyPath $moduleAssemblyPath -Force
    Write-Host "✓ Copied Cirrus.dll to module root" -ForegroundColor Green
}
else {
    Write-Error "C# assembly not found at: $assemblyPath"
    exit 1
}

# Step 5: Test the module (if requested)
if ($Test) {
    Write-Host "`n--- Step 5: Testing Module ---" -ForegroundColor Yellow
    
    try {
        # Import the module
        Import-Module "$scriptDir\Cirrus.psd1" -Force
        
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
Write-Host "The Cirrus PowerShell module has been built successfully!" -ForegroundColor White
Write-Host ""
Write-Host "To use the module:" -ForegroundColor Cyan
Write-Host "  Import-Module '$scriptDir\Cirrus.psd1'" -ForegroundColor White
Write-Host ""
Write-Host "Available commands:" -ForegroundColor Cyan
Write-Host "  Get-CirrusVersion      - Get version information" -ForegroundColor White
Write-Host "  Test-CirrusLibrary     - Test library functionality" -ForegroundColor White
Write-Host "  Invoke-CirrusCommand   - Execute Cirrus commands" -ForegroundColor White 