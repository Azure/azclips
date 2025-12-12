function Connect-CirrusAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $true)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $true)]
        [string] $TenantId,

        [switch] $UseDeviceCode
    )

    begin {

    }

    process {
        $extensionName = "Login";
        $commandName = "Connect-CirrusAccount";
        $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

        if ($PSBoundParameters.ContainsKey('SubscriptionId')) {
            $parameters.Add('SubscriptionId', [object]$SubscriptionId);
        }
        if ($PSBoundParameters.ContainsKey('TenantId')) {
            $parameters.Add('TenantId', [object]$TenantId);
        }
        if ($PSBoundParameters.ContainsKey('UseDeviceCode')) {
            $parameters.Add('UseDeviceCode', $UseDeviceCode);
        }
        $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
        [Core.Abstractions.AuthenticationInfo]$output = [Core.Abstractions.AuthenticationInfo]([Core.Engine.Engine]::GetCommand($invocation).process());
        Write-Output $output;
    }

    end {

    }
}