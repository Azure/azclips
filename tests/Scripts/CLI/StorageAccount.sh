# Storage account - CLI (az storage account create/show/list/delete)
# Equivalent of: az storage account create/show/list/delete
#
# Shared test context:
#   subscription: resolved from the active profile (no --subscription-id needed)
#   resource group: a per-run azclips-storage-account-* group (region: australiaeast), deleted
#                   with every account in it when the script exits, even after a failure or Ctrl+C
#
# Account names must be 3-24 lowercase alphanumerics and are GLOBALLY unique, so each
# case below appends $RANDOM. Note the zone cases need a region with availability
# zones; australiasoutheast has none and the service rejects zone pinning there.

# Run unattended: on a command ERROR az normally hands off to the interactive
# agent-mode TUI (which would block on input). Disable the agent via config so the
# classic error and a non-zero exit surface instead, and the negative cases below
# don't hang.
#
# azclips reads config from ~/.azclips/config.json via HomePaths and has no
# AZURE_CONFIG_DIR-style override, so the setting cannot be isolated to this run.
# Save the previous value and restore it on exit instead, so running this script
# leaves the developer's configuration as it found it. `az config get` prints
# "agent.enabled = <value>[ [default]]"; the [default] marker means no explicit
# value was stored, in which case the key is deleted rather than written back.
PREV_AGENT_RAW="$(az config get --key agent.enabled 2>/dev/null || true)"
restore_agent_config() {
    case "$PREV_AGENT_RAW" in
        ""|*"[default]"*)
            az config delete --key agent.enabled >/dev/null 2>&1 || true
            ;;
        *)
            az config set --key agent.enabled --value "${PREV_AGENT_RAW##*= }" >/dev/null 2>&1 || true
            ;;
    esac
}

# The group name is unique per run, so deleting it on exit can only remove what this run
# created. Delete before restoring the agent config, so a failed delete exits with an error
# instead of handing off to the interactive agent.
RG="azclips-storage-account-$(date +%Y%m%d%H%M%S)-$$-$RANDOM"
LOC=australiaeast
cleanup() {
    az group delete --name "$RG" >/dev/null 2>&1 || true
    restore_agent_config
}
trap cleanup EXIT
# Every negative case depends on this: with the agent still enabled, an expected error would open
# the interactive agent and hang the run instead of failing, so stop here if it can't be set.
if ! az config set --key agent.enabled --value false; then
    echo "Could not disable agent mode; aborting before any case runs." >&2
    exit 1
fi

# No `set -e`: the negative cases are EXPECTED to exit non-zero, and the script must
# continue past them. Each command's exit code surfaces its own error on stderr.
#
# The exit status still means something. Any other command that fails trips the ERR trap,
# negative cases run through expect_failure (inside an `if`, which the ERR trap ignores) and
# count only if they unexpectedly succeed, and the script exits non-zero if anything counted.
UNEXPECTED=0
trap 'UNEXPECTED=$((UNEXPECTED + 1))' ERR
expect_failure() {
    if "$@"; then
        echo "UNEXPECTED SUCCESS: $*" >&2
        UNEXPECTED=$((UNEXPECTED + 1))
    fi
}

az group create --name "$RG" --location $LOC

# Happy path: the minimum required set. The request body must carry only sku, kind and
# location so every unspecified option keeps its service default.
az storage account create \
    --resource-group $RG \
    --name azclipsmin$RANDOM \
    --debug

# Scalar options and tags. Verify in the response that accessTier, minimumTlsVersion and
# the tag map came back as sent.
az storage account create \
    --resource-group $RG \
    --name azclipsopt$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --access-tier Cool \
    --min-tls-version TLS1_2 \
    --https-only true \
    --allow-blob-public-access false \
    --allow-shared-key-access true \
    --public-network-access Enabled \
    --dns-endpoint-type Standard \
    --tags env=dev owner=platform \
    --debug

# Managed identity plus SAS and key policies. The response should include an identity
# with a principalId, and a sasPolicy whose expirationAction defaulted to Log.
az storage account create \
    --resource-group $RG \
    --name azclipsid$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --identity-type SystemAssigned \
    --sas-expiration-period 90.00:00:00 \
    --key-expiration-period-in-days 60 \
    --debug

# Network rules. Unlike azure-cli's create, IP rules are accepted here rather than
# requiring a follow-up network-rule command. Bypass is sent as a comma-separated
# string and each IP rule uses the "value" wire name.
az storage account create \
    --resource-group $RG \
    --name azclipsnet$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --default-action Deny \
    --bypass Logging Metrics AzureServices \
    --ip-address 13.64.0.0/16 40.90.0.0/16 \
    --debug

