# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Create an Azure SQL logical server - PowerShell
# Equivalent of: New-AzSqlServer
#
# Run:
#   Invoke-Pester -Path tests/Scripts/PS/SqlServerCreate.Tests.ps1 -Output Detailed
#
# Prereq: build the solution in Debug (the committed manifests reference
# artifacts/Debug), import Login FIRST, then Sql, then Connect-AzAccount.
#   dotnet build azclips.slnx --tl:off
#
# The admin password is read from $env:AZCLIPS_SQL_ADMIN_PASSWORD so no credential is
# committed; set it before running:
#   $env:AZCLIPS_SQL_ADMIN_PASSWORD = '<a password meeting the Azure SQL policy>'
#
# Shared test context:
#   subscription: f758ac53-3e63-4317-a956-0997793808d7
#   resource group: rg-sql-test
#   region: southeastasia
#
# Region note: 'eastus' returned RegionDoesNotAllowProvisioning during verification -
# the same failure reproduces with the shipping Azure CLI, so it is a capacity
# condition, not a client bug. Pick another region when it recurs.
#
# Server names are GLOBALLY unique and lowercase; the timestamp suffix avoids collisions.
#
# These are live tests: they create real servers. Cleanup is a manual step at the bottom.

BeforeAll {
    if (-not $env:AZCLIPS_SQL_ADMIN_PASSWORD) {
        # ponytail: Write-Host, because Pester 6 replaces a throw from a top-level BeforeAll
        # with its own break/continue message and swallows this one (pester/Pester#2669).
        # Drop the Write-Host once that is fixed.
        Write-Host 'Set $env:AZCLIPS_SQL_ADMIN_PASSWORD before running; without it the create request sends a null password.' -ForegroundColor Red
        throw 'AZCLIPS_SQL_ADMIN_PASSWORD is not set.'
    }

    Import-Module $PSScriptRoot/../../../src/PS/Login/Login.psd1
    Import-Module $PSScriptRoot/../../../src/PS/Sql/Sql.psd1

    $sub = 'f758ac53-3e63-4317-a956-0997793808d7'
    $rg  = 'rg-sql-test'
    $loc = 'southeastasia'
    $srv = "azclips-sql-$(Get-Date -Format 'yyMMddHHmmss')"
    $adminPassword = $env:AZCLIPS_SQL_ADMIN_PASSWORD

    Connect-AzAccount -SubscriptionId $sub
}

