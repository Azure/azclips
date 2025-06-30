<#
.SYNOPSIS
Builds Cirrus PowerShell plugin modules from component files.

.DESCRIPTION
This script can build either a single plugin module from a component file, or multiple
plugin modules from all .plugin files in a specified folder.

.PARAMETER ComponentPath
Path to a single component file (.plugin) to build into a module.
Cannot be used together with ComponentsFolder.

.PARAMETER ComponentsFolder
Path to a folder containing multiple component files (.plugin files).
All .plugin files in this folder will be built into separate modules.
Cannot be used together with ComponentPath.

.PARAMETER OutputPSModules
Directory where PowerShell modules are stored. Plugin modules will be created as subdirectories.
The Cirrus core module will also be built here if it doesn't exist.

.PARAMETER KeepHelpFolder
Keep the help directory after generating help files. Defaults to $false to keep the module directory clean.

.PARAMETER CreateAzureModuleOnly
Only create the Azure aggregator module by scanning existing modules in OutputPSModules. 
Does not build any plugin modules.

.EXAMPLE
Build-PluginModule.ps1 -ComponentPath "C:\path\to\databricks.plugin" -OutputPSModules "C:\PSModules"

Builds a single plugin module from the specified component file.

.EXAMPLE
Build-PluginModule.ps1 -ComponentsFolder "C:\Components" -OutputPSModules "C:\PSModules"

Builds plugin modules for all .plugin files found in the Components folder.

.EXAMPLE
Build-PluginModule.ps1 -ComponentPath "C:\path\to\databricks.plugin" -OutputPSModules "C:\PSModules" -KeepHelpFolder $true

Builds a single plugin module and keeps the help directory for inspection.

.EXAMPLE
Build-PluginModule.ps1 -CreateAzureModuleOnly -OutputPSModules "C:\PSModules"

Creates only the Azure aggregator module by scanning existing modules in the OutputPSModules directory.

.NOTES
- The script will automatically build the Cirrus core module if it's not found in OutputPSModules
- Each plugin module is created in its own subdirectory under OutputPSModules
- Help documentation is automatically generated for each module
- Use -CreateAzureModuleOnly to regenerate just the Azure aggregator module
#>

# PowerShell script parameters must be at the top level
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, Position = 0, ParameterSetName = "SingleComponent",
               HelpMessage = "Path to a single component file (.plugin) to build")]
    [string]$ComponentPath,
    
    [Parameter(Mandatory = $false, Position = 0, ParameterSetName = "MultipleComponents",
               HelpMessage = "Path to a folder containing multiple component files (.plugin files)")]
    [string]$ComponentsFolder,
    
    [Parameter(Mandatory = $false, ParameterSetName = "AzureModuleOnly",
               HelpMessage = "Only create the Azure aggregator module by scanning existing modules")]
    [switch]$CreateAzureModuleOnly,
    
    [Parameter(Mandatory = $true, Position = 1,
               HelpMessage = "Directory where PowerShell modules are stored")]
    [string]$OutputPSModules,
    
    [Parameter(Mandatory = $false, ParameterSetName = "SingleComponent",
               HelpMessage = "Keep the help directory after generating help files")]
    [Parameter(Mandatory = $false, ParameterSetName = "MultipleComponents",
               HelpMessage = "Keep the help directory after generating help files")]
    [bool]$KeepHelpFolder = $false
)

