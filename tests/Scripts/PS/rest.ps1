# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

# Universal REST passthrough - PowerShell
# Equivalent of: Invoke-AzRestMethod -Method GET -Uri <url>
#
# Prereq: build the solution, import the staged Accounts + Resources modules, then Connect-AzAccount.

Import-Module ../../../artifacts/Debug/PS/Accounts/Accounts.psd1
Import-Module ../../../artifacts/Debug/PS/Resources/Resources.psd1

# Happy path: relative ARM path. {subscriptionId} is substituted from the auth context,
# the ARM endpoint is prepended, and the request is authenticated automatically.
# Expect StatusCode 200 + a parsed JSON body of resource groups.
Invoke-AzRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

# Happy path: absolute URL passes through unchanged (still ARM-authenticated).
Invoke-AzRestMethod `
    -Method GET `
    -Uri "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups/azclips-test-group-1?api-version=2021-04-01"

# Happy path with headers supplied as a hashtable.
Invoke-AzRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    -Headers @{ Accept = 'application/json' }

# Happy path: ByParameters mode collection GET. Instead of a full URL, supply the ARM resource-id
# pieces and an api-version; the cmdlet builds the subscription-wide provider collection URL
# https://management.azure.com/subscriptions/{sub}/providers/Microsoft.Storage/storageAccounts for
# you. This lists all Storage accounts in the subscription (an empty "value" array is still a 200).
Invoke-AzRestMethod `
    -ResourceProviderName Microsoft.Storage `
    -ResourceType storageAccounts `
    -ApiVersion 2021-09-01

# Happy path: ByParameters mode single-resource GET. Supplying -ResourceGroupName builds the
# /subscriptions/{sub}/resourceGroups/{name} segment, which is the correct ARM path for a single
# resource group; this fetches the azclips-test-group-1 resource group (Expect 200).
Invoke-AzRestMethod `
    -ResourceGroupName azclips-test-group-1 `
    -ApiVersion 2021-04-01

# Happy path: extra query-string parameters via -UriParameters (hashtable or KEY=VALUE string[]).
# Here $top limits the number of resource groups returned. Works in both URI and ByParameters modes.
Invoke-AzRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    -UriParameters @{ '$top' = '3' }

# Each negative case below EXPECTS a terminating error. To keep this manual smoke script running
# end-to-end, every negative invocation is wrapped in its own try/catch: catching the error is the
# expected outcome (printed as PASS), while reaching the line after the call without throwing is a
# FAIL. The happy-path cases above are intentionally left un-wrapped so real failures still surface.

# Negative case: an unsupported HTTP method must be rejected (ValidateSet) with a clear message.
try {
    Invoke-AzRestMethod `
        -Method FOO `
        -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"
    Write-Host "FAIL: expected an error but call succeeded (unsupported -Method FOO)"
} catch {
    Write-Host "PASS (expected failure): $($_.Exception.Message)"
}

# Negative case: the URI form and the piece-wise ByParameters parameters are mutually exclusive;
# supplying both must be rejected before any request is sent.
try {
    Invoke-AzRestMethod `
        -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
        -ResourceProviderName Microsoft.Resources `
        -ApiVersion 2021-04-01
    Write-Host "FAIL: expected an error but call succeeded (URI + ByParameters mutual exclusion)"
} catch {
    Write-Host "PASS (expected failure): $($_.Exception.Message)"
}

# Negative case: ByParameters mode requires an api-version; omitting it must be rejected.
try {
    Invoke-AzRestMethod `
        -ResourceProviderName Microsoft.Resources `
        -ResourceType resourceGroups
    Write-Host "FAIL: expected an error but call succeeded (ByParameters missing -ApiVersion)"
} catch {
    Write-Host "PASS (expected failure): $($_.Exception.Message)"
}

# Negative case: an HTTP error (>= 400) must surface the body AND raise a terminating error so
# scripts can detect it via $? / try-catch. This GET targets a resource group that does not exist
# and is expected to return 404; the full RestResponse (status/headers/body) is still shown.
try {
    Invoke-AzRestMethod `
        -Method GET `
        -Uri "/subscriptions/{subscriptionId}/resourcegroups/this-rg-does-not-exist-404?api-version=2021-04-01"
    Write-Host "FAIL: expected an error but call succeeded (404 HTTP error)"
} catch {
    Write-Host "PASS (expected failure): $($_.Exception.Message)"
}

# Negative case: an absolute URL pointing at a non-ARM host must be REJECTED before any request is
# sent, so the ARM bearer token is never leaked to an arbitrary destination (token exfiltration).
try {
    Invoke-AzRestMethod `
        -Method GET `
        -Uri "https://example.com/steal-the-token"
    Write-Host "FAIL: expected an error but call succeeded (non-ARM host rejection)"
} catch {
    Write-Host "PASS (expected failure): $($_.Exception.Message)"
}

# Negative case: an absolute http:// (non-https) ARM URL must be rejected.
try {
    Invoke-AzRestMethod `
        -Method GET `
        -Uri "http://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"
    Write-Host "FAIL: expected an error but call succeeded (non-https ARM URL rejection)"
} catch {
    Write-Host "PASS (expected failure): $($_.Exception.Message)"
}

