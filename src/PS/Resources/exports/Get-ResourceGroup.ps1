function Get-ResourceGroup {
    [CmdletBinding()]
    param (
        [Parameter(ParameterSetName='Get', Mandatory)]
        [Parameter(ParameterSetName='List', Mandatory)]
        [string] $SubscriptionId,

        [Parameter(ParameterSetName='Get', Mandatory)]
        [string] $Name
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "Get-ResourceGroup" -Parameters $PSBoundParameters
        
        $engine = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            if ($value -is [System.Collections.IEnumerable]) {
                $PSCmdlet.WriteObject($value, $true)
            } else {
                $PSCmdlet.WriteObject($value)
            }
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Resources";
            $commandName = "Get-ResourceGroup_$($PSCmdlet.ParameterSetName)";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('SubscriptionId')) {
                $parameters.Add('SubscriptionId', [object]$SubscriptionId);
            }
            if ($PSBoundParameters.ContainsKey('Name')) {
                $parameters.Add('Name', [object]$Name);
            }
            $moduleName = $MyInvocation.MyCommand.ModuleName
            $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
            $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, [Core.Abstractions.ClientType]::PowerShell, $false, $moduleName, $moduleVersion)
            $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None);

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