function Convert-UriToGuid {
    <#
    .SYNOPSIS
    Converts a URI to a deterministic GUID using namespace-based UUID v5 algorithm.
    
    .DESCRIPTION
    This function generates a consistent GUID from a given URI by using SHA1 hashing
    and following the UUID v5 specification. The same URI will always produce the same GUID.
    
    .PARAMETER Uri
    The URI string to convert to a GUID.
    
    .EXAMPLE
    Convert-UriToGuid -Uri "https://github.com/your-org/azure-cirrus-plugin"
    
    .OUTPUTS
    System.Guid
    The generated deterministic GUID.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri
    )
    
    # DNS namespace GUID as per RFC 4122
    $NamespaceGuid = [System.Guid]::Parse('033756d2-cf6b-4483-a8d6-f422cd3b0dd5')
    
    try {
        # Convert URI to bytes
        $uriBytes = [System.Text.Encoding]::UTF8.GetBytes($Uri)
        
        # Create SHA1 hash
        Add-Type -AssemblyName System.Security
        $sha1 = [System.Security.Cryptography.SHA1]::Create()
        
        # Combine namespace GUID bytes with URI bytes
        $namespaceBytes = $NamespaceGuid.ToByteArray()
        $combinedBytes = $namespaceBytes + $uriBytes
        
        # Compute SHA1 hash
        $hashBytes = $sha1.ComputeHash($combinedBytes)
        
        # Take first 16 bytes for GUID
        $guidBytes = $hashBytes[0..15]
        
        # Set version bits (version 5 = 0101)
        $guidBytes[6] = ($guidBytes[6] -band 0x0F) -bor 0x50
        
        # Set variant bits (10xx)
        $guidBytes[8] = ($guidBytes[8] -band 0x3F) -bor 0x80
        
        # Create GUID from byte array using the correct constructor
        # Convert to hex string first, then to GUID
        $hexString = [System.BitConverter]::ToString($guidBytes).Replace('-', '')
        $guidString = $hexString.Substring(0,8) + '-' + $hexString.Substring(8,4) + '-' + $hexString.Substring(12,4) + '-' + $hexString.Substring(16,4) + '-' + $hexString.Substring(20,12)
        $deterministicGuid = [System.Guid]::Parse($guidString)
        
        Write-Verbose "Generated GUID '$deterministicGuid' from URI '$Uri'"
        return $deterministicGuid
    }
    catch {
        Write-Error "Failed to convert URI '$Uri' to GUID: $_"
        throw
    }
    finally {
        if ($sha1) {
            $sha1.Dispose()
        }
    }
}

function Convert-PascalCase {
    <#
    .SYNOPSIS
    Converts a string to PascalCase (first letter of each word capitalized).
    
    .DESCRIPTION
    This function converts a given string to PascalCase format by capitalizing
    the first letter of each word and removing spaces, hyphens, and underscores.
    
    .PARAMETER InputString
    The string to convert to PascalCase.
    
    .EXAMPLE
    Convert-PascalCase -InputString "azure-storage-account"
    Returns "AzureStorageAccount"
    
    .OUTPUTS
    System.String
    The converted PascalCase string.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputString
    )
    
    if ([string]::IsNullOrWhiteSpace($InputString)) {
        return ""
    }
    
    # Split by common separators and filter out empty strings
    $words = $InputString -split '[-_\s]+' | Where-Object { $_ -ne "" }
    
    # Capitalize first letter of each word and join
    $pascalCase = ($words | ForEach-Object { 
        if ($_.Length -gt 0) {
            $_.Substring(0, 1).ToUpper() + $_.Substring(1).ToLower()
        }
    }) -join ""
    
    return $pascalCase
}

