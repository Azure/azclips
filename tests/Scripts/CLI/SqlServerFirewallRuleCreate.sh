# Create an Azure SQL server firewall rule - CLI (az sql server firewall-rule create)
# Equivalent of: az sql server firewall-rule create
#
# Prereq: az login, and a SQL server to hold the rules. This script creates its own
# server first, because a firewall rule is a child of a server.
#
# The admin password for that server is read from AZCLIPS_SQL_ADMIN_PASSWORD so no
# credential is committed; export it before running:
#   export AZCLIPS_SQL_ADMIN_PASSWORD='<a password meeting the Azure SQL policy>'
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

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang.
az config set --key agent.enabled --value false

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them.

SUB=f758ac53-3e63-4317-a956-0997793808d7
RG=rg-sql-test
LOC=southeastasia
SRV="azclips-sql-$(date +%y%m%d%H%M%S)"

# grep on the response JSON rather than jq - jq is not a dependency of this
# repo and this CLI has no --query. Switch to jq if assertions ever need real JSON paths.
FAILURES=0

pass() { printf 'PASS %s\n' "$1"; }
fail() { FAILURES=$((FAILURES + 1)); printf 'FAIL %s\n' "$1"; }

# assert_matches <text> <extended-regex> <because>
assert_matches() {
    if printf '%s' "$1" | grep -Eq -- "$2"; then pass "$3"; else fail "$3 - no match for '$2'"; fi
}

# assert_not_matches <text> <extended-regex> <because>
assert_not_matches() {
    if printf '%s' "$1" | grep -Eq -- "$2"; then fail "$3 - unexpected match for '$2'"; else pass "$3"; fi
}

# assert_exit <actual-code> <expected-code> <because>
assert_exit() {
    if [ "$1" -eq "$2" ]; then pass "$3"; else fail "$3 - expected exit $2, got $1"; fi
}

# assert_failed <actual-code> <because> - any non-zero code, the exact value is not contracted
assert_failed() {
    if [ "$1" -ne 0 ]; then pass "$2"; else fail "$2 - expected a non-zero exit, got 0"; fi
}

# Whitespace-tolerant JSON field matcher.
json_field() { printf '"%s"[[:space:]]*:[[:space:]]*"%s"' "$1" "$2"; }

# Prereqs: the resource group and the parent server.
az group create \
    --subscription-id "$SUB" \
    --name "$RG" \
    --location "$LOC"

az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD"

# Happy path: a single-address rule (start == end).
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name AllowMyIp \
    --start-ip-address 203.0.113.5 \
    --end-ip-address 203.0.113.5 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'single-IP create exits 0'
assert_matches "$OUT" "$(json_field name AllowMyIp)"    'single-IP create echoes the requested name'
assert_matches "$OUT" "$(json_field type 'Microsoft\.Sql/servers/firewallRules')" 'single-IP create returns the firewall rule resource type'
assert_matches "$OUT" "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft\.Sql/servers/$SRV/firewallRules/AllowMyIp" 'single-IP create returns the full ARM id'
assert_matches "$OUT" "$(json_field startIpAddress '203\.0\.113\.5')" 'single-IP create echoes --start-ip-address'
assert_matches "$OUT" "$(json_field endIpAddress '203\.0\.113\.5')"   'single-IP create echoes --end-ip-address'
# A firewall rule is a proxy resource; a location in the response would mean the request
# body carried one.
assert_not_matches "$OUT" '"location"' 'the rule has no location - it is a proxy resource'

# Happy path: a range, through the short aliases (-g -s -n).
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    -g "$RG" \
    -s "$SRV" \
    -n AllowOffice \
    --start-ip-address 203.0.113.5 \
    --end-ip-address 203.0.113.10 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'range create with short aliases exits 0'
assert_matches "$OUT" "$(json_field name AllowOffice)" 'short aliases bind to the same parameters as the long forms'
assert_matches "$OUT" "$(json_field startIpAddress '203\.0\.113\.5')"  'range create echoes the start of the range'
assert_matches "$OUT" "$(json_field endIpAddress '203\.0\.113\.10')"   'range create echoes the end of the range'

