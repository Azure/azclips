# Resource group scope deployment what-if - PowerShell (New-ResourceGroupDeploymentWhatIf)
# Equivalent of: New-AzResourceGroupDeploymentWhatIf / Get-AzResourceGroupDeploymentWhatIfResult
#
# What-if is read-only: it predicts the changes a deployment would make WITHOUT
# creating or modifying any resources.
#
# Shared test context:
#   SubscriptionId: 282bb33f-067d-4179-a7a4-b833c13af735
#   ResourceGroupName: cirrus-test-group-1 (region: australiaeast)
# The resource-group-scoped what-if inherits the resource group's region,
# so no location parameter is required here.

# Happy path: predict the changes for a local .bicep storage template.
# Expected: the operation polls the Location LRO to completion and returns a
# WhatIfOperationResult with status Succeeded and a "Create" change for the storage account.
# No real storage account is created.
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-whatif-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -Parameter "storageAccountName=cirruswhatif$stamp" `
    -Mode Incremental `
    -Debug

# ResourceIdOnly result-format variant: a leaner change list (no full payloads).
$stamp = (Get-Date -Format 'yyMMddHHmmss')
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name "cirrus-test-whatif-idonly-$stamp" `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -Parameter "storageAccountName=cirruswhatifid$stamp" `
    -ResultFormat ResourceIdOnly `
    -Mode Incremental `
    -Debug

# Negative case: invalid -ResultFormat value (Foo) must be rejected.
# Expected: the command fails fast with a clear validation message listing the allowed
# values (ResourceIdOnly, FullResourcePayloads) and does NOT issue a what-if.
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name cirrus-test-whatif-rf-negative `
    -TemplateFile ../Bicep/sample-storage.bicep `
    -ResultFormat Foo `
    -Mode Incremental `
    -Debug

# Negative case (server-side error surfaced): a malformed .bicep fails compilation locally
# (non-zero exit -> InvalidOperationException) before any request is made. No what-if is run.
New-ResourceGroupDeploymentWhatIf `
    -SubscriptionId 282bb33f-067d-4179-a7a4-b833c13af735 `
    -ResourceGroupName cirrus-test-group-1 `
    -Name cirrus-test-whatif-bicep-negative `
    -TemplateFile ../Bicep/malformed.bicep `
    -Mode Incremental `
    -Debug