function New-AzureAggregatorModule {
    <#
    .SYNOPSIS
    Creates an Azure aggregator module that imports all available PowerShell modules.
    
    .DESCRIPTION
    This function scans the OutputPSModules directory for all available PowerShell modules
    and creates an Azure.psd1 aggregator module that includes all found modules in its RequiredModules.
    
    .PARAMETER OutputPSModules
    Directory containing PowerShell modules to scan and include in the aggregator.
    
    .EXAMPLE
    New-AzureAggregatorModule -OutputPSModules "C:\PSModules"
    
    .OUTPUTS
    System.IO.FileInfo
    The path to the created Azure.psd1 file.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPSModules
    )
    
    try {
        Write-Host "`n" + "="*60 -ForegroundColor Magenta
        Write-Host "CREATING AZURE AGGREGATOR MODULE" -ForegroundColor Magenta
        Write-Host "="*60 -ForegroundColor Magenta
        
        # Validate OutputPSModules directory exists
        if (-not (Test-Path $OutputPSModules)) {
            throw "OutputPSModules directory not found: $OutputPSModules"
        }
        
        # Scan for all PowerShell modules in the directory
        Write-Host "Scanning for PowerShell modules in: $OutputPSModules" -ForegroundColor Yellow
        $moduleDirectories = Get-ChildItem -Path $OutputPSModules -Directory | Where-Object {
            # Check if directory contains a .psd1 file with the same name
            $manifestPath = Join-Path $_.FullName "$($_.Name).psd1"
            Test-Path $manifestPath
        }
        
        if ($moduleDirectories.Count -eq 0) {
            Write-Warning "No PowerShell modules found in: $OutputPSModules"
            return $null
        }
        
        # Collect module names, excluding Azure itself
        $moduleNames = $moduleDirectories | Select-Object -ExpandProperty Name | Where-Object { $_ -ne "Azure" } | Sort-Object
        Write-Host "Found $($moduleNames.Count) PowerShell modules:" -ForegroundColor Green
        foreach ($moduleName in $moduleNames) {
            Write-Host "  - $moduleName" -ForegroundColor White
        }
        
        # Create Azure module directory
        $azureModulePath = Join-Path $OutputPSModules "Azure"
        if (Test-Path $azureModulePath) {
            Remove-Item $azureModulePath -Recurse -Force
            Write-Host "Removed existing Azure module directory" -ForegroundColor Gray
        }
        New-Item -Path $azureModulePath -ItemType Directory -Force | Out-Null
        Write-Host "✓ Created Azure module directory: $azureModulePath" -ForegroundColor Green
        
        # Generate GUID for Azure module
        $azureGuid = Convert-UriToGuid -Uri "crs://azure/"
        Write-Host "✓ Generated Azure module GUID: $azureGuid" -ForegroundColor Green
        
        # Create RequiredModules array string
        $requiredModulesArray = ($moduleNames | ForEach-Object { "'$_'" }) -join ",`r`n        "
        $requiredModulesValue = "@(`r`n        $requiredModulesArray`r`n    )"
        
        # Create Azure.psd1 content
        $azurePsd1Content = @"
@{
    # Module manifest for Azure aggregator module
    ModuleVersion        = '0.1.0'
    GUID                 = '$azureGuid'
    Author               = 'Azure Cirrus Team'
    CompanyName          = 'Microsoft Corporation'
    Copyright            = 'Copyright (c) Microsoft Corporation. All rights reserved.'
    Description          = 'Azure Cirrus aggregator module that imports all available Azure service modules'
    PowerShellVersion    = '7.2'
    
    # Required modules - all available PowerShell modules
    RequiredModules      = $requiredModulesValue
    
    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry
    FunctionsToExport    = @()
    
    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry  
    CmdletsToExport      = @()
    
    # Variables to export from this module
    VariablesToExport    = @()
    
    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry
    AliasesToExport      = @()
    
    # List of all files packaged with this module
    FileList             = @()
}
"@
        
        # Save Azure.psd1
        $azurePsd1Path = Join-Path $azureModulePath "Azure.psd1"
        Set-Content -Path $azurePsd1Path -Value $azurePsd1Content -Encoding UTF8
        Write-Host "✓ Created Azure module manifest: $azurePsd1Path" -ForegroundColor Green
        
        Write-Host "`nAzure aggregator module created successfully!" -ForegroundColor Magenta
        Write-Host "Module path: $azureModulePath" -ForegroundColor Cyan
        Write-Host "Required modules: $($moduleNames.Count) modules" -ForegroundColor Cyan
        Write-Host "To import all Azure modules: Import-Module '$azurePsd1Path'" -ForegroundColor Cyan
        
        return Get-Item $azurePsd1Path
    }
    catch {
        Write-Error "Failed to create Azure aggregator module: $_"
        throw
    }
}