# Happy path: 0.0.0.0-0.0.0.0, ARM's convention for "all Azure services".
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name AllowAllWindowsAzureIps \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'allow-all-Azure create exits 0'
assert_matches "$OUT" "$(json_field name AllowAllWindowsAzureIps)" 'allow-all-Azure create echoes the well-known rule name'
assert_matches "$OUT" "$(json_field startIpAddress '0\.0\.0\.0')" 'allow-all-Azure create stores 0.0.0.0 as the start address'
assert_matches "$OUT" "$(json_field endIpAddress '0\.0\.0\.0')"   'allow-all-Azure create stores 0.0.0.0 as the end address'

# Idempotence: the PUT is a create-or-update, so re-running the first rule with a wider
# range must succeed and return the NEW range.
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name AllowMyIp \
    --start-ip-address 203.0.113.5 \
    --end-ip-address 203.0.113.20 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 're-creating an existing rule exits 0 (PUT is create-or-update)'
assert_matches "$OUT" "$(json_field endIpAddress '203\.0\.113\.20')" 'the re-created rule carries the updated end address'

# Help surface: the subgroup renders under az sql server, and the command renders every
# parameter with its short alias and [Required] markers.
OUT=$(az sql server --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit    "$RC" 0 'az sql server --help exits 0'
assert_matches "$OUT" 'firewall-rule' 'az sql server --help lists the firewall-rule subgroup'

OUT=$(az sql server firewall-rule --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit    "$RC" 0 'az sql server firewall-rule --help exits 0'
assert_matches "$OUT" 'create' 'az sql server firewall-rule --help lists the create command'

OUT=$(az sql server firewall-rule create --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit "$RC" 0 'az sql server firewall-rule create --help exits 0'
for flag in --resource-group --server --name --start-ip-address --end-ip-address; do
    assert_matches "$OUT" "$flag" "help documents $flag"
done
assert_matches "$OUT" '\-n' 'help documents the short aliases'
assert_matches "$OUT" '\[Required\]' 'help marks required parameters'
# A firewall rule has no location of its own; advertising one would be a parity bug.
assert_not_matches "$OUT" '\-\-location' 'help does not advertise a --location flag'

# Negative case: a required parameter is missing (--start-ip-address) and must be
# rejected before any request is sent.
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name AllowIncomplete \
    --end-ip-address 203.0.113.5 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'omitting required --start-ip-address exits non-zero'
assert_matches "$OUT" '\-\-start-ip-address|start-ip-address' 'the error names the missing parameter'
assert_not_matches "$OUT" "$(json_field name AllowIncomplete)" 'no rule is created when a required parameter is missing'

# Negative case: a non-IPv4 address. There is deliberately no client-side validation, so
# this proves the service error reaches the user.
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name AllowBadIp \
    --start-ip-address not-an-ip \
    --end-ip-address 203.0.113.5 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'a non-IPv4 --start-ip-address exits non-zero'
assert_matches "$OUT" 'FirewallRuleNotIPv4Address|IPv4|400' 'the service validation error is surfaced to the user'

# Negative case: an end address lower than the start address.
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name AllowInvertedRange \
    --start-ip-address 203.0.113.10 \
    --end-ip-address 203.0.113.5 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'an inverted IP range exits non-zero'
assert_matches "$OUT" 'FirewallRuleInvalidRange|range|400' 'the invalid-range error is surfaced to the user'

# Negative case: a server that does not exist.
OUT=$(az sql server firewall-rule create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV-does-not-exist" \
    --name AllowMyIp \
    --start-ip-address 203.0.113.5 \
    --end-ip-address 203.0.113.5 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'a missing parent server exits non-zero'
assert_matches "$OUT" 'ResourceNotFound|not found|NotFound|404' 'the missing-server failure is surfaced to the user'

# Cleanup. `az group delete` is not implemented by this CLI yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes

if [ "$FAILURES" -gt 0 ]; then
    printf '%s assertion(s) failed\n' "$FAILURES"
    exit 1
fi

printf 'All assertions passed\n'
