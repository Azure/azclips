# AgentHelper.ps1 - AI-powered natural language support for PowerShell

# Find the az CLI executable
$script:AzCliPath = $null

# Try common locations
$possiblePaths = @(
    "$PSScriptRoot/../../../artifacts/Release/Core.Cli/az",
    "$PSScriptRoot/../../../artifacts/Debug/Core.Cli/az",
    (Get-Command az -ErrorAction SilentlyContinue).Path
)

foreach ($path in $possiblePaths) {
    if ($path -and (Test-Path $path)) {
        $script:AzCliPath = $path
        break
    }
}

if (-not $script:AzCliPath) {
    Write-Warning "az CLI not found. Build the solution first with: dotnet build cirrus.slnx"
}

function Invoke-AzV2 {
    <#
    .SYNOPSIS
        Execute natural language commands using AI agent.
    
    .DESCRIPTION
        Sends natural language queries to the AI agent, which interprets intent
        and can generate executable PowerShell cmdlets.
    
    .PARAMETER Query
        Natural language description of what you want to do.
    
    .EXAMPLE
        Invoke-AzV2 "create a resource group called test"
    
    .EXAMPLE
        ask "show me all my resource groups"
    
    .EXAMPLE
        please "help me with resource groups"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromRemainingArguments = $true)]
        [string[]]$Query
    )
    
    if (-not $script:AzCliPath) {
        Write-Error "az CLI not found. Build the solution first with: dotnet build cirrus.slnx"
        return
    }
    
    # Set flavor to indicate PowerShell context
    $env:AZURECLI_FLAVOR = "azure powershell"
    
    try {
        # Join query into single string
        $command = $Query -join ' '
        
        # Track if we have a command to execute
        $script:commandToExecute = $null
        
        # Call CLI with natural language input
        $output = & $script:AzCliPath $command.Split(' ') 2>&1
        
        foreach ($line in $output) {
            $lineText = $line.ToString()
            
            # Look for execution marker from AI
            if ($lineText -match '^__PSEXEC__(.+)$') {
                $script:commandToExecute = $matches[1]
                Write-Verbose "AI generated command: $script:commandToExecute"
            }
            # Show progress during AI thinking
            elseif ($lineText -match 'Thinking|Processing') {
                Write-Progress -Activity "AI Agent" -Status "Processing your request..."
            }
            # Display other output
            else {
                Write-Host $lineText -ForegroundColor Cyan
            }
        }
        
        # Execute extracted PowerShell command
        if ($script:commandToExecute) {
            Write-Host "`nExecuting: " -NoNewline -ForegroundColor Green
            Write-Host $script:commandToExecute -ForegroundColor White
            Write-Host ""
            
            try {
                Invoke-Expression $script:commandToExecute
            }
            catch {
                Write-Error "Failed to execute command: $_"
            }
        }
    }
    finally {
        # Clean up environment variable
        if (Test-Path Env:\AZURECLI_FLAVOR) {
            Remove-Item Env:\AZURECLI_FLAVOR
        }
        Write-Progress -Activity "AI Agent" -Completed
    }
}

# Create convenient aliases for natural language commands
Set-Alias -Name ask -Value Invoke-AzV2 -Description "Ask the AI agent a question"
Set-Alias -Name please -Value Invoke-AzV2 -Description "Request the AI agent to perform an action"
Set-Alias -Name pls -Value Invoke-AzV2 -Description "Short form of please"
