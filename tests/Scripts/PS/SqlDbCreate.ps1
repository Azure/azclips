# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Create an Azure SQL database - PowerShell
# Equivalent of: New-AzSqlDatabase
#
# Prereq: build the solution in Debug (the committed manifests reference
# artifacts/Debug), import Login FIRST, then Sql, then Connect-AzAccount.
#   dotnet build azclips.slnx --tl:off
#
# This script creates its own SQL server first, because a database inherits its region
# from its server and there is no -Location parameter to fall back on.
#
# The admin password for that server is read from $env:AZCLIPS_SQL_ADMIN_PASSWORD so no
# credential is committed; set it before running:
#   $env:AZCLIPS_SQL_ADMIN_PASSWORD = '<a password meeting the Azure SQL policy>'
#
# Shared test context:
#   subscription: f758ac53-3e63-4317-a956-0997793808d7
#   resource group: rg-sql-test
#   region: southeastasia
#
# Server names are GLOBALLY unique and lowercase; the timestamp suffix avoids collisions.
# Database names only have to be unique within their server.
#
# This script asserts on the responses instead of only printing them, so a silent
# behavior change fails the run. Exit code is non-zero when any assertion fails.

Import-Module ../../../src/PS/Login/Login.psd1
Import-Module ../../../src/PS/Sql/Sql.psd1

$sub = 'f758ac53-3e63-4317-a956-0997793808d7'
$rg  = 'rg-sql-test'
$loc = 'southeastasia'
$srv = "azclips-sql-$(Get-Date -Format 'yyMMddHHmmss')"
$db  = 'mydb'

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

# Prereq: the parent server.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name $srv `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD | Out-Null

# Happy path: minimal create. Two calls under the hood - a GET on the server to resolve
# the location, then a long-running PUT on the database.
$database = New-AzSqlDatabase `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -DatabaseName $db

Assert-NotNull  $database                                  'minimal create returns a database'
Assert-AreEqual $db $database.Name                         'minimal create echoes the requested name'
Assert-AreEqual 'Microsoft.Sql/servers/databases' $database.Type 'minimal create returns the database resource type'
Assert-AreEqual "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.Sql/servers/$srv/databases/$db" $database.Id 'minimal create returns the full ARM id'
# The decisive one for the orchestration: no location was supplied, so this only passes
# if the server GET ran and its location reached the PUT body.
Assert-AreEqual $loc $database.Location                    'the database inherited its location from the server'
# 'Online' only appears after the LRO reached a terminal state, so this fails if the
# cmdlet ever returns the 202 body instead of polling to completion.
Assert-AreEqual 'Online' $database.Properties.Status       'LRO ran to an Online database rather than returning the 202 body'

# Happy path: an explicit DTU SKU plus collation and size cap, echoed back instead of the
# ARM defaults.
$databaseB = New-AzSqlDatabase `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -DatabaseName "$db-b" `
    -Edition Standard `
    -RequestedServiceObjectiveName S0 `
    -CollationName SQL_Latin1_General_CP1_CI_AS `
    -MaxSizeBytes 268435456000

Assert-NotNull  $databaseB                                 'full create returns a database'
Assert-AreEqual "$db-b"   $databaseB.Name                  'full create echoes the requested name'
Assert-AreEqual 'Standard' $databaseB.Sku.Tier             '-Edition maps onto sku.tier'
Assert-AreEqual 'S0'      $databaseB.Sku.Name              '-RequestedServiceObjectiveName maps onto sku.name'
Assert-AreEqual 'SQL_Latin1_General_CP1_CI_AS' $databaseB.Properties.Collation 'full create echoes -CollationName'
Assert-AreEqual 268435456000 $databaseB.Properties.MaxSizeBytes 'full create echoes -MaxSizeBytes'
Assert-AreEqual 'Online'  $databaseB.Properties.Status     'full create ran the LRO to Online'

# Happy path: vCore SKU. -Family and -Capacity are the two parameters that only make
# sense together, so they are exercised as a pair.
$databaseC = New-AzSqlDatabase `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -DatabaseName "$db-c" `
    -Edition GeneralPurpose `
    -Family Gen5 `
    -Capacity 2

Assert-NotNull  $databaseC                                    'vCore create returns a database'
Assert-AreEqual 'GeneralPurpose' $databaseC.Sku.Tier          'vCore create echoes -Edition'
Assert-AreEqual 'Gen5' $databaseC.Sku.Family                  'vCore create echoes -Family'
Assert-AreEqual 2      $databaseC.Sku.Capacity                'vCore create echoes -Capacity'
Assert-AreEqual 'Online' $databaseC.Properties.Status         'vCore create ran the LRO to Online'

# Happy path: -WhatIf must describe the operation without sending the PUT.
$whatIf = New-AzSqlDatabase `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -DatabaseName "$db-d" `
    -WhatIf

Assert-AreEqual $null $whatIf '-WhatIf emits no database object'
# This module ships no Get-AzSqlDatabase yet, so the shipping Azure CLI is the read path -
# the same tool the cleanup step below already assumes is installed.
az sql db show --name "$db-d" --server $srv --resource-group $rg --subscription $sub 2>$null | Out-Null
Assert-AreEqual $false ($LASTEXITCODE -eq 0) '-WhatIf did not actually create the database'

# Help surface: rendered from src/PS/Sql/help/New-AzSqlDatabase.md via the generated MAML.
$help = Get-Help New-AzSqlDatabase -Full
Assert-NotNull  $help.Synopsis                        'help exposes a synopsis'
Assert-AreEqual $true ($help.Examples.Example.Count -gt 0) 'help exposes at least one example'
foreach ($p in 'ResourceGroupName', 'ServerName', 'DatabaseName', 'Edition', 'RequestedServiceObjectiveName', 'Family', 'Capacity', 'CollationName', 'MaxSizeBytes', 'ElasticPoolName', 'ZoneRedundant') {
    Assert-AreEqual $true ($help.Parameters.Parameter.Name -contains $p) "help documents -$p"
}
# A database has no location of its own; advertising one would be a parity bug.
Assert-AreEqual $false ($help.Parameters.Parameter.Name -contains 'Location') 'the cmdlet does not advertise a -Location parameter'

# Negative case: a mandatory parameter is omitted (-ServerName). Binding must fail rather
# than sending an incomplete request. -ErrorAction Stop turns the prompt into an error in
# a non-interactive run.
Assert-Throws {
    New-AzSqlDatabase `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -DatabaseName "$db-e" `
        -ErrorAction Stop
} 'ServerName' 'omitting mandatory -ServerName fails binding'

# Negative case: -Capacity is typed as an integer, so a non-numeric value must be
# rejected at binding time, before any request is sent.
Assert-Throws {
    New-AzSqlDatabase `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName $srv `
        -DatabaseName "$db-f" `
        -Capacity notanumber `
        -ErrorAction Stop
} 'Capacity|Int32|cannot be converted' '-Capacity notanumber is rejected at binding time'

# Negative case: a server that does not exist. The location lookup is the FIRST call, so
# this must fail there and never issue the database PUT.
Assert-Throws {
    New-AzSqlDatabase `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName "$srv-does-not-exist" `
        -DatabaseName "$db-g" `
        -ErrorAction Stop
} 'ResourceNotFound|not found|NotFound|404' 'a missing parent server surfaces the lookup failure as a terminating error'

# Cleanup. Removing a SQL database is not implemented by this module yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes

if ($script:Failures -gt 0) {
    Write-Host "$script:Failures assertion(s) failed" -ForegroundColor Red
    exit 1
}

Write-Host 'All assertions passed' -ForegroundColor Green
