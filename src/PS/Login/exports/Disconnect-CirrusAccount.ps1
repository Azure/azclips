function Disconnect-CirrusAccount {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
    )

    begin {

    }

    process {
        $extensionName = "Login";
        $commandName = "Disconnect-CirrusAccount";
        $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();

        $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
        [Core.Engine.Engine]::GetCommand($invocation).process();
    }

    end {

    }
}
