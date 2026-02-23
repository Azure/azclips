function Set-CirrusConfig {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Set-CirrusConfig" -Parameters $PSBoundParameters

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
            $commandName = "Set-CirrusConfig";
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            if ($PSBoundParameters.ContainsKey('Name')) {
                $parameters.Add('Name', [object]$Name);
            }
            if ($PSBoundParameters.ContainsKey('Value')) {
                $parameters.Add('Value', [object]$Value);
            }
            $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);

            if ($PSCmdlet.ShouldProcess("Config '$Name'", "Set to '$Value'")) {
                $response = $engine.Process($invocation, $outputDelegate, [System.Threading.CancellationToken]::None);
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
