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
#
# This script asserts on the responses instead of only printing them, so a silent
# behavior change fails the run. Exit code is non-zero when any assertion fails.

Import-Module ../../../src/PS/Login/Login.psd1
Import-Module ../../../src/PS/Sql/Sql.psd1

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$rg  = 'rg-sql-test'
$loc = 'southeastasia'
$srv = "azclips-sql-$(Get-Date -Format 'yyMMddHHmmss')"

# three tiny helpers instead of Pester - these scripts run standalone against
# live Azure and must not require a module that is not a repo dependency.
$script:Failures = 0

function Assert-AreEqual($expected, $actual, $because) {
    if ($expected -ne $actual) {
        $script:Failures++
        Write-Host "FAIL $because - expected '$expected', got '$actual'" -ForegroundColor Red
    }
    else {
        Write-Host "PASS $because" -ForegroundColor Green
    }
}

function Assert-NotNull($actual, $because) {
    Assert-AreEqual $true ($null -ne $actual) $because
}

function Assert-Throws([scriptblock] $operation, $expectedMessagePattern, $because) {
    try {
        & $operation | Out-Null
        $script:Failures++
        Write-Host "FAIL $because - expected a terminating error, none was thrown" -ForegroundColor Red
    }
    catch {
        if ($_.Exception.Message -notmatch $expectedMessagePattern) {
            $script:Failures++
            Write-Host "FAIL $because - error did not match '$expectedMessagePattern': $($_.Exception.Message)" -ForegroundColor Red
        }
        else {
            Write-Host "PASS $because" -ForegroundColor Green
        }
    }
}

Connect-AzAccount -SubscriptionId $sub

# Happy path: minimal create. This is a long-running operation - the PUT returns 202, the
# LRO polls Azure-AsyncOperation every 15s, and the terminal GET yields the server.
# ARM defaults Version to 12.0, MinimalTlsVersion to 1.2 and PublicNetworkAccess to
# Enabled when they are not supplied.
$server = New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name $srv `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD

Assert-NotNull  $server                                    'minimal create returns a server'
Assert-AreEqual $srv   $server.Name                        'minimal create echoes the requested name'
Assert-AreEqual $loc   $server.Location                    'minimal create echoes the requested location'
Assert-AreEqual 'Microsoft.Sql/servers' $server.Type       'minimal create returns the SQL server resource type'
Assert-AreEqual "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.Sql/servers/$srv" $server.Id 'minimal create returns the full ARM id'
Assert-AreEqual 'Ready' $server.Properties.State           'LRO ran to a Ready server rather than returning the 202 body'
Assert-AreEqual ('{0}.database{1}' -f $srv, '.windows.net') $server.Properties.FullyQualifiedDomainName 'minimal create returns the resolvable FQDN'
Assert-AreEqual 'sqladmin' $server.Properties.AdministratorLogin 'minimal create echoes the administrator login'
# The password is create-only input and must never come back on the response.
Assert-AreEqual $null  $server.Properties.AdministratorLoginPassword 'response does not echo the administrator password'
# ARM defaults, asserted so a change in the pinned api-version surfaces here.
Assert-AreEqual '12.0'    $server.Properties.Version            'ARM defaults Version to 12.0'
Assert-AreEqual '1.2'     $server.Properties.MinimalTlsVersion  'ARM defaults MinimalTlsVersion to 1.2'
Assert-AreEqual 'Enabled' $server.Properties.PublicNetworkAccess 'ARM defaults PublicNetworkAccess to Enabled'

# Happy path: every optional property supplied. Expect them echoed back in the response
# properties rather than the ARM defaults.
$serverB = New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name "$srv-b" `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
    -ServerVersion 12.0 `
    -MinimalTlsVersion 1.2 `
    -PublicNetworkAccess Disabled

Assert-NotNull  $serverB                                    'full create returns a server'
Assert-AreEqual "$srv-b" $serverB.Name                      'full create echoes the requested name'
Assert-AreEqual 'Ready'  $serverB.Properties.State          'full create ran the LRO to Ready'
Assert-AreEqual '12.0'   $serverB.Properties.Version        'full create echoes -ServerVersion'
Assert-AreEqual '1.2'    $serverB.Properties.MinimalTlsVersion 'full create echoes -MinimalTlsVersion'
# The one supplied value that differs from the ARM default - proves the parameter is wired
# through to the PUT body rather than the response merely looking correct by coincidence.
Assert-AreEqual 'Disabled' $serverB.Properties.PublicNetworkAccess 'full create echoes -PublicNetworkAccess Disabled'

# Happy path: -WhatIf must describe the operation without sending the PUT.
$whatIf = New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name "$srv-c" `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
    -WhatIf

Assert-AreEqual $null $whatIf '-WhatIf emits no server object'
# This module ships no Get-AzSqlServer yet, so the shipping Azure CLI is the read path -
# the same tool the cleanup step below already assumes is installed.
az sql server show --name "$srv-c" --resource-group $rg --subscription $sub 2>$null | Out-Null
Assert-AreEqual $false ($LASTEXITCODE -eq 0) '-WhatIf did not actually create the server'

# Help surface: rendered from src/PS/Sql/help/New-AzSqlServer.md via the generated MAML.
$help = Get-Help New-AzSqlServer -Full
Assert-NotNull  $help.Synopsis                        'help exposes a synopsis'
Assert-AreEqual $true ($help.Examples.Example.Count -gt 0) 'help exposes at least one example'
foreach ($p in 'ResourceGroupName', 'Name', 'Location', 'AdministratorLogin', 'AdministratorLoginPassword', 'ServerVersion', 'MinimalTlsVersion', 'PublicNetworkAccess') {
    Assert-AreEqual $true ($help.Parameters.Parameter.Name -contains $p) "help documents -$p"
}

# Negative case: a mandatory parameter is omitted (-Location). Binding must fail rather
# than sending an incomplete request. -ErrorAction Stop turns the prompt into an error in
# a non-interactive run.
Assert-Throws {
    New-AzSqlServer `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -Name "$srv-d" `
        -AdministratorLogin sqladmin `
        -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
        -ErrorAction Stop
} 'Location' 'omitting mandatory -Location fails binding'

# Negative case: a value outside the ValidateSet for -MinimalTlsVersion must be rejected
# at binding time, before any request is sent.
Assert-Throws {
    New-AzSqlServer `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -Name "$srv-e" `
        -Location $loc `
        -AdministratorLogin sqladmin `
        -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
        -MinimalTlsVersion 9.9 `
        -ErrorAction Stop
} 'ValidateSet|not belong to the set|9\.9' '-MinimalTlsVersion 9.9 is rejected by ValidateSet'

# Negative case: a server name that is already taken globally must surface the ARM
# failure (LRO terminal state 'Failed') as a terminating error rather than success.
Assert-Throws {
    New-AzSqlServer `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -Name $srv `
        -Location $loc `
        -AdministratorLogin sqladmin `
        -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD `
        -ErrorAction Stop
} 'already exists|NameAlreadyExists|Conflict|Failed' 'a duplicate server name surfaces the ARM failure as a terminating error'

# Cleanup. Removing a SQL server is not implemented by this module yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes

if ($script:Failures -gt 0) {
    Write-Host "$script:Failures assertion(s) failed" -ForegroundColor Red
    exit 1
}

Write-Host 'All assertions passed' -ForegroundColor Green
