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
