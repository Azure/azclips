function Connect-CirrusAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $true)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $true)]
        [string] $TenantId
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "Connect-CirrusAccount" -Parameters $PSBoundParameters
    }

    process {
        try {
            $extensionName = "Login";
            $commandName = "Connect-CirrusAccount";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('SubscriptionId')) {
                $parameters.Add('SubscriptionId', [object]$SubscriptionId);
            }
            if ($PSBoundParameters.ContainsKey('TenantId')) {
                $parameters.Add('TenantId', [object]$TenantId);
            }
            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
            [Core.Abstractions.AuthenticationInfo]$output = [Core.Abstractions.AuthenticationInfo]([Core.Engine.Engine]::GetCommand($invocation).Process());
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