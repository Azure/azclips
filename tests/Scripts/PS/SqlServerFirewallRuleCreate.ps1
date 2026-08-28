# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Create an Azure SQL server firewall rule - PowerShell
# Equivalent of: New-AzSqlServerFirewallRule
#
# Prereq: build the solution in Debug (the committed manifests reference
# artifacts/Debug), import Login FIRST, then Sql, then Connect-AzAccount.
#   dotnet build azclips.slnx --tl:off
#
# This script creates its own SQL server first, because a firewall rule is a child of a
# server.
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
# Firewall rule names only have to be unique within their server.
#
# Firewall rule create is a SYNCHRONOUS PUT (200/201, no polling headers), so the rule
# comes back on the first response - there is no provisioning state to wait for.
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

# Prereq: the parent server.
New-AzSqlServer `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -Name $srv `
    -Location $loc `
    -AdministratorLogin sqladmin `
    -AdministratorLoginPassword $env:AZCLIPS_SQL_ADMIN_PASSWORD | Out-Null

# Happy path: a single-address rule (start == end).
$rule = New-AzSqlServerFirewallRule `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -FirewallRuleName AllowMyIp `
    -StartIpAddress 203.0.113.5 `
    -EndIpAddress 203.0.113.5

Assert-NotNull  $rule                                             'single-IP create returns a firewall rule'
Assert-AreEqual 'AllowMyIp' $rule.Name                            'single-IP create echoes the requested name'
Assert-AreEqual 'Microsoft.Sql/servers/firewallRules' $rule.Type  'single-IP create returns the firewall rule resource type'
Assert-AreEqual "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.Sql/servers/$srv/firewallRules/AllowMyIp" $rule.Id 'single-IP create returns the full ARM id'
Assert-AreEqual '203.0.113.5' $rule.Properties.StartIpAddress     'single-IP create echoes -StartIpAddress'
Assert-AreEqual '203.0.113.5' $rule.Properties.EndIpAddress       'single-IP create echoes -EndIpAddress'

# Happy path: an inclusive range.
$rangeRule = New-AzSqlServerFirewallRule `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -FirewallRuleName AllowOffice `
    -StartIpAddress 203.0.113.5 `
    -EndIpAddress 203.0.113.10

Assert-NotNull  $rangeRule                                          'range create returns a firewall rule'
Assert-AreEqual 'AllowOffice' $rangeRule.Name                       'range create echoes the requested name'
Assert-AreEqual '203.0.113.5'  $rangeRule.Properties.StartIpAddress 'range create echoes the start of the range'
Assert-AreEqual '203.0.113.10' $rangeRule.Properties.EndIpAddress   'range create echoes the end of the range'

# Happy path: the second parameter set. -AllowAllAzureIPs supplies the well-known rule
# name and the 0.0.0.0-0.0.0.0 addresses, so none of the three may be passed here.
$azureRule = New-AzSqlServerFirewallRule `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -AllowAllAzureIPs

Assert-NotNull  $azureRule                                            '-AllowAllAzureIPs returns a firewall rule'
Assert-AreEqual 'AllowAllWindowsAzureIps' $azureRule.Name             '-AllowAllAzureIPs creates the well-known rule name'
Assert-AreEqual '0.0.0.0' $azureRule.Properties.StartIpAddress        '-AllowAllAzureIPs stores 0.0.0.0 as the start address'
Assert-AreEqual '0.0.0.0' $azureRule.Properties.EndIpAddress          '-AllowAllAzureIPs stores 0.0.0.0 as the end address'

# Idempotence: the PUT is a create-or-update, so re-running the first rule with a wider
# range must succeed and return the NEW range.
$updated = New-AzSqlServerFirewallRule `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -FirewallRuleName AllowMyIp `
    -StartIpAddress 203.0.113.5 `
    -EndIpAddress 203.0.113.20

Assert-NotNull  $updated                                        're-creating an existing rule returns a firewall rule'
Assert-AreEqual '203.0.113.20' $updated.Properties.EndIpAddress  'the re-created rule carries the updated end address'

