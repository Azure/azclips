function New-ResourceGroup {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $true)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Location,

        [Parameter(Mandatory = $true)]
        [string] $AccessToken
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "New-ResourceGroup" -Parameters $PSBoundParameters
    }

    process {
        try {
            $extensionName = "Resources";
            $commandName = "New-ResourceGroup";
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
            if ($PSBoundParameters.ContainsKey('AccessToken')) {
                $parameters.Add('AccessToken', [object]$AccessToken);
            }
            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
            [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]$output = [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]([Core.Engine.Engine]::ProcessCommand([Core.Engine.Engine]::GetCommand($invocation)));
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