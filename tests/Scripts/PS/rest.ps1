# Universal REST passthrough - PowerShell
# Equivalent of: Invoke-AzRestMethod -Method GET -Uri <url>
#
# Prereq: import Login + Rest modules, then Connect-CirrusAccount.

Import-Module ../../../src/PS/Login/Login.psd1
Import-Module ../../../src/PS/Rest/Rest.psd1

# Happy path: relative ARM path. {subscriptionId} is substituted from the auth context,
# the ARM endpoint is prepended, and the request is authenticated automatically.
# Expect StatusCode 200 + a parsed JSON body of resource groups.
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

# Happy path: absolute URL passes through unchanged (still ARM-authenticated).
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups/cirrus-test-group-1?api-version=2021-04-01"

# Happy path with headers supplied as a hashtable.
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    -Headers @{ Accept = 'application/json' }

# Happy path: ByParameters mode collection GET. Instead of a full URL, supply the ARM resource-id
# pieces and an api-version; the cmdlet builds the subscription-wide provider collection URL
# https://management.azure.com/subscriptions/{sub}/providers/Microsoft.Storage/storageAccounts for
# you. This lists all Storage accounts in the subscription (an empty "value" array is still a 200).
Invoke-CirrusRestMethod `
    -ResourceProviderName Microsoft.Storage `
    -ResourceType storageAccounts `
    -ApiVersion 2021-09-01

# Happy path: ByParameters mode single-resource GET. Supplying -ResourceGroupName builds the
# /subscriptions/{sub}/resourceGroups/{name} segment, which is the correct ARM path for a single
# resource group; this fetches the cirrus-test-group-1 resource group (Expect 200).
Invoke-CirrusRestMethod `
    -ResourceGroupName cirrus-test-group-1 `
    -ApiVersion 2021-04-01

# Happy path: extra query-string parameters via -UriParameters (hashtable or KEY=VALUE string[]).
# Here $top limits the number of resource groups returned. Works in both URI and ByParameters modes.
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    -UriParameters @{ '$top' = '3' }

# Negative case: an unsupported HTTP method must be rejected (ValidateSet) with a clear message.
Invoke-CirrusRestMethod `
    -Method FOO `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

# Negative case: the URI form and the piece-wise ByParameters parameters are mutually exclusive;
# supplying both must be rejected before any request is sent.
Invoke-CirrusRestMethod `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    -ResourceProviderName Microsoft.Resources `
    -ApiVersion 2021-04-01

# Negative case: ByParameters mode requires an api-version; omitting it must be rejected.
Invoke-CirrusRestMethod `
    -ResourceProviderName Microsoft.Resources `
    -ResourceType resourceGroups

# Negative case: an HTTP error (>= 400) must surface the body AND raise a terminating error so
# scripts can detect it via $? / try-catch. This GET targets a resource group that does not exist
# and is expected to return 404; the full RestResponse (status/headers/body) is still shown.
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups/this-rg-does-not-exist-404?api-version=2021-04-01"
# After the call, $? should be $false (terminating error) — surface it for the test harness.
Write-Host "After 404 case, `$? = $?"

# Negative case: an absolute URL pointing at a non-ARM host must be REJECTED before any request is
# sent, so the ARM bearer token is never leaked to an arbitrary destination (token exfiltration).
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "https://example.com/steal-the-token"

# Negative case: an absolute http:// (non-https) ARM URL must be rejected.
Invoke-CirrusRestMethod `
    -Method GET `
    -Uri "http://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"
