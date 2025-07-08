# PowerShell Module for Azure Cirrus
# This module provides cmdlets for Azure Cirrus cloud management using Rust backend

# Import the nested module (initialization happens automatically via CirrusModuleInitializer)
Import-Module "$PSScriptRoot\Cirrus.dll" -Force

# Module variables
$script:ModuleName = "Cirrus"

# Collect all functions to export (starting with built-in functions)
$script:FunctionsToExport = @()

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

# Module cleanup when being removed
$ExecutionContext.SessionState.Module.OnRemove = {
    Write-Host "Cirrus module is being removed" -ForegroundColor Yellow
}

# Export all functions (including dynamically created ones)
Export-ModuleMember -Function $script:FunctionsToExport
