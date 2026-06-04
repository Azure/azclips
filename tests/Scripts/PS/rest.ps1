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
