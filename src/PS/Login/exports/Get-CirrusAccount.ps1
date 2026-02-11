function Get-CirrusAccount {
    [CmdletBinding()]
    param()

    begin {
        $script:activity = Start-CmdletActivity -CmdletName "Get-CirrusAccount" -Parameters $PSBoundParameters

        $outputDelegate = [Core.Abstractions.Runtime.CommandOutputDelegate]{
            param([object]$value, [System.Threading.CancellationToken]$ct)
            $PSCmdlet.WriteObject($value)
            return [System.Threading.Tasks.Task]::CompletedTask
        }
    }

    process {
        try {
            $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

            $script:CirrusHost.InvokeByName(
                $script:ExtensionAssemblyPath,
                'Login',
                'Get-CirrusAccount',
                $parameters,
                $outputDelegate,
                [System.Threading.CancellationToken]::None);

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
