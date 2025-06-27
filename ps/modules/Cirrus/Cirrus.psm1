# PowerShell Module for Azure Cirrus
# This module provides cmdlets for Azure Cirrus cloud management using Rust backend

# Import the nested module (initialization happens automatically via CirrusModuleInitializer)
Import-Module "$PSScriptRoot\Cirrus.dll" -Force

# Module variables
$script:ModuleName = "Cirrus"

# # Initialize DynamicTypeBuilder for the main module
# [Cirrus.Core.DynamicTypeBuilder]::GetOrCreateBuilder($script:ModuleName);

# Collect all functions to export (starting with built-in functions)
$script:FunctionsToExport = @(
    "Get-CirrusVersion"
)

function Get-CirrusVersion {
    <#
    .SYNOPSIS
    Gets the version information of the Cirrus module
    
    .DESCRIPTION
    Returns version information about both the PowerShell module and the underlying Rust library
    
    .EXAMPLE
    Get-CirrusVersion
    #>
    [CmdletBinding()]
    param()
    
    try {
        $rustVersion = [Cirrus.Core.PSWrapper]::GetCirrusVersion()
        $isInitialized = [Cirrus.Core.PSWrapper]::IsInitialized
        
        [PSCustomObject]@{
            PSModuleName       = $script:ModuleName
            PSModuleVersion    = $rustVersion
            RustLibraryVersion = $rustVersion
            IsInitialized      = $isInitialized
            LoadedAt           = Get-Date
        }
    }
    catch {
        [PSCustomObject]@{
            PSModuleName       = $script:ModuleName
            PSModuleVersion    = "Not available"
            RustLibraryVersion = "Not available"
            IsInitialized      = $false
            LoadedAt           = Get-Date
            Error              = $_.Exception.Message
        }
    }
}

# Dynamic cmdlet creation
try {
    Write-Host "Creating dynamic cmdlets..." -ForegroundColor Cyan
    
    # Get builtin cmdlets from Rust library (call only once)
    $builtinCmdlets = [Cirrus.Core.PSWrapper]::GetBuiltinCmdlets()
    
    if ($builtinCmdlets -and $builtinCmdlets.Count -gt 0) {
        Write-Host "Found $($builtinCmdlets.Count) builtin cmdlets" -ForegroundColor Green

        # Generate all function scripts at once using PSCmdlet template
        # Pass module name and directory for help file detection
        $allFunctionScripts = [Cirrus.Core.PSCmdlet]::GenerateFunctionScripts($builtinCmdlets, $script:ModuleName, $PSScriptRoot)

        if (![string]::IsNullOrEmpty($allFunctionScripts)) {
            # Check if external help file exists
            $helpFilePath = Join-Path $PSScriptRoot "$($script:ModuleName)-help.xml"
            
            # Execute all function definitions at once
            Invoke-Expression $allFunctionScripts
            
            # Add all valid cmdlet names to export list
            foreach ($cmdlet in $builtinCmdlets) {
                if (![string]::IsNullOrEmpty($cmdlet.Name) -and ![string]::IsNullOrEmpty($cmdlet.Uri)) {
                    $script:FunctionsToExport += $cmdlet.Name
                    Write-Verbose "Created function: $($cmdlet.Name)"
                }
            }
        }

        Write-Host "✓ Dynamic cmdlets created successfully" -ForegroundColor Green
    }
    else {
        Write-Host "No builtin cmdlets found" -ForegroundColor Yellow
    }
}
catch {
    Write-Warning "Failed to create dynamic cmdlets: $_"
    Write-Host "This may happen if the Rust library is not properly initialized" -ForegroundColor Yellow
}

# # Create 6400 fake functions for performance testing using PSCmdlet template
# try {
#     Write-Host "Creating 6400 fake functions for testing..." -ForegroundColor Cyan
    
#     # Build fake CmdletInfo array
#     $fakeCmdlets = @()
#     for ($i = 1; $i -le 6400; $i++) {
#         $fakeCmdlet = New-Object Cirrus.Core.CmdletInfo
#         $fakeCmdlet.Name = "Invoke-Fake$i"
#         $fakeCmdlet.Uri = "fake://test/function$i"
#         $fakeCmdlets += $fakeCmdlet
        
#         # Show progress every 1000 cmdlets during creation
#         if ($i % 1000 -eq 0) {
#             Write-Host "Built $i fake cmdlet infos..." -ForegroundColor Blue
#         }
#     }
    
#     Write-Host "Generating all fake function scripts using PSCmdlet template..." -ForegroundColor Cyan
    
#     # Generate all fake function scripts at once using PSCmdlet template
#     $allFakeFunctionScripts = [Cirrus.Core.PSCmdlet]::GenerateFunctionScripts($fakeCmdlets)
    
#     if (![string]::IsNullOrEmpty($allFakeFunctionScripts)) {
#         Write-Host "Executing all fake function definitions..." -ForegroundColor Cyan
        
#         # Execute all fake function definitions at once
#         Invoke-Expression $allFakeFunctionScripts
        
#         # Add all fake function names to export list
#         foreach ($fakeCmdlet in $fakeCmdlets) {
#             $script:FunctionsToExport += $fakeCmdlet.Name
#         }
        
#         Write-Host "✓ All 6400 fake functions created successfully using PSCmdlet template" -ForegroundColor Green
#     }
#     else {
#         Write-Warning "No fake function scripts were generated"
#     }
# }
# catch {
#     Write-Warning "Failed to create fake functions using PSCmdlet template: $_"
#     Write-Host "This may indicate an issue with the PSCmdlet.GenerateFunctionScripts method" -ForegroundColor Yellow
# }

# Module cleanup when being removed
$ExecutionContext.SessionState.Module.OnRemove = {
    Write-Host "Cirrus module is being removed" -ForegroundColor Yellow
}

# Export all functions (including dynamically created ones)
Export-ModuleMember -Function $script:FunctionsToExport
