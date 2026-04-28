function Remove-CirrusConfig {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param (
        [Parameter(Mandatory = $true)]
        [string] $Key,

        [Parameter(Mandatory = $false)]
        [switch] $Force
    )

    begin {
        # Start telemetry activity
        $script:activity = Start-CmdletActivity -CmdletName "Remove-CirrusConfig" -Parameters $PSBoundParameters

        $engine = [Core.DependencyInjection.CirrusServiceProvider]::GetRequiredService([Core.Abstractions.IEngine])
    }

    process {
        try {
            if ($PSCmdlet.ShouldProcess("config key '$Key'", "delete")) {
                $extensionName = "Config"
                $commandName = "Remove-CirrusConfig"
                $parameters = [System.Collections.Generic.Dictionary[string, object]]::New()

                if ($PSBoundParameters.ContainsKey('Key')) {
                    $parameters.Add('Key', [object]$Key)
                }
                if ($PSBoundParameters.ContainsKey('Force')) {
                    $parameters.Add('Force', [object][bool]$Force)
                }

                $moduleName = $MyInvocation.MyCommand.ModuleName
                $moduleVersion = if ($MyInvocation.MyCommand.Version) { $MyInvocation.MyCommand.Version.ToString() } else { $null }
                $invocation = [Core.Abstractions.CommandInvocation]::New($extensionName, $commandName, $parameters, $false, $moduleName, $moduleVersion)
                Invoke-CirrusEngineCommand -Engine $engine -Invocation $invocation -PSCmdlet $PSCmdlet

                # Mark activity as successful
                if ($null -ne $script:activity) {
                    Complete-CmdletActivity -Activity $script:activity -Success $true
                    $script:activity = $null
                }
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
}
