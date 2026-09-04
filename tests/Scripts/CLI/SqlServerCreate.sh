# Create an Azure SQL logical server - CLI (az sql server create)
# Equivalent of: az sql server create
#
# Prereq: az login. The admin password is read from AZCLIPS_SQL_ADMIN_PASSWORD so no
# credential is committed; export it before running:
#   export AZCLIPS_SQL_ADMIN_PASSWORD='<a password meeting the Azure SQL policy>'
#
# Shared test context:
#   subscription: f758ac53-3e63-4317-a956-0997793808d7
#   resource group: rg-sql-test
#   region: southeastasia
#
# Region note: 'eastus' returned RegionDoesNotAllowProvisioning during verification
# ("Location 'East US' is not accepting creation of new Windows Azure SQL Database
# servers at this time") - the same failure reproduces with the shipping Azure CLI, so
# it is a capacity condition, not a client bug. Pick another region when it recurs.
#
# Server names are GLOBALLY unique and lowercase; the timestamp suffix avoids collisions.
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

# Whitespace-tolerant JSON field matcher: json_field state Ready -> "state" *: *"Ready"
json_field() { printf '"%s"[[:space:]]*:[[:space:]]*"%s"' "$1" "$2"; }

# Prereq: the resource group must exist. A SQL server does not have to sit in its
# resource group's region.
az group create \
    --subscription-id "$SUB" \
    --name "$RG" \
    --location "$LOC"