# Happy path: -WhatIf must describe the operation without sending the PUT.
$whatIf = New-AzSqlServerFirewallRule `
    -SubscriptionId $sub `
    -ResourceGroupName $rg `
    -ServerName $srv `
    -FirewallRuleName AllowWhatIf `
    -StartIpAddress 203.0.113.30 `
    -EndIpAddress 203.0.113.30 `
    -WhatIf

Assert-AreEqual $null $whatIf '-WhatIf emits no firewall rule object'
# This module ships no Get-AzSqlServerFirewallRule yet, so the shipping Azure CLI is the
# read path - the same tool the cleanup step below already assumes is installed.
az sql server firewall-rule show --name AllowWhatIf --server $srv --resource-group $rg --subscription $sub 2>$null | Out-Null
Assert-AreEqual $false ($LASTEXITCODE -eq 0) '-WhatIf did not actually create the rule'

# Help surface: rendered from src/PS/Sql/help/New-AzSqlServerFirewallRule.md via the
# generated MAML.
$help = Get-Help New-AzSqlServerFirewallRule -Full
Assert-NotNull  $help.Synopsis                            'help exposes a synopsis'
Assert-AreEqual $true ($help.Examples.Example.Count -gt 0) 'help exposes at least one example'
foreach ($p in 'ResourceGroupName', 'ServerName', 'FirewallRuleName', 'StartIpAddress', 'EndIpAddress', 'AllowAllAzureIPs') {
    Assert-AreEqual $true ($help.Parameters.Parameter.Name -contains $p) "help documents -$p"
}
# Two parameter sets: the IP range set and the -AllowAllAzureIPs shortcut.
Assert-AreEqual 2 $help.syntax.syntaxItem.Count 'help renders both parameter sets'
# A firewall rule has no location of its own; advertising one would be a parity bug.
Assert-AreEqual $false ($help.Parameters.Parameter.Name -contains 'Location') 'the cmdlet does not advertise a -Location parameter'

# Negative case: a mandatory parameter is omitted (-StartIpAddress). Binding must fail
# rather than sending an incomplete request. -ErrorAction Stop turns the prompt into an
# error in a non-interactive run.
Assert-Throws {
    New-AzSqlServerFirewallRule `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName $srv `
        -FirewallRuleName AllowIncomplete `
        -EndIpAddress 203.0.113.5 `
        -ErrorAction Stop
} 'StartIpAddress' 'omitting mandatory -StartIpAddress fails binding'

# Negative case: the two parameter sets are mutually exclusive, so combining
# -AllowAllAzureIPs with an explicit range must be rejected by the binder.
Assert-Throws {
    New-AzSqlServerFirewallRule `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName $srv `
        -AllowAllAzureIPs `
        -StartIpAddress 203.0.113.5 `
        -EndIpAddress 203.0.113.5 `
        -ErrorAction Stop
} 'parameter set|ParameterBindingException|cannot be resolved' '-AllowAllAzureIPs cannot be combined with an explicit range'

# Negative case: a non-IPv4 address. There is deliberately no client-side validation, so
# this proves the service error reaches the user.
Assert-Throws {
    New-AzSqlServerFirewallRule `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName $srv `
        -FirewallRuleName AllowBadIp `
        -StartIpAddress not-an-ip `
        -EndIpAddress 203.0.113.5 `
        -ErrorAction Stop
} 'FirewallRuleNotIPv4Address|IPv4|400' 'a non-IPv4 start address surfaces the service validation error'

# Negative case: an end address lower than the start address.
Assert-Throws {
    New-AzSqlServerFirewallRule `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName $srv `
        -FirewallRuleName AllowInvertedRange `
        -StartIpAddress 203.0.113.10 `
        -EndIpAddress 203.0.113.5 `
        -ErrorAction Stop
} 'FirewallRuleInvalidRange|range|400' 'an inverted IP range surfaces the service validation error'

# Negative case: a server that does not exist.
Assert-Throws {
    New-AzSqlServerFirewallRule `
        -SubscriptionId $sub `
        -ResourceGroupName $rg `
        -ServerName "$srv-does-not-exist" `
        -FirewallRuleName AllowMyIp `
        -StartIpAddress 203.0.113.5 `
        -EndIpAddress 203.0.113.5 `
        -ErrorAction Stop
} 'ResourceNotFound|not found|NotFound|404' 'a missing parent server surfaces the failure as a terminating error'

# Cleanup. Removing a firewall rule is not implemented by this module yet; use the
# shipping Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes

if ($script:Failures -gt 0) {
    Write-Host "$script:Failures assertion(s) failed" -ForegroundColor Red
    exit 1
}

Write-Host 'All assertions passed' -ForegroundColor Green
