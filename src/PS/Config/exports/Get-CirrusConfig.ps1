function Get-CirrusConfig {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string] $Name
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Get-CirrusConfig" -Parameters $PSBoundParameters

        $engine = [Core.Engine.Bootstrapper]::ServiceProviderAsync.GetAwaiter().GetResult().GetService([Core.Engine.EngineFactory]).GetEngine([Core.Abstractions.ClientType]::PowerShell);
        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $extensionName = "Config";
            $commandName = "Get-CirrusConfig";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('Name')) {
                $parameters.Add('Name', [object]$Name);
            }
            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
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
