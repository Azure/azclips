@{
    SchemaVersion = 1
    Modules = @(
        @{
            Names = @('Az.Accounts', 'Az.Login')
            Tests = @(
                @{ Name = 'disconnect-whatif'; Command = 'Disconnect-AzAccount'; Mode = 'WhatIf'; Parameters = @{} }
            )
        }
        @{
            Names = @('Az.Compute')
            Tests = @(
                @{
                    Name = 'remove-vm-whatif'
                    Command = 'Remove-AzVM'
                    Mode = 'WhatIf'
                    Parameters = @{
                        SubscriptionId = '00000000-0000-0000-0000-000000000000'
                        ResourceGroupName = 'azclips-smoke'
                        Name = 'azclips-smoke'
                    }
                }
            )
        }
        @{
            Names = @('Az.Config')
            Tests = @(
                @{ Name = 'config-keys'; Command = 'Get-AzConfigInfo'; Mode = 'ReadOnly'; Parameters = @{ KeysOnly = $true }; MinimumOutputCount = 1 }
            )
        }
        @{
            Names = @('Az.Resources')
            Tests = @(
                @{
                    Name = 'missing-profile-guard'
                    Command = 'Get-AzResourceGroup'
                    Mode = 'ReadOnly'
                    Parameters = @{
                        SubscriptionId = '00000000-0000-0000-0000-000000000000'
                        Profile = '{MissingProfile}'
                    }
                    # The engine rejects an unknown explicit profile before constructing its HTTP pipeline.
                    ExpectedError = @{ Id = 'System.ArgumentException'; Message = 'Profile ''{MissingProfile}'' does not exist.' }
                }
            )
        }
        @{
            Names = @('Az.Identity')
            Tests = @(
                @{
                    Name = 'remove-identity-whatif'
                    Command = 'Remove-AzUserAssignedIdentity'
                    Mode = 'WhatIf'
                    Parameters = @{
                        SubscriptionId = '00000000-0000-0000-0000-000000000000'
                        ResourceGroupName = 'azclips-smoke'
                        Name = 'azclips-smoke'
                    }
                }
            )
        }
        @{
            Names = @('Az.MsGraph')
            Tests = @(
                @{
                    Name = 'remove-application-whatif'
                    Command = 'Remove-AzMsGraphApplication'
                    Mode = 'WhatIf'
                    Parameters = @{ Id = '00000000-0000-0000-0000-000000000000' }
                }
            )
        }
        @{
            Names = @('Az')
            Tests = @(
                # The rollup exports no commands of its own. Exercise a pinned required module after importing Az.
                @{ Name = 'rollup-config-keys'; Command = 'Get-AzConfigInfo'; CommandModule = 'Az.Config'; Mode = 'ReadOnly'; Parameters = @{ KeysOnly = $true }; MinimumOutputCount = 1 }
            )
        }
    )
}