function Build-MultiplePlugins {
    <#
    .SYNOPSIS
    Builds multiple Cirrus PowerShell plugin modules from a components folder.
    
    .DESCRIPTION
    This function scans a folder for .plugin files and builds each one into a 
    PowerShell module using Build-CirrusPlugin.
    
    .PARAMETER ComponentsFolder
    Path to the folder containing component files (.plugin files).
    
    .PARAMETER OutputPSModules
    Directory where PowerShell modules are stored. Plugin modules will be created as subdirectories.
    
    .PARAMETER KeepHelpFolder
    Keep the help directory after generating help files. Defaults to $false.
    
    .EXAMPLE
    Build-MultiplePlugins -ComponentsFolder "C:\Components" -OutputPSModules "C:\PSModules"
    
    .OUTPUTS
    System.Collections.ArrayList
    List of output directories containing the built modules.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ComponentsFolder,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputPSModules,
        
        [Parameter(Mandatory = $false)]
        [bool]$KeepHelpFolder = $false
    )
    
    try {
        Write-Host "Building Multiple Cirrus Plugins..." -ForegroundColor Cyan
        
        # Validate components folder
        if (-not (Test-Path $ComponentsFolder)) {
            throw "Components folder not found: $ComponentsFolder"
        }
        
        # Find all .plugin files
        Write-Host "Scanning for component files in: $ComponentsFolder" -ForegroundColor Yellow
        $pluginFiles = Get-ChildItem -Path $ComponentsFolder -Filter "*.plugin" -File
        
        if ($pluginFiles.Count -eq 0) {
            Write-Warning "No .plugin files found in: $ComponentsFolder"
            return @()
        }
        
        Write-Host "Found $($pluginFiles.Count) plugin file(s):" -ForegroundColor Green
        foreach ($file in $pluginFiles) {
            Write-Host "  - $($file.Name)" -ForegroundColor White
        }
        
        $results = New-Object System.Collections.ArrayList
        $successCount = 0
        $failureCount = 0
        
        # Build each plugin
        foreach ($pluginFile in $pluginFiles) {
            Write-Host "`n" + "="*60 -ForegroundColor Cyan
            Write-Host "Building plugin: $($pluginFile.Name)" -ForegroundColor Cyan
            Write-Host "="*60 -ForegroundColor Cyan
            
            try {
                $result = Build-CirrusPlugin -ComponentPath $pluginFile.FullName -OutputPSModules $OutputPSModules -KeepHelpFolder $KeepHelpFolder
                $results.Add($result) | Out-Null
                $successCount++
                Write-Host "✓ Successfully built: $($pluginFile.Name)" -ForegroundColor Green
            }
            catch {
                $failureCount++
                Write-Error "✗ Failed to build $($pluginFile.Name): $_"
                Write-Host "Continuing with next plugin..." -ForegroundColor Yellow
            }
        }
        
        # Summary
        Write-Host "`n" + "="*60 -ForegroundColor Cyan
        Write-Host "BUILD SUMMARY" -ForegroundColor Cyan
        Write-Host "="*60 -ForegroundColor Cyan
        Write-Host "Total plugins found: $($pluginFiles.Count)" -ForegroundColor White
        Write-Host "Successfully built: $successCount" -ForegroundColor Green
        if ($failureCount -gt 0) {
            Write-Host "Failed to build: $failureCount" -ForegroundColor Red
        }
        Write-Host "Output directory: $OutputPSModules" -ForegroundColor Cyan
        
        # Create Azure aggregator module if we have successfully built plugins
        if ($successCount -gt 0) {
            try {
                New-AzureAggregatorModule -OutputPSModules $OutputPSModules | Out-Null
            }
            catch {
                Write-Warning "Failed to create Azure aggregator module: $_"
                Write-Host "Individual plugin modules are still available" -ForegroundColor Yellow
            }
        }
        
        return $results
    }
    catch {
        Write-Error "Failed to build multiple plugins: $_"
        throw
    }
}

