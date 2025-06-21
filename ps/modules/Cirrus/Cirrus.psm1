# PowerShell Module for Azure Cirrus
# This module provides cmdlets for Azure Cirrus cloud management using Rust backend

# # Load the compiled C# assembly first
# Add-Type -Path "$PSScriptRoot\Cirrus.dll"

# Initialize the Rust library
Write-Host "Initializing Cirrus module..." -ForegroundColor Cyan
try {
    [Cirrus.Core.PSWrapper]::Initialize()
    Write-Host "✓ Cirrus library initialized successfully" -ForegroundColor Green
}
catch {
    Write-Warning "Failed to initialize Cirrus library: $_"
    Write-Host "Make sure the Rust library (az_ps.dll) is built and available" -ForegroundColor Yellow
}

# Module variables
$script:ModuleVersion = "1.0.0"
$script:ModuleName = "Cirrus"

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
        $rustVersion = [Cirrus.Core.PSWrapper]::GetVersion()
        $isInitialized = [Cirrus.Core.PSWrapper]::IsInitialized
        
        [PSCustomObject]@{
            PSModuleName       = $script:ModuleName
            PSModuleVersion    = $script:ModuleVersion
            RustLibraryVersion = $rustVersion
            IsInitialized      = $isInitialized
            LoadedAt           = Get-Date
        }
    }
    catch {
        [PSCustomObject]@{
            PSModuleName       = $script:ModuleName
            PSModuleVersion    = $script:ModuleVersion
            RustLibraryVersion = "Not available"
            IsInitialized      = $false
            LoadedAt           = Get-Date
            Error              = $_.Exception.Message
        }
    }
}

function Invoke-CirrusCommand {
    <#
    .SYNOPSIS
    Invokes a Cirrus command using the Rust backend
    
    .DESCRIPTION
    Executes a command through the Cirrus Rust library with the specified URI and parameters
    
    .PARAMETER Uri
    The command URI to execute
    
    .PARAMETER Parameters
    The parameters to pass to the command (as hashtable)
    
    .EXAMPLE
    Invoke-CirrusCommand -Uri "az://vm/list" -Parameters @{resourceGroup="mygroup"}
    
    .EXAMPLE
    Invoke-CirrusCommand -Uri "az://storage/create" -Parameters @{name="mystorageaccount"; location="eastus"}
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [Parameter()]
        [hashtable]$Parameters = @{}
    )
    
    try {
        # Convert hashtable to JSON
        $parametersJson = $Parameters | ConvertTo-Json -Depth 10 -Compress
        
        # Call the Rust library
        $resultJson = [Cirrus.Core.PSWrapper]::RunCommand($Uri, $parametersJson)
        
        # Parse and return the result
        if ([string]::IsNullOrEmpty($resultJson) -or $resultJson -eq "{}") {
            Write-Host "Command executed successfully (no output)" -ForegroundColor Green
            return $null
        }
        
        $result = $resultJson | ConvertFrom-Json
        return $result
        
    }
    catch {
        Write-Error "Failed to execute Cirrus command: $_"
        throw
    }
}

# Module cleanup when being removed
$ExecutionContext.SessionState.Module.OnRemove = {
    Write-Host "Cirrus module is being removed" -ForegroundColor Yellow
}
