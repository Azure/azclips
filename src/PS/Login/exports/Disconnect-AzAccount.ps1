# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

function Disconnect-AzAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Disconnect-AzAccount" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.AzclipsServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Login";
            $commandName = "Disconnect-AzAccount";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)

            if ($PSCmdlet.ShouldProcess("Az Account", "Disconnect")) {
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
