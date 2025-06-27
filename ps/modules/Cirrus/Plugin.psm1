
# Module variables
$script:ModuleName = ""
$script:ComponentUri = ""

[Cirrus.Core.PSWrapper]::ImportPluginComponent("$PSScriptRoot/component.plugin")

# Collect all functions to export (starting with built-in functions)
$script:FunctionsToExport = @()

try {
    # Get builtin cmdlets from Rust library (call only once)
    $componentCmdlets = [Cirrus.Core.PSWrapper]::GetComponentCmdlets($script:ComponentUri)

    if ($componentCmdlets -and $componentCmdlets.Count -gt 0) {
        Write-Host "Found $($componentCmdlets.Count) builtin cmdlets" -ForegroundColor Green

        # Generate all function scripts at once using PSCmdlet template
        # Pass module name and directory for help file detection
        $allFunctionScripts = [Cirrus.Core.PSCmdlet]::GenerateFunctionScripts($componentCmdlets, $script:ModuleName, $PSScriptRoot)

        if (![string]::IsNullOrEmpty($allFunctionScripts)) {
            # Check if external help file exists
            $helpFilePath = Join-Path $PSScriptRoot "$($script:ModuleName)-help.xml"
            
            # Execute all function definitions at once
            Invoke-Expression $allFunctionScripts
            
            # Add all valid cmdlet names to export list
            foreach ($cmdlet in $componentCmdlets) {
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

} catch {
    Write-Warning "Failed to create dynamic cmdlets: $_"
    Write-Host "This may happen if the Rust library is not properly initialized" -ForegroundColor Yellow
}


# Module cleanup when being removed
$ExecutionContext.SessionState.Module.OnRemove = {
    Write-Host "$($script:ModuleName) module is being removed" -ForegroundColor Yellow
}

# Export all functions (including dynamically created ones)
Export-ModuleMember -Function $script:FunctionsToExport
