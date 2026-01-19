function Get-CirrusAccount {
    [CmdletBinding()]
    param()

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Get-CirrusAccount" -Parameters $PSBoundParameters
    }

    process {
        try {
            $extensionName = "Login";
            $commandName = "Get-CirrusAccount";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();
            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);

            [Core.Abstractions.AuthContext]$output = [Core.Abstractions.AuthContext]([Core.Engine.Engine]::GetCommand($invocation).process());
            Write-Output $output;

            # Mark activity as successful
            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $true
                $script:activity = $null
            }
        }
        catch {
            # Mark activity as failed
            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $false -ErrorMessage $_.Exception.Message
                $script:activity = $null
            }
            throw
        }
    }

    end {
        # Cleanup activity if not already done
        if ($null -ne $script:activity) {
            Complete-CmdletActivity -Activity $script:activity -Success $true
            $script:activity = $null
        }
    }
}
