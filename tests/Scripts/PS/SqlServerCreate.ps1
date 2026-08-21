# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Create an Azure SQL logical server - PowerShell
# Equivalent of: New-AzSqlServer
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

Import-Module ../../../src/PS/Login/Login.psd1
Import-Module ../../../src/PS/Sql/Sql.psd1

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$rg  = 'rg-sql-test'
$loc = 'southeastasia'
$srv = "azclips-sql-$(Get-Date -Format 'yyMMddHHmmss')"

Connect-AzAccount -SubscriptionId $sub

# Happy path: minimal create. This is a long-running operation - expect the PUT to
# return 202, the LRO to poll Azure-AsyncOperation every 15s, and a SqlServer object
# with State 'Ready' and a FullyQualifiedDomainName of <name>.database.windows.net.
# ARM defaults Version to 12.0, MinimalTlsVersion to 1.2 and PublicNetworkAccess to
# Enabled when they are not supplied.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name $srv `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD

# Happy path: every optional property supplied. Expect them echoed back in the response
# properties rather than the ARM defaults.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name "$srv-b" `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
    -ServerVersion 12.0 `
    -MinimalTlsVersion 1.2 `
    -PublicNetworkAccess Disabled

# Happy path: -WhatIf must describe the operation without sending the PUT.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name "$srv-c" `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
    -WhatIf

# Help surface: rendered from src/PS/Sql/help/New-AzSqlServer.md via the generated MAML.
Get-Help New-AzSqlServer -Full

# Negative case: a mandatory parameter is omitted (-Location). PowerShell must prompt
# or fail binding rather than sending an incomplete request.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name "$srv-d" `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD

# Negative case: a value outside the ValidateSet for -MinimalTlsVersion must be rejected
# at binding time, before any request is sent.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name "$srv-e" `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
    -MinimalTlsVersion 9.9

# Negative case: a server name that is already taken globally must surface the ARM
# failure (LRO terminal state 'Failed') as a terminating error rather than success.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name $srv `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD

# Cleanup. Removing a SQL server is not implemented by this module yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes
