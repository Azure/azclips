# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

function New-AzResourceGroup {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $false)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Location
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "New-AzResourceGroup" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.AzclipsServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Resources";
            $commandName = "New-AzResourceGroup";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('SubscriptionId')) {
                $parameters.Add('SubscriptionId', [object]$SubscriptionId);
            }
            if ($PSBoundParameters.ContainsKey('Name')) {
                $parameters.Add('Name', [object]$Name);
            }
            if ($PSBoundParameters.ContainsKey('Location')) {
                $parameters.Add('Location', [object]$Location);
            }
            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)
            $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None);
            # Mark activity as successful
            if ($null -ne $script:activity) {
                $null = Complete-CmdletActivity -Activity $script:activity -Success $true
                $script:activity = $null
            }
        }
        catch {
            # Mark activity as failed
            if ($null -ne $script:activity) {
                $null = Complete-CmdletActivity -Activity $script:activity -Success $false -ErrorMessage $_.Exception.Message
                $script:activity = $null
            }
            throw
        }
    }

    end {
        # Cleanup activity if not already done
        if ($null -ne $script:activity) {
            $null = Complete-CmdletActivity -Activity $script:activity -Success $true
            $script:activity = $null
        }
    }
}
