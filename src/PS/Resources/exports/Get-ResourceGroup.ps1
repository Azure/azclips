function Get-ResourceGroup {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $false)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $false)]
        [string] $Name
    )

    begin {

    }

    process {
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
        } else {
            $rg = [Microsoft.Azure.Cirrus.Extensions.Resources.Models.ResourceGroup]$output;
            Write-Output $rg;
        }
    }

    end {

    }
}
