function Disconnect-CirrusAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Disconnect-CirrusAccount" -Parameters $PSBoundParameters
    }

    process {
        try {
            $extensionName = "Login";
            $commandName = "Disconnect-CirrusAccount";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);

            if ($PSCmdlet.ShouldProcess("Cirrus Account", "Disconnect")) {
                [Core.Engine.Engine]::GetCommand($invocation).Process();
            }

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