# Happy path: minimal create. This is a long-running operation - the PUT returns 202, the
# LRO polls Azure-AsyncOperation every 15s, and the terminal response is the resource JSON.
# ARM defaults version to 12.0, minimalTlsVersion to 1.2 and publicNetworkAccess to
# Enabled when they are not supplied.
OUT=$(az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'minimal create exits 0'
assert_matches "$OUT" "$(json_field name "$SRV")"          'minimal create echoes the requested name'
assert_matches "$OUT" "$(json_field location "$LOC")"      'minimal create echoes the requested location'
assert_matches "$OUT" "$(json_field type 'Microsoft\.Sql/servers')" 'minimal create returns the SQL server resource type'
assert_matches "$OUT" "/subscriptions/$SUB/resourceGroups/$RG/providers/Microsoft\.Sql/servers/$SRV" 'minimal create returns the full ARM id'
# The decisive one: "Ready" only appears after the LRO reached a terminal state, so this
# fails if the command ever returns the 202 body instead of polling to completion.
assert_matches "$OUT" "$(json_field state Ready)"          'LRO ran to a Ready server rather than returning the 202 body'
assert_matches "$OUT" "$(json_field fullyQualifiedDomainName "$SRV\.database\.windows\.net")" 'minimal create returns the resolvable FQDN'
assert_matches "$OUT" "$(json_field administratorLogin sqladmin)" 'minimal create echoes the administrator login'
# The password is create-only input and must never come back on the response or in logs.
assert_not_matches "$OUT" 'administratorLoginPassword' 'response does not echo the administrator password'
assert_not_matches "$OUT" "$AZCLIPS_SQL_ADMIN_PASSWORD" 'password does not leak into the output'
# ARM defaults, asserted so a change in the pinned api-version surfaces here.
assert_matches "$OUT" "$(json_field version '12\.0')"          'ARM defaults version to 12.0'
assert_matches "$OUT" "$(json_field minimalTlsVersion '1\.2')" 'ARM defaults minimalTlsVersion to 1.2'
assert_matches "$OUT" "$(json_field publicNetworkAccess Enabled)" 'ARM defaults publicNetworkAccess to Enabled'

# Happy path: short aliases (-n -g -l -u -p) resolve through ParameterAliases.
OUT=$(az sql server create \
    --subscription-id "$SUB" \
    -g "$RG" \
    -n "$SRV-b" \
    -l "$LOC" \
    -u sqladmin \
    -p "$AZCLIPS_SQL_ADMIN_PASSWORD" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'short aliases exit 0'
assert_matches "$OUT" "$(json_field name "$SRV-b")"   'short aliases bind to the same parameters as the long forms'
assert_matches "$OUT" "$(json_field location "$LOC")" '-l binds to --location'
assert_matches "$OUT" "$(json_field administratorLogin sqladmin)" '-u binds to --admin-user'
assert_matches "$OUT" "$(json_field state Ready)"     'short-alias create ran the LRO to Ready'

# Happy path: every optional property supplied. Expect them echoed back in the response
# properties rather than the ARM defaults.
OUT=$(az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV-c" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" \
    --server-version 12.0 \
    --minimal-tls-version 1.2 \
    --public-network-access Disabled 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_exit    "$RC" 0 'full create exits 0'
assert_matches "$OUT" "$(json_field name "$SRV-c")"            'full create echoes the requested name'
assert_matches "$OUT" "$(json_field state Ready)"              'full create ran the LRO to Ready'
assert_matches "$OUT" "$(json_field version '12\.0')"          'full create echoes --server-version'
assert_matches "$OUT" "$(json_field minimalTlsVersion '1\.2')" 'full create echoes --minimal-tls-version'
# The one supplied value that differs from the ARM default - proves the parameter is wired
# through to the PUT body rather than the response merely looking correct by coincidence.
assert_matches "$OUT" "$(json_field publicNetworkAccess Disabled)" 'full create echoes --public-network-access Disabled'

# Help surface: the group renders its subgroup, and the command renders every parameter
# with its short alias and [Required] markers.
OUT=$(az sql --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit    "$RC" 0 'az sql --help exits 0'
assert_matches "$OUT" 'server' 'az sql --help lists the server subgroup'

OUT=$(az sql server --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit    "$RC" 0 'az sql server --help exits 0'
assert_matches "$OUT" 'create' 'az sql server --help lists the create command'

OUT=$(az sql server create --help 2>&1); RC=$?
printf '%s\n' "$OUT"
assert_exit "$RC" 0 'az sql server create --help exits 0'
for flag in --resource-group --name --location --admin-user --admin-password \
            --server-version --minimal-tls-version --public-network-access; do
    assert_matches "$OUT" "$flag" "help documents $flag"
done
assert_matches "$OUT" '\-n' 'help documents the short aliases'
assert_matches "$OUT" '\[Required\]' 'help marks required parameters'

# Negative case: a required parameter is missing (--location) and must be rejected
# before any request is sent.
OUT=$(az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV-d" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'omitting required --location exits non-zero'
assert_matches "$OUT" '\-\-location|location' 'the error names the missing parameter'
# Rejected during parsing means nothing was created.
assert_not_matches "$OUT" "$(json_field state Ready)" 'no server is created when a required parameter is missing'

# Negative case: a value outside AllowedValues for --minimal-tls-version must be rejected.
OUT=$(az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV-e" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" \
    --minimal-tls-version 9.9 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" '--minimal-tls-version 9.9 exits non-zero'
assert_matches "$OUT" '9\.9|allowed|not valid|invalid' 'the error names the rejected value or the allowed set'
assert_not_matches "$OUT" "$(json_field state Ready)" 'no server is created when a value fails validation'

# Negative case: a server name that is already taken globally must surface the ARM
# failure (LRO terminal state "Failed") and exit non-zero rather than reporting success.
OUT=$(az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" 2>&1)
RC=$?
printf '%s\n' "$OUT"

assert_failed  "$RC" 'a duplicate server name exits non-zero'
assert_matches "$OUT" 'already exists|NameAlreadyExists|Conflict|Failed' 'the ARM failure is surfaced to the user'

# Cleanup. `az group delete` is not implemented by this CLI yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes

if [ "$FAILURES" -gt 0 ]; then
    printf '%s assertion(s) failed\n' "$FAILURES"
    exit 1
fi

printf 'All assertions passed\n'
