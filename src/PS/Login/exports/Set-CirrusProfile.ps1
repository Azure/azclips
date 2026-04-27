function Set-CirrusProfile {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $false)]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $false)]
        [string] $TenantId,

        [Parameter(Mandatory = $false)]
        [string] $Set,

        [Parameter(Mandatory = $false)]
        [string] $RemoveDefault
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Set-CirrusProfile" -Parameters $PSBoundParameters

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
            $commandName = "Set-CirrusProfile";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();
            $parameters.Add('Name', [object]$Name);

            if ($PSBoundParameters.ContainsKey('SubscriptionId')) {
                $parameters.Add('SubscriptionId', [object]$SubscriptionId);
            }
            if ($PSBoundParameters.ContainsKey('TenantId')) {
                $parameters.Add('TenantId', [object]$TenantId);
            }
            if ($PSBoundParameters.ContainsKey('Set')) {
                $parameters.Add('Set', [object]$Set);
            }
            if ($PSBoundParameters.ContainsKey('RemoveDefault')) {
                $parameters.Add('RemoveDefault', [object]$RemoveDefault);
            }

            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)

            if ($PSCmdlet.ShouldProcess("Profile '$Name'", "Modify")) {
                $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None);
            }

            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $true
                $script:activity = $null
            }
        }
        catch {
            if ($null -ne $script:activity) {
                Complete-CmdletActivity -Activity $script:activity -Success $false -ErrorMessage $_.Exception.Message
                $script:activity = $null
            }
            throw
        }
    }

    end {
        if ($null -ne $script:activity) {
            Complete-CmdletActivity -Activity $script:activity -Success $true
            $script:activity = $null
        }
    }
}
