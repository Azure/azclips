function Get-CirrusAccount {
    [CmdletBinding(ConfirmImpact = 'Medium')]
    param()

    begin {

    }

    process {
        $extensionName = "Login";
        $commandName = "Get-CirrusAccount";
        $parameters = [System.Collections.Generic.Dictionary[string, object]]::New();
        $invocation = [Core.Implementation.PSCommandInvocation]::New($extensionName, $commandName, $parameters);
        [Core.Abstractions.AuthContext]$output = [Core.Abstractions.AuthContext]([Core.Engine.Engine]::GetCommand($invocation).process());
        Write-Output $output;
    }

    end {

    }
}
