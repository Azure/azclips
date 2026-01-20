function Get-ResourceGroup {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $false)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $false)]
        [string] $Name
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "Get-ResourceGroup" -Parameters $PSBoundParameters
    }

    process {
        try {
            $extensionName = "Resources";
            $commandName = "Get-ResourceGroup";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('SubscriptionId')) {
                $parameters.Add('SubscriptionId', [object]$SubscriptionId);
            }
            if ($PSBoundParameters.ContainsKey('Name')) {
                $parameters.Add('Name', [object]$Name);
            }
            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
            $output = [Core.Engine.Engine]::GetCommand($invocation).Process();
            if ($output -is [System.Collections.IEnumerable]) {
                foreach ($item in $output) {
                    $rg = [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]$item;
                    Write-Output $rg;
                }
            }
            else {
                $rg = [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]$output;
                Write-Output $rg;
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