function Build-CirrusPlugin {
    <#
    .SYNOPSIS
    Builds a Cirrus PowerShell plugin module from a component file.
    
    .DESCRIPTION
    This function creates a PowerShell module for a Cirrus component by:
    1. Ensuring Cirrus module is available (building if necessary)
    2. Copying the component file to the output directory
    3. Importing the component to get metadata
    4. Generating module files with proper configuration
    5. Generating help documentation
    
    .PARAMETER ComponentPath
    Path to the component file to build into a module.
    
    .PARAMETER OutputPSModules
    Directory where PowerShell modules are stored. Plugin module will be created as a subdirectory.
    
    .PARAMETER KeepHelpFolder
    Keep the help directory after generating help files. Defaults to $false.
    
    .EXAMPLE
    Build-CirrusPlugin -ComponentPath "C:\path\to\component.plugin" -OutputPSModules "C:\PSModules"
    
    .NOTES
    This function is also called by Build-MultiplePlugins when processing multiple components.
    
    .OUTPUTS
    System.IO.DirectoryInfo
    The output directory containing the built module.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ComponentPath,
        
        [Parameter(Mandatory = $true)]
        [string]$OutputPSModules,
        
        [Parameter(Mandatory = $false)]
        [bool]$KeepHelpFolder = $false
    )
    
    try {
        Write-Host "Building Cirrus Plugin..." -ForegroundColor Cyan
        
        # Step 1: Validate input paths
        Write-Host "Step 1: Validating input paths..." -ForegroundColor Yellow
        if (-not (Test-Path $ComponentPath)) {
            throw "Component file not found: $ComponentPath"
        }
        
        # Step 2: Ensure Cirrus module is available
        Write-Host "Step 2: Ensuring Cirrus module is available..." -ForegroundColor Yellow
        $cirrusModulePath = Join-Path $OutputPSModules "Cirrus"
        $cirrusDllPath = Join-Path $cirrusModulePath "Cirrus.dll"
        
        if (-not (Test-Path $cirrusDllPath)) {
            Write-Host "Cirrus module not found. Building Cirrus module..." -ForegroundColor Yellow
            $buildScriptPath = Join-Path $PSScriptRoot "Build-CirrusModule.ps1"
            
            if (-not (Test-Path $buildScriptPath)) {
                throw "Build-CirrusModule.ps1 not found at: $buildScriptPath"
            }
            
            # Build Cirrus module with Release configuration
            Write-Host "Building Cirrus module with Release configuration..." -ForegroundColor White
            & $buildScriptPath -Release -OutputPSModules $OutputPSModules
            
            if (-not (Test-Path $cirrusDllPath)) {
                throw "Failed to build Cirrus module"
            }
        }
        
        # Import Cirrus module
        Write-Host "Importing Cirrus module..." -ForegroundColor Green
        $cirrusManifestPath = Join-Path $cirrusModulePath "Cirrus.psd1"
        Import-Module $cirrusManifestPath -Force
        
        # Step 3: Import component and get metadata
        Write-Host "Step 3: Importing component to get metadata..." -ForegroundColor Yellow
        $componentInfo = [Cirrus.Core.PSWrapper]::ImportPluginComponent($ComponentPath)
        
        if ($null -eq $componentInfo) {
            throw "Failed to import component from: $ComponentPath"
        }
        
        Write-Host "Component imported successfully:" -ForegroundColor Green
        Write-Host "  URI: $($componentInfo.Uri)" -ForegroundColor White
        Write-Host "  Name: $($componentInfo.Name)" -ForegroundColor White
        Write-Host "  Version: $($componentInfo.Version)" -ForegroundColor White
        
        # Convert name to PascalCase for module name
        $moduleName = Convert-PascalCase -InputString $componentInfo.Name
        Write-Host "  Module Name: $moduleName" -ForegroundColor White
        
        # Step 4: Create plugin module directory
        Write-Host "Step 4: Creating plugin module directory..." -ForegroundColor Yellow
        $pluginModulePath = Join-Path $OutputPSModules $moduleName
        
        if (Test-Path $pluginModulePath) {
            Remove-Item $pluginModulePath -Recurse -Force
            Write-Host "Removed existing plugin module directory" -ForegroundColor Gray
        }
        
        New-Item -Path $pluginModulePath -ItemType Directory -Force | Out-Null
        Write-Host "Created plugin module directory: $pluginModulePath" -ForegroundColor Green
        
        # Step 5: Copy component file to plugin module directory
        Write-Host "Step 5: Copying component file..." -ForegroundColor Yellow
        $targetComponentPath = Join-Path $pluginModulePath "component.plugin"
        Copy-Item -Path $ComponentPath -Destination $targetComponentPath -Force
        Write-Host "Component copied to: $targetComponentPath" -ForegroundColor Green
        
        # Step 6: Generate GUID from URI
        Write-Host "Step 6: Generating GUID from URI..." -ForegroundColor Yellow
        $moduleGuid = Convert-UriToGuid -Uri $componentInfo.Uri
        Write-Host "Generated GUID: $moduleGuid" -ForegroundColor Green
        
        # Step 7: Create module manifest
        Write-Host "Step 7: Creating module manifest..." -ForegroundColor Yellow
        $sourcePsd1 = Join-Path $PSScriptRoot "Plugin.psd1"
        $targetPsd1 = Join-Path $pluginModulePath "$moduleName.psd1"
        
        $psd1Content = Get-Content $sourcePsd1 -Raw
        
        # Replace placeholders
        $psd1Content = $psd1Content -replace "ModuleVersion\s*=\s*''", "ModuleVersion = '$($componentInfo.Version)'"
        $psd1Content = $psd1Content -replace "GUID\s*=\s*''", "GUID = '$moduleGuid'"
        $psd1Content = $psd1Content -replace "RootModule\s*=\s*''", "RootModule = '$moduleName.psm1'"
        
        # Set FunctionsToExport to '*' for help generation (similar to Build-CirrusModule.ps1)
        $psd1Content = $psd1Content -replace "FunctionsToExport\s*=\s*[^`n]+", "FunctionsToExport    = '*'"
        
        # Save modified psd1 temporarily for help generation
        Set-Content -Path $targetPsd1 -Value $psd1Content -Encoding UTF8
        Write-Host "Created module manifest: $targetPsd1" -ForegroundColor Green
        
        # Step 8: Create module script
        Write-Host "Step 8: Creating module script..." -ForegroundColor Yellow
        $sourcePsm1 = Join-Path $PSScriptRoot "Plugin.psm1"
        $targetPsm1 = Join-Path $pluginModulePath "$moduleName.psm1"
        
        $psm1Content = Get-Content $sourcePsm1 -Raw
        
        # Replace placeholders
        $psm1Content = $psm1Content -replace '\$script:ModuleName\s*=\s*""', "`$script:ModuleName = `"$moduleName`""
        $psm1Content = $psm1Content -replace '\$script:ComponentUri\s*=\s*""', "`$script:ComponentUri = `"$($componentInfo.Uri)`""
        
        # Save modified psm1
        Set-Content -Path $targetPsm1 -Value $psm1Content -Encoding UTF8
        Write-Host "Created module script: $targetPsm1" -ForegroundColor Green
        
        # Step 9: Generate Help Documentation and Update Manifest
        Write-Host "Step 9: Generating Help Documentation and Finalizing Manifest..." -ForegroundColor Yellow
        try {
            # Import the plugin module
            Write-Host "  Importing $moduleName module..." -ForegroundColor White
            Import-Module $targetPsd1 -Force
            
            # Get actual exported cmdlets and functions
            $actualCmdlets = Get-Command -Module $moduleName -CommandType Cmdlet | Select-Object -ExpandProperty Name
            $actualFunctions = Get-Command -Module $moduleName -CommandType Function | Select-Object -ExpandProperty Name
            Write-Host "  Found $($actualCmdlets.Count) cmdlets: $($actualCmdlets -join ', ')" -ForegroundColor Gray
            Write-Host "  Found $($actualFunctions.Count) functions: $($actualFunctions -join ', ')" -ForegroundColor Gray
            
            # Import PlatyPS module
            Write-Host "  Importing PlatyPS module..." -ForegroundColor White
            Import-Module platyps -Force
            
            # Create help directory
            $helpDir = Join-Path $pluginModulePath "help"
            if (-not (Test-Path $helpDir)) {
                New-Item -ItemType Directory -Path $helpDir -Force | Out-Null
                Write-Host "  ✓ Created help directory: $helpDir" -ForegroundColor Green
            }
            
            # Generate markdown help files
            Write-Host "  Generating markdown help files..." -ForegroundColor White
            New-MarkdownHelp -Module $moduleName -OutputFolder $helpDir -Force
            Write-Host "  ✓ Generated markdown help files" -ForegroundColor Green
            
            # Generate external help XML file directly to module directory
            Write-Host "  Generating external help XML file..." -ForegroundColor White
            New-ExternalHelp -Path $helpDir -OutputPath $pluginModulePath -Force
            
            # Check if XML file was generated
            $xmlFile = Join-Path $pluginModulePath "$moduleName-help.xml"
            if (Test-Path $xmlFile) {
                Write-Host "  ✓ Generated external help XML file: $xmlFile" -ForegroundColor Green
            }
            else {
                Write-Warning "  No XML help file was generated"
            }
            
            # Remove the imported modules
            Remove-Module $moduleName -Force
            Remove-Module platyps -Force
            
            # Clean up help directory if not keeping it
            if (-not $KeepHelpFolder -and (Test-Path $helpDir)) {
                Remove-Item $helpDir -Recurse -Force
                Write-Host "  ✓ Removed help directory: $helpDir" -ForegroundColor Green
            }
            
            # Update manifest with actual cmdlets, functions and file list
            Write-Host "  Updating module manifest with actual cmdlets, functions and files..." -ForegroundColor White
            $psd1Content = Get-Content $targetPsd1 -Raw
            
            # Update CmdletsToExport with actual cmdlets
            if ($actualCmdlets.Count -gt 0) {
                $cmdletsArray = ($actualCmdlets | ForEach-Object { "'$_'" }) -join ', '
                $cmdletsToExportValue = "@($cmdletsArray)"
                $psd1Content = $psd1Content -replace "CmdletsToExport\s*=\s*[^`n]+", "CmdletsToExport      = $cmdletsToExportValue"
                Write-Host "  ✓ Updated CmdletsToExport with actual cmdlets" -ForegroundColor Green
            }
            
            # Update FunctionsToExport with actual functions (similar to Build-CirrusModule.ps1)
            if ($actualFunctions.Count -gt 0) {
                $functionsArray = ($actualFunctions | ForEach-Object { "'$_'" }) -join ', '
                $functionsToExportValue = "@($functionsArray)"
                $psd1Content = $psd1Content -replace "FunctionsToExport\s*=\s*[^`n]+", "FunctionsToExport    = $functionsToExportValue"
                Write-Host "  ✓ Updated FunctionsToExport with actual functions" -ForegroundColor Green
            }
            else {
                Write-Host "  ○ No functions found in module, keeping FunctionsToExport as '*'" -ForegroundColor Gray
            }
            
            # Update FileList with actual files
            $actualFiles = Get-ChildItem -Path $pluginModulePath -File | Select-Object -ExpandProperty Name
            if ($actualFiles.Count -gt 0) {
                $filesArray = ($actualFiles | ForEach-Object { "'$_'" }) -join ",`r`n        "
                $fileListValue = "@(`r`n        $filesArray`r`n    )"
                $psd1Content = $psd1Content -replace "FileList\s*=\s*@\(\s*\)", "FileList = $fileListValue"
                Write-Host "  ✓ Updated FileList with actual files" -ForegroundColor Green
            }
            
            # Save updated manifest
            Set-Content -Path $targetPsd1 -Value $psd1Content -Encoding UTF8
            
            Write-Host "  ✓ Help documentation and manifest finalized successfully" -ForegroundColor Green
        }
        catch {
            Write-Warning "  Failed to generate help documentation or update manifest: $_"
            Write-Host "  ⚠ Help generation failed, but build will continue" -ForegroundColor Yellow
            
            # Clean up any imported modules
            try {
                Remove-Module $moduleName -Force -ErrorAction SilentlyContinue
                Remove-Module platyps -Force -ErrorAction SilentlyContinue
            }
            catch {
                # Ignore cleanup errors
            }
        }
        
        Write-Host "`nBuild completed successfully!" -ForegroundColor Green
        Write-Host "Module Name: $moduleName" -ForegroundColor Cyan
        Write-Host "Module GUID: $moduleGuid" -ForegroundColor Cyan
        Write-Host "Plugin Module Path: $pluginModulePath" -ForegroundColor Cyan
        
        return Get-Item $pluginModulePath
    }
    catch {
        Write-Error "Failed to build Cirrus plugin: $_"
        throw
    }
}

# Check if script was called directly or dot-sourced
if ($MyInvocation.InvocationName -ne '.' -and $MyInvocation.Line -ne '') {
    # Script was called directly, not dot-sourced
    
    # Validate that OutputPSModules is provided
    if ([string]::IsNullOrEmpty($OutputPSModules)) {
        Write-Error "OutputPSModules parameter is required."
        Write-Host "Usage:" -ForegroundColor Cyan
        Write-Host "  Single component: .\Build-PluginModule.ps1 -ComponentPath <path> -OutputPSModules <path>" -ForegroundColor White
        Write-Host "  Multiple components: .\Build-PluginModule.ps1 -ComponentsFolder <path> -OutputPSModules <path>" -ForegroundColor White
        exit 1
    }
    
    # Validate parameter sets based on PSCmdlet.ParameterSetName
    if ($PSCmdlet.ParameterSetName -eq "SingleComponent" -and [string]::IsNullOrEmpty($ComponentPath)) {
        Write-Error "ComponentPath parameter is required when using SingleComponent parameter set."
        Write-Host "Usage: .\Build-PluginModule.ps1 -ComponentPath <path> -OutputPSModules <path>" -ForegroundColor Cyan
        exit 1
    }
    
    if ($PSCmdlet.ParameterSetName -eq "MultipleComponents" -and [string]::IsNullOrEmpty($ComponentsFolder)) {
        Write-Error "ComponentsFolder parameter is required when using MultipleComponents parameter set."
        Write-Host "Usage: .\Build-PluginModule.ps1 -ComponentsFolder <path> -OutputPSModules <path>" -ForegroundColor Cyan
        exit 1
    }
    
    # Execute based on parameter set
    switch ($PSCmdlet.ParameterSetName) {
        "SingleComponent" {
            # Single component mode
            Write-Host "Building single component..." -ForegroundColor Cyan
            $result = Build-CirrusPlugin -ComponentPath $ComponentPath -OutputPSModules $OutputPSModules -KeepHelpFolder $KeepHelpFolder
            
            # Create or update Azure aggregator module
            if ($result) {
                try {
                    New-AzureAggregatorModule -OutputPSModules $OutputPSModules | Out-Null
                }
                catch {
                    Write-Warning "Failed to create Azure aggregator module: $_"
                    Write-Host "Individual plugin module is still available" -ForegroundColor Yellow
                }
            }
        }
        
        "MultipleComponents" {
            # Multiple components mode
            Write-Host "Building multiple components..." -ForegroundColor Cyan
            Build-MultiplePlugins -ComponentsFolder $ComponentsFolder -OutputPSModules $OutputPSModules -KeepHelpFolder $KeepHelpFolder
        }
        
        "AzureModuleOnly" {
            # Azure aggregator module only mode
            Write-Host "Creating Azure aggregator module only..." -ForegroundColor Cyan
            try {
                $azureModule = New-AzureAggregatorModule -OutputPSModules $OutputPSModules
                if ($azureModule) {
                    Write-Host "`n=== Azure Module Creation Complete ===" -ForegroundColor Green
                    Write-Host "Azure aggregator module created successfully!" -ForegroundColor White
                    Write-Host "Module path: $($azureModule.DirectoryName)" -ForegroundColor Cyan
                    Write-Host "To import all Azure modules: Import-Module '$($azureModule.FullName)'" -ForegroundColor Cyan
                }
            }
            catch {
                Write-Error "Failed to create Azure aggregator module: $_"
                exit 1
            }
        }
        
        default {
            Write-Error "Invalid parameter set: $($PSCmdlet.ParameterSetName)"
            Write-Host "Usage:" -ForegroundColor Cyan
            Write-Host "  Single component: .\Build-PluginModule.ps1 -ComponentPath <path> -OutputPSModules <path>" -ForegroundColor White
            Write-Host "  Multiple components: .\Build-PluginModule.ps1 -ComponentsFolder <path> -OutputPSModules <path>" -ForegroundColor White
            Write-Host "  Azure module only: .\Build-PluginModule.ps1 -CreateAzureModuleOnly -OutputPSModules <path>" -ForegroundColor White
            exit 1
        }
    }
}
else {
    # Script was dot-sourced, export functions for module usage
    Export-ModuleMember -Function New-AzureAggregatorModule, Build-MultiplePlugins, Build-CirrusPlugin
}