# Account-level immutability with a policy, plus a routing preference. Immutability can
# only be enabled at creation time. Publishing Microsoft endpoints adds a
# primaryEndpoints.microsoftEndpoints block to the response.
az storage account create \
    --resource-group $RG \
    --name azclipsalw$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --enable-alw true \
    --immutability-period-since-creation-in-days 30 \
    --immutability-policy-state Unlocked \
    --allow-protected-append-writes true \
    --routing-choice MicrosoftRouting \
    --publish-microsoft-endpoints true \
    --debug

# Hierarchical namespace with SFTP. SFTP requires a hierarchical namespace, so the two
# are set together.
az storage account create \
    --resource-group $RG \
    --name azclipshns$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --enable-hierarchical-namespace true \
    --enable-sftp true \
    --debug

# Service-scoped encryption keys. Requesting a key type for table and queue replaces the
# default blob services entry in the request.
az storage account create \
    --resource-group $RG \
    --name azclipsenc$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --encryption-key-type-for-table Account \
    --encryption-key-type-for-queue Account \
    --require-infrastructure-encryption true \
    --debug

# Azure Files share permission without an authentication enabler. The service requires
# directoryServiceOptions, so the request carries it as None.
az storage account create \
    --resource-group $RG \
    --name azclipsfiles$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --default-share-permission StorageFileDataSmbShareReader \
    --debug

# Zone pinning. Requires a region with availability zones; expect the service to reject
# this in a region without them.
az storage account create \
    --resource-group $RG \
    --name azclipszone$RANDOM \
    --location $LOC \
    --sku Standard_GRS \
    --kind StorageV2 \
    --zone-placement-policy Any \
    --enable-blob-geo-priority-replication true \
    --debug

# Negative case: invalid --sku value. Expected result: the command fails fast listing the
# allowed SKU names and does NOT create an account.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_BOGUS \
    --kind StorageV2

# Negative case: missing the required account name. Location, SKU and kind are optional
# and resolve to the resource-group location, Standard_RAGRS and StorageV2.
expect_failure az storage account create \
    --resource-group $RG

# Negative case: --sas-expiration-action without --sas-expiration-period. Both members are
# required by the service, so this is rejected locally.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --sas-expiration-action Block

# Negative case: a network rule without --default-action, which the service requires.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --bypass Logging Metrics

# Negative case: a subnet NAME without --vnet-name. A name cannot be composed into a
# resource id without the virtual network it belongs to.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --default-action Deny \
    --subnet mysubnet

# Negative case: immutability policy options without --enable-alw true.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --immutability-policy-state Locked

# Negative case: two Azure Files authentication enablers. They map onto a single
# directoryServiceOptions value, so at most one may be true.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --enable-files-aadds true \
    --enable-files-adds true

# Negative case: --enable-files-adds without the full Active Directory property set.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --enable-files-adds true \
    --domain-name contoso.com

# Negative case: a key vault URI without a key name. Both are required together.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --encryption-key-vault https://myvault.vault.azure.net/

# Negative case: a malformed tag that is not in KEY=VALUE form.
expect_failure az storage account create \
    --resource-group $RG \
    --name azclipsbad$RANDOM \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2 \
    --tags notavalidtag

# ── show / list / delete ──
#
# These reuse one account so the read cases have something stable to act on, and the
# delete at the end doubles as the cleanup this script used to leave to the portal.
CRUD_NAME=azclipscrud$RANDOM

az storage account create \
    --resource-group $RG \
    --name $CRUD_NAME \
    --location $LOC \
    --sku Standard_LRS \
    --kind StorageV2

az storage account show \
    --resource-group $RG \
    --name $CRUD_NAME

# Geo-replication stats are only returned when asked for; Standard_LRS is not geo-redundant,
# so expect the expansion to come back empty rather than populated.
az storage account show \
    --resource-group $RG \
    --name $CRUD_NAME \
    --expand geoReplicationStats

# Scoped to the resource group, then across the whole subscription. The second form takes
# the other API operation, so it is worth running both.
az storage account list --resource-group $RG

az storage account list

az storage account delete \
    --resource-group $RG \
    --name $CRUD_NAME

# Deleting twice: the second call should report that the account no longer exists.
expect_failure az storage account delete \
    --resource-group $RG \
    --name $CRUD_NAME

# Negative case: show an account that does not exist.
expect_failure az storage account show \
    --resource-group $RG \
    --name azclipsdoesnotexist

if (( UNEXPECTED > 0 )); then
    echo "$UNEXPECTED case(s) did not behave as expected; see the output above." >&2
    exit 1
fi
echo "All storage account cases behaved as expected."
