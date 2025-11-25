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

    }

    process {
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
        [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]$output = [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]([Core.Engine.Engine]::GetCommand($invocation).process());
        Write-Output $output;
    }

    end {

    }
}