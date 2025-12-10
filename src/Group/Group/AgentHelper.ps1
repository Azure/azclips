# AzV2 AI Agent Helper
# Automatically loaded when importing the module

# Find azv2 CLI in the bin directory (parent of Group/Login folders)
$script:AzV2CliPath = Split-Path -Parent $PSScriptRoot
$script:AzV2CliPath = Join-Path $script:AzV2CliPath "azv2"

function Invoke-AzV2 {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
        [string[]]$Query
    )
    
    $command = $Query -join ' '
    $env:AZURECLI_FLAVOR = "azure powershell"
    
    try {
        # Capture output and look for command execution marker
        $script:commandToExecute = $null
        $isThinking = $false
        $startTime = $null
        
        & $script:AzV2CliPath $command 2>&1 | ForEach-Object {
            $line = $_.ToString()
            
            # Check for command execution marker
            if ($line -match '^__PSEXEC__(.+)$') {
                $script:commandToExecute = $matches[1]
            }
            # Filter out spinner lines
            elseif ($line -match '[\u280b\u2819\u2839\u2838\u283c\u2834\u2826\u2827\u2807\u280f]\s*Thinking') {
                if (-not $isThinking) {
                    $isThinking = $true
                    $startTime = Get-Date
                }
                $elapsed = ((Get-Date) - $startTime).TotalSeconds
                $percentComplete = [Math]::Min(95, [int](($elapsed / 20.0) * 100))
                Write-Progress -Activity "AI Agent" -Status "Thinking..." -PercentComplete $percentComplete
            }
            else {
                if ($isThinking) {
                    Write-Progress -Activity "AI Agent" -Completed
                    $isThinking = $false
                }
                Write-Host $line -ForegroundColor Cyan
            }
        }
        
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
    finally {
        Remove-Item Env:\AZURECLI_FLAVOR -ErrorAction SilentlyContinue
    }
}

# Create aliases
Set-Alias -Name ask -Value Invoke-AzV2 -Scope Global
Set-Alias -Name please -Value Invoke-AzV2 -Scope Global
Set-Alias -Name pls -Value Invoke-AzV2 -Scope Global
