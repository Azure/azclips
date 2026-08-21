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

# Prereq: the resource group must exist. A SQL server does not have to sit in its
# resource group's region.
az group create \
    --subscription-id "$SUB" \
    --name "$RG" \
    --location "$LOC"

# Happy path: minimal create. This is a long-running operation - expect the PUT to
# return 202, the LRO to poll Azure-AsyncOperation every 15s, and the final resource
# JSON with "state": "Ready" and a fullyQualifiedDomainName of <name>.database.windows.net.
# ARM defaults version to 12.0, minimalTlsVersion to 1.2 and publicNetworkAccess to
# Enabled when they are not supplied.
az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD"

# Happy path: short aliases (-n -g -l -u -p) resolve through ParameterAliases.
az sql server create \
    --subscription-id "$SUB" \
    -g "$RG" \
    -n "$SRV-b" \
    -l "$LOC" \
    -u sqladmin \
    -p "$AZCLIPS_SQL_ADMIN_PASSWORD"

# Happy path: every optional property supplied. Expect them echoed back in the response
# properties rather than the ARM defaults.
az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV-c" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" \
    --server-version 12.0 \
    --minimal-tls-version 1.2 \
    --public-network-access Disabled

# Help surface: the group renders its subgroup, and the command renders every parameter
# with its short alias and [Required] markers.
az sql --help
az sql server --help
az sql server create --help

# Negative case: a required parameter is missing (--location) and must be rejected
# before any request is sent.
az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV-d" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD"

# Negative case: a value outside AllowedValues for --minimal-tls-version must be rejected.
az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV-e" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD" \
    --minimal-tls-version 9.9

# Negative case: a server name that is already taken globally must surface the ARM
# failure (LRO terminal state "Failed") and exit non-zero rather than reporting success.
az sql server create \
    --subscription-id "$SUB" \
    --resource-group "$RG" \
    --name "$SRV" \
    --location "$LOC" \
    --admin-user sqladmin \
    --admin-password "$AZCLIPS_SQL_ADMIN_PASSWORD"

# Cleanup. `az group delete` is not implemented by this CLI yet; use the shipping
# Azure CLI or the portal:
#   az group delete --name rg-sql-test --subscription f758ac53-3e63-4317-a956-0997793808d7 --yes
