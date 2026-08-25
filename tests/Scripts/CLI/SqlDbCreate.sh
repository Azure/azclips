# Create an Azure SQL database - CLI (az sql db create)
# Equivalent of: az sql db create
#
# Prereq: az login, and a SQL server to host the databases. This script creates its own
# server first, because a database inherits its region from its server and there is no
# --location flag to fall back on.
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
# Region note: 'eastus' returned RegionDoesNotAllowProvisioning during verification of
# the server command - a capacity condition, not a client bug. Pick another region when
# it recurs.
#
# Server names are GLOBALLY unique and lowercase; the timestamp suffix avoids collisions.
# Database names only have to be unique within their server.
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
DB=mydb

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

# Whitespace-tolerant JSON field matchers.
json_field() { printf '"%s"[[:space:]]*:[[:space:]]*"%s"' "$1" "$2"; }
json_number() { printf '"%s"[[:space:]]*:[[:space:]]*%s' "$1" "$2"; }

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

# Happy path: minimal create. Two calls under the hood - a GET on the server to resolve
# the location, then a long-running PUT on the database.
OUT=$(az sql db create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name "$DB" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'minimal create exits 0'
assert_matches "$OUT" "$(json_field name "$DB")"       'minimal create echoes the requested name'
assert_matches "$OUT" "$(json_field type 'Microsoft\.Sql/servers/databases')" 'minimal create returns the database resource type'
assert_matches "$OUT" "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft\.Sql/servers/$SRV/databases/$DB" 'minimal create returns the full ARM id'
# The decisive one for the orchestration: no --location was supplied, so this only
# matches if the server GET ran and its location reached the PUT body.
assert_matches "$OUT" "$(json_field location "$LOC")"  'the database inherited its location from the server'
# "Online" only appears after the LRO reached a terminal state, so this fails if the
# command ever returns the 202 body instead of polling to completion.
assert_matches "$OUT" "$(json_field status Online)"    'LRO ran to an Online database rather than returning the 202 body'

# Happy path: short aliases (-g -s -n -e) resolve through ParameterAliases, and an
# explicit SKU is echoed back instead of the ARM default.
OUT=$(az sql db create \
    --subscription-id "$SUB" \
    -g "$RG" \
    -s "$SRV" \
    -n "$DB-b" \
    -e Standard \
    --service-objective S0 \
    --collation SQL_Latin1_General_CP1_CI_AS \
    --max-size-bytes 268435456000 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'short aliases exit 0'
assert_matches "$OUT" "$(json_field name "$DB-b")"     'short aliases bind to the same parameters as the long forms'
assert_matches "$OUT" "$(json_field tier Standard)"    '-e binds to --edition and maps onto sku.tier'
assert_matches "$OUT" "$(json_field name S0)"          '--service-objective maps onto sku.name'
assert_matches "$OUT" "$(json_field collation 'SQL_Latin1_General_CP1_CI_AS')" 'create echoes --collation'
assert_matches "$OUT" "$(json_number maxSizeBytes 268435456000)" 'create echoes --max-size-bytes'
assert_matches "$OUT" "$(json_field status Online)"    'short-alias create ran the LRO to Online'

# Happy path: vCore SKU. --family and --capacity are the two parameters that only make
# sense together, so they are exercised as a pair.
OUT=$(az sql db create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name "$DB-c" \
    --edition GeneralPurpose \
    --family Gen5 \
    --capacity 2 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'vCore create exits 0'
assert_matches "$OUT" "$(json_field name "$DB-c")"        'vCore create echoes the requested name'
assert_matches "$OUT" "$(json_field tier GeneralPurpose)" 'vCore create echoes --edition'
assert_matches "$OUT" "$(json_field family Gen5)"         'vCore create echoes --family'
assert_matches "$OUT" "$(json_number capacity 2)"         'vCore create echoes --capacity'
assert_matches "$OUT" "$(json_field status Online)"       'vCore create ran the LRO to Online'

# Help surface: the group renders its subgroups, and the command renders every parameter
# with its short alias and [Required] markers.
OUT=$(az sql --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit    "$RC" 0 'az sql --help exits 0'
assert_matches "$OUT" 'db' 'az sql --help lists the db subgroup'

OUT=$(az sql db --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit    "$RC" 0 'az sql db --help exits 0'
assert_matches "$OUT" 'create' 'az sql db --help lists the create command'

OUT=$(az sql db create --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit "$RC" 0 'az sql db create --help exits 0'
for flag in --resource-group --server --name --edition --service-objective \
            --family --capacity --collation --max-size-bytes --elastic-pool --zone-redundant; do
    assert_matches "$OUT" "$flag" "help documents $flag"
done
assert_matches "$OUT" '\-n' 'help documents the short aliases'
assert_matches "$OUT" '\[Required\]' 'help marks required parameters'
# A database has no location of its own; advertising one would be a parity bug.
assert_not_matches "$OUT" '\-\-location' 'help does not advertise a --location flag'

# Negative case: a required parameter is missing (--server) and must be rejected before
# any request is sent.
OUT=$(az sql db create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$DB-d" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'omitting required --server exits non-zero'
assert_matches "$OUT" '\-\-server|server' 'the error names the missing parameter'
assert_not_matches "$OUT" "$(json_field status Online)" 'no database is created when a required parameter is missing'

# Negative case: a server that does not exist. The location lookup is the FIRST call, so
# this must fail there and never issue the database PUT.
OUT=$(az sql db create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV-does-not-exist" \
    --name "$DB-e" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'a missing parent server exits non-zero'
assert_matches "$OUT" 'ResourceNotFound|not found|NotFound|404' 'the server lookup failure is surfaced to the user'
assert_not_matches "$OUT" "$(json_field status Online)" 'no database is created when the server lookup fails'

# Negative case: --capacity is typed as an integer, so a non-numeric value must be
# rejected during parsing.
OUT=$(az sql db create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --server "$SRV" \
    --name "$DB-f" \
    --capacity notanumber 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" '--capacity notanumber exits non-zero'
assert_not_matches "$OUT" "$(json_field status Online)" 'no database is created when a value fails type conversion'

# Cleanup. `az group delete` is not implemented by this CLI yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes

if [ "$FAILURES" -gt 0 ]; then
    printf '%s assertion(s) failed\n' "$FAILURES"
    exit 1
fi

printf 'All assertions passed\n'
