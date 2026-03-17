function Disconnect-CirrusAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Disconnect-CirrusAccount" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Login";
            $commandName = "Disconnect-CirrusAccount";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            $invocation = [Core.Runtime.PSCommandInvocation]::New($extensionName, $commandName, $parameters);

            if ($PSCmdlet.ShouldProcess("Cirrus Account", "Disconnect")) {
                $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None);
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
