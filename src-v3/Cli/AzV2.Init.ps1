# AzV2 PowerShell Integration
# Enables natural language commands via a simple wrapper function

$script:AzV2CliPath = "$PSScriptRoot/azv2"

# Create a function that accepts any arguments and passes them to azv2
function Invoke-AzV2 {
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments=$true)]
        [string[]]$Arguments
    )
    
    # Join all arguments into a single command string
    $command = $Arguments -join ' '
    
    # Set environment variable for PowerShell flavor
    $env:AZURECLI_FLAVOR = "azure powershell"
    
    # Set environment variable for PowerShell flavor
    $env:AZURECLI_FLAVOR = "azure powershell"
    
    try {
        # For interactive sessions, just pass through directly to preserve prompts
        if ([Console]::IsInputRedirected -eq $false) {
            # Interactive - run directly without redirection to allow real-time streaming
            # and interactive prompts (no buffering), but capture output to look for __PSEXEC__ marker
            $script:commandToExecute = $null
            $isThinking = $false
            $startTime = $null
            
            & $script:AzV2CliPath $command 2>&1 | ForEach-Object {
                $line = $_.ToString()
                
                # Check for command execution marker
                if ($line -match '^__PSEXEC__(.+)$') {
                    # Store command but don't display the marker
                    $script:commandToExecute = $matches[1]
                }
                # Filter out spinner lines (lines with spinner characters + "Thinking...")
                elseif ($line -match '[\u280b\u2819\u2839\u2838\u283c\u2834\u2826\u2827\u2807\u280f]\s*Thinking') {
                    if (-not $isThinking) {
                        $isThinking = $true
                        $startTime = Get-Date
                    }
                    # Show progress bar (assume 20 second operation)
                    $elapsed = ((Get-Date) - $startTime).TotalSeconds
                    $percentComplete = [Math]::Min(95, [int](($elapsed / 20.0) * 100))
                    Write-Progress -Activity "AI Agent" -Status "Thinking..." -PercentComplete $percentComplete
                }
                else {
                    # Clear progress bar if it was showing
                    if ($isThinking) {
                        Write-Progress -Activity "AI Agent" -Completed
                        $isThinking = $false
                    }
                    # Display all other output immediately in cyan (real-time streaming)
                    Write-Host $line -ForegroundColor Cyan
                }
            }
            
            # Clear any remaining progress bar
            if ($isThinking) {
                Write-Progress -Activity "AI Agent" -Completed
            }
            
            # Execute the command if one was extracted
            if ($script:commandToExecute) {
                Write-Host "`nExecuting: $script:commandToExecute" -ForegroundColor Green
                Invoke-Expression $script:commandToExecute
                $script:commandToExecute = $null
            }
        }
        else {
            # Non-interactive - use the pipeline approach with progress bar
            $allLines = @()
            $isThinking = $false
            $progressCounter = 0
            
            & $script:AzV2CliPath $command | ForEach-Object {
                $line = $_.ToString()
                
                if ($line -match '[\u280B\u2819\u2839\u2838\u283C\u2834\u2826\u2827\u2807\u280F].+Thinking') {
                    if (-not $isThinking) {
                        $isThinking = $true
                        $progressCounter = 0
                    }
                    $progressCounter = ($progressCounter + 5) % 100
                    Write-Progress -Activity "AI Agent" -Status "Thinking..." -PercentComplete $progressCounter
                }
                else {
                    if ($isThinking) {
                        Write-Progress -Activity "AI Agent" -Completed
                        $isThinking = $false
                    }
                    
                    if ($line -match '^\s*$') {
                        Write-Host ""
                    }
                    else {
                        Write-Host $line -ForegroundColor Cyan
                        $allLines += $line
                    }
                }
            }
            
            if ($isThinking) {
                Write-Progress -Activity "AI Agent" -Completed
            }
            
            $executableCommand = $null
            $foundDone = $false
            
            foreach ($line in $allLines) {
                if ($line -match '\[DONE\]') {
                    $foundDone = $true
                }
                
                $cleanLine = $line -replace '\x1b\[[0-9;]*m', ''
                $trimmed = $cleanLine.Trim()
                
                if ($trimmed -match '^(New-AzV2|Get-AzV2|Set-AzV2|Remove-AzV2)' -and 
                    $trimmed -notmatch '<.*>') {
                    $executableCommand = $trimmed
                }
            }
            
            if ($foundDone -and $executableCommand) {
                Write-Host "`nExecuting: $executableCommand" -ForegroundColor Green
                Invoke-Expression $executableCommand
            }
        }
    }
    finally {
        Remove-Item Env:\AZURECLI_FLAVOR -ErrorAction SilentlyContinue
    }
}

# Create aliases for natural language commands
Set-Alias -Name ask -Value Invoke-AzV2 -Scope Global
Set-Alias -Name azv2 -Value Invoke-AzV2 -Scope Global

Write-Host "✓ AzV2 AI agent integration enabled" -ForegroundColor Green
Write-Host "  Use: " -NoNewline -ForegroundColor Gray
Write-Host "ask <your question in natural language>" -ForegroundColor White
Write-Host "  Try: " -NoNewline -ForegroundColor Gray
Write-Host "ask create a resource group" -ForegroundColor Yellow
Write-Host "  Try: " -NoNewline -ForegroundColor Gray  
Write-Host "ask list all my VMs" -ForegroundColor Yellow
Write-Host "  Try: " -NoNewline -ForegroundColor Gray  
Write-Host "ask show me storage accounts in eastus" -ForegroundColor Yellow
Write-Host ""
