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

function Invoke-AzAI {
    <#
    .SYNOPSIS
        Execute natural language commands using AI agent.
    
    .DESCRIPTION
        Sends natural language queries to the AI agent, which interprets intent
        and can generate executable PowerShell cmdlets.
    
    .PARAMETER Query
        Natural language description of what you want to do.
    
    .EXAMPLE
        Invoke-AzAI "create a resource group called test"
    
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
        
        # Track thinking state for progress bar
        $isThinking = $false
        $startTime = $null
        
        # Call CLI with streaming output - use pipeline for real-time feedback
        & $script:AzCliPath $command 2>&1 | ForEach-Object {
            $line = $_.ToString()
            
            # Look for execution marker from AI
            if ($line -match '^__PSEXEC__(.+)$') {
                $script:commandToExecute = $matches[1]
                Write-Verbose "AI generated command: $script:commandToExecute"
            }
            # Filter spinner Unicode characters and show progress bar
            elseif ($line -match '[\u280b\u2819\u2839\u2838\u283c\u2834\u2826\u2827\u2807\u280f]\s*Thinking') {
                if (-not $isThinking) {
                    $isThinking = $true
                    $startTime = Get-Date
                }
                
                # Calculate progress based on elapsed time (max 95%)
                $elapsed = ((Get-Date) - $startTime).TotalSeconds
                $percentComplete = [Math]::Min(95, [int](($elapsed / 20.0) * 100))
                
                Write-Progress -Activity "AI Agent" -Status "Thinking..." -PercentComplete $percentComplete
            }
            # Display regular output
            else {
                # Complete progress bar when we get real output
                if ($isThinking) {
                    Write-Progress -Activity "AI Agent" -Completed
                    $isThinking = $false
                }
                
                Write-Host $line -ForegroundColor Cyan
            }
        }
        
        # Complete progress bar if still active
        if ($isThinking) {
            Write-Progress -Activity "AI Agent" -Completed
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
Set-Alias -Name ask -Value Invoke-AzAI -Description "Ask the AI agent a question"
Set-Alias -Name please -Value Invoke-AzAI -Description "Request the AI agent to perform an action"
Set-Alias -Name pls -Value Invoke-AzAI -Description "Short form of please"