Describe 'New-AzSqlServer' {
    # Ordered by design, as the generated azure-powershell suites are: the duplicate-name
    # case below depends on the minimal create having run.

    It 'CreateExpanded' {
        # Minimal create. This is a long-running operation - the PUT returns 202, the LRO
        # polls Azure-AsyncOperation every 15s, and the terminal GET yields the server.
        # ARM defaults Version to 12.0, MinimalTlsVersion to 1.2 and PublicNetworkAccess
        # to Enabled when they are not supplied.
        $server = New-AzSqlServer `
            -SubscriptionId $sub `
            -ResourceGroupName $rg `
            -Name $srv `
            -Location $loc `
            -AdministratorLogin sqladmin `
            -AdministratorLoginPassword $adminPassword

        $server | Should -Not -BeNullOrEmpty
        $server.Name | Should -Be $srv
        $server.Location | Should -Be $loc
        $server.Type | Should -Be 'Microsoft.Sql/servers'
        $server.Id | Should -Be "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.Sql/servers/$srv"
        # Ready proves the LRO ran to completion rather than returning the 202 body.
        $server.Properties.State | Should -Be 'Ready'
        $server.Properties.FullyQualifiedDomainName | Should -Be ('{0}.database{1}' -f $srv, '.windows.net')
        $server.Properties.AdministratorLogin | Should -Be 'sqladmin'
        # The password is create-only input and must never come back on the response.
        $server.Properties.AdministratorLoginPassword | Should -BeNullOrEmpty
        # ARM defaults, asserted so a change in the pinned api-version surfaces here.
        $server.Properties.Version | Should -Be '12.0'
        $server.Properties.MinimalTlsVersion | Should -Be '1.2'
        $server.Properties.PublicNetworkAccess | Should -Be 'Enabled'
    }

    It 'CreateExpanded with every optional property' {
        # Expect the supplied values echoed back rather than the ARM defaults.
        $server = New-AzSqlServer `
            -SubscriptionId $sub `
            -ResourceGroupName $rg `
            -Name "$srv-b" `
            -Location $loc `
            -AdministratorLogin sqladmin `
            -AdministratorLoginPassword $adminPassword `
            -ServerVersion 12.0 `
            -MinimalTlsVersion 1.2 `
            -PublicNetworkAccess Disabled

        $server | Should -Not -BeNullOrEmpty
        $server.Name | Should -Be "$srv-b"
        $server.Properties.State | Should -Be 'Ready'
        $server.Properties.Version | Should -Be '12.0'
        $server.Properties.MinimalTlsVersion | Should -Be '1.2'
        # The one supplied value that differs from the ARM default - proves the parameter
        # is wired through to the PUT body rather than the response merely looking correct
        # by coincidence.
        $server.Properties.PublicNetworkAccess | Should -Be 'Disabled'
    }

    It 'WhatIf describes the operation without sending the PUT' {
        $whatIf = New-AzSqlServer `
            -SubscriptionId $sub `
            -ResourceGroupName $rg `
            -Name "$srv-c" `
            -Location $loc `
            -AdministratorLogin sqladmin `
            -AdministratorLoginPassword $adminPassword `
            -WhatIf

        $whatIf | Should -BeNullOrEmpty

        # This module ships no Get-AzSqlServer yet, so the shipping Azure CLI is the read
        # path - the same tool the cleanup step below already assumes is installed.
        az sql server show --name "$srv-c" --resource-group $rg --subscription $sub 2>$null | Out-Null
        $LASTEXITCODE | Should -Not -Be 0
    }

    It 'exposes help rendered from src/PS/Sql/help/New-AzSqlServer.md' {
        $help = Get-Help New-AzSqlServer -Full

        $help.Synopsis | Should -Not -BeNullOrEmpty
        $help.Examples.Example.Count | Should -BeGreaterThan 0
        foreach ($p in 'ResourceGroupName', 'Name', 'Location', 'AdministratorLogin', 'AdministratorLoginPassword', 'ServerVersion', 'MinimalTlsVersion', 'PublicNetworkAccess') {
            $help.Parameters.Parameter.Name | Should -Contain $p
        }
    }

    It 'rejects a missing mandatory -Location at binding time' {
        # Binding must fail rather than sending an incomplete request. -ErrorAction Stop
        # turns the prompt into an error in a non-interactive run.
        $err = {
            New-AzSqlServer `
                -SubscriptionId $sub `
                -ResourceGroupName $rg `
                -Name "$srv-d" `
                -AdministratorLogin sqladmin `
                -AdministratorLoginPassword $adminPassword `
                -ErrorAction Stop
        } | Should -Throw -PassThru

        $err.Exception.Message | Should -Match 'Location'
    }

    It 'rejects -MinimalTlsVersion 9.9 by ValidateSet' {
        # Rejected at binding time, before any request is sent.
        $err = {
            New-AzSqlServer `
                -SubscriptionId $sub `
                -ResourceGroupName $rg `
                -Name "$srv-e" `
                -Location $loc `
                -AdministratorLogin sqladmin `
                -AdministratorLoginPassword $adminPassword `
                -MinimalTlsVersion 9.9 `
                -ErrorAction Stop
        } | Should -Throw -PassThru

        $err.Exception.Message | Should -Match 'ValidateSet|not belong to the set|9\.9'
    }

    It 'surfaces a duplicate server name as a terminating error' {
        # A name already taken globally must surface the ARM failure (LRO terminal state
        # 'Failed') rather than succeeding.
        $err = {
            New-AzSqlServer `
                -SubscriptionId $sub `
                -ResourceGroupName $rg `
                -Name $srv `
                -Location $loc `
                -AdministratorLogin sqladmin `
                -AdministratorLoginPassword $adminPassword `
                -ErrorAction Stop
        } | Should -Throw -PassThru

        $err.Exception.Message | Should -Match 'already exists|NameAlreadyExists|Conflict|Failed'
    }
}

# Cleanup. Removing a SQL server is not implemented by this module yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes
